using System.Diagnostics;
using System.Text.Json;
using AIJobCoach.Api.Data;
using AIJobCoach.Api.Modules.AI.Application;
using AIJobCoach.Api.Modules.Resumes.Domain;
using AIJobCoach.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace AIJobCoach.Api.Modules.Resumes.Application;

/// <summary>
/// Produces and reads the Developer Profile for a resume. An analysis is persisted once
/// per resume; a repeat request returns the stored result rather than calling the
/// provider again, and only an explicit forced re-analysis spends another call.
/// </summary>
public sealed class ResumeAnalysisService
{
    private const string PromptName = "resume-analysis";

    private static readonly JsonSerializerOptions PayloadOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly string[] EmptyCollection = [];

    private readonly AppDbContext _dbContext;
    private readonly IOpenAIClient _openAIClient;
    private readonly PromptLoader _promptLoader;
    private readonly ILogger<ResumeAnalysisService> _logger;

    public ResumeAnalysisService(
        AppDbContext dbContext,
        IOpenAIClient openAIClient,
        PromptLoader promptLoader,
        ILogger<ResumeAnalysisService> logger)
    {
        _dbContext = dbContext;
        _openAIClient = openAIClient;
        _promptLoader = promptLoader;
        _logger = logger;
    }

    /// <summary>
    /// Returns the persisted analysis for a resume, or analyses it. When an analysis
    /// already exists and <paramref name="force"/> is false, the stored result is
    /// returned and the provider is not called.
    /// </summary>
    public async Task<Result<ResumeAnalysisDto>> AnalyseAsync(
        Guid resumeId,
        Guid userId,
        bool force,
        CancellationToken ct = default)
    {
        if (resumeId == Guid.Empty)
        {
            throw new ArgumentException("Resume id is required.", nameof(resumeId));
        }

        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User id is required.", nameof(userId));
        }

        var resume = await _dbContext.Resumes
            .Include(r => r.Analysis)
            .FirstOrDefaultAsync(
                r => r.Id == resumeId && r.UserId == userId && r.IsActive,
                ct);

        if (resume is null)
        {
            return NotFound();
        }

        if (resume.Analysis is not null && !force)
        {
            _logger.LogInformation(
                "Resume analysis served from store. PromptName={PromptName} ResumeId={ResumeId} Status={Status}",
                PromptName,
                resumeId,
                "Cached");

            return Result<ResumeAnalysisDto>.Success(ToDto(resume.Analysis));
        }

        if (string.IsNullOrWhiteSpace(resume.ContentText))
        {
            return Result<ResumeAnalysisDto>.Failure(new Error(
                "CONTENT_TEXT_MISSING",
                "This resume has no extracted text to analyse."));
        }

        var systemPrompt = _promptLoader.Load(PromptName);
        var stopwatch = Stopwatch.StartNew();

        string raw;
        try
        {
            raw = await _openAIClient.CompleteAsync(systemPrompt, resume.ContentText, ct);
        }
        catch (OpenAIParseException ex)
        {
            stopwatch.Stop();
            LogFailure(resumeId, stopwatch, "ParseError", ex);
            return ParseError();
        }
        catch (HttpRequestException ex)
        {
            stopwatch.Stop();
            LogFailure(resumeId, stopwatch, "Unavailable", ex);
            return Unavailable();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // The caller gave up — a disconnected client, not a provider outage. Let it
            // propagate so nothing is persisted and no failure is reported as an outage.
            throw;
        }
        catch (OperationCanceledException ex)
        {
            stopwatch.Stop();
            LogFailure(resumeId, stopwatch, "Unavailable", ex);
            return Unavailable();
        }

        stopwatch.Stop();

        ResumeAnalysisPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<ResumeAnalysisPayload>(
                AiJsonResponse.Unwrap(raw), PayloadOptions);
        }
        catch (JsonException ex)
        {
            LogFailure(resumeId, stopwatch, "ParseError", ex);
            return ParseError();
        }

        if (payload is null)
        {
            LogFailure(resumeId, stopwatch, "ParseError", exception: null);
            return ParseError();
        }

        // Nothing above this line has mutated state, so a failed re-analysis leaves the
        // previously stored profile exactly as it was.
        var isNewAnalysis = resume.Analysis is null;

        if (isNewAnalysis)
        {
            _dbContext.ResumeAnalysis.Add(CreateAnalysis(resumeId, payload, raw));
        }
        else
        {
            ApplyPayload(resume.Analysis!, payload, raw);
        }

        try
        {
            await _dbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateException) when (isNewAnalysis)
        {
            // A concurrent first analysis won the unique index on resume_id. Its result
            // is the correct answer for this request too.
            var winner = await _dbContext.ResumeAnalysis
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.ResumeId == resumeId, ct);

            if (winner is null)
            {
                throw;
            }

            return Result<ResumeAnalysisDto>.Success(ToDto(winner));
        }

        _logger.LogInformation(
            "Resume analysis completed. PromptName={PromptName} ResumeId={ResumeId} LatencyMs={LatencyMs} Status={Status}",
            PromptName,
            resumeId,
            stopwatch.ElapsedMilliseconds,
            "Analysed");

        var analysis = resume.Analysis
            ?? await _dbContext.ResumeAnalysis.AsNoTracking()
                .FirstAsync(a => a.ResumeId == resumeId, ct);

        return Result<ResumeAnalysisDto>.Success(ToDto(analysis));
    }

    /// <summary>
    /// Returns the persisted analysis for a resume. Never calls the provider.
    /// </summary>
    public async Task<Result<ResumeAnalysisDto>> GetAnalysisAsync(
        Guid resumeId,
        Guid userId,
        CancellationToken ct = default)
    {
        var resume = await _dbContext.Resumes
            .AsNoTracking()
            .Include(r => r.Analysis)
            .FirstOrDefaultAsync(
                r => r.Id == resumeId && r.UserId == userId && r.IsActive,
                ct);

        if (resume is null)
        {
            return NotFound();
        }

        if (resume.Analysis is null)
        {
            return Result<ResumeAnalysisDto>.Failure(new Error(
                "ANALYSIS_NOT_FOUND",
                "This resume has not been analysed yet."));
        }

        return Result<ResumeAnalysisDto>.Success(ToDto(resume.Analysis));
    }

    private static ResumeAnalysis CreateAnalysis(
        Guid resumeId,
        ResumeAnalysisPayload payload,
        string raw)
    {
        return new ResumeAnalysis(
            resumeId,
            Trimmed(payload.CareerSummary),
            payload.YearsOfExperience,
            NormaliseCareerLevel(payload.CareerLevel),
            Serialise(payload.ProgrammingLanguages),
            Serialise(payload.Frameworks),
            Serialise(payload.CloudPlatforms),
            Serialise(payload.Databases),
            Serialise(payload.Tools),
            SerialiseProjects(payload.Projects),
            raw);
    }

    private static void ApplyPayload(
        ResumeAnalysis analysis,
        ResumeAnalysisPayload payload,
        string raw)
    {
        analysis.Update(
            Trimmed(payload.CareerSummary),
            payload.YearsOfExperience,
            NormaliseCareerLevel(payload.CareerLevel),
            Serialise(payload.ProgrammingLanguages),
            Serialise(payload.Frameworks),
            Serialise(payload.CloudPlatforms),
            Serialise(payload.Databases),
            Serialise(payload.Tools),
            SerialiseProjects(payload.Projects),
            raw);
    }

    private static ResumeAnalysisDto ToDto(ResumeAnalysis analysis)
    {
        return new ResumeAnalysisDto(
            analysis.Id,
            analysis.ResumeId,
            analysis.Summary,
            analysis.YearsOfExperience,
            analysis.CareerLevel,
            Deserialise(analysis.ProgrammingLanguages),
            Deserialise(analysis.Frameworks),
            Deserialise(analysis.CloudPlatforms),
            Deserialise(analysis.Databases),
            Deserialise(analysis.Tools),
            DeserialiseProjects(analysis.Projects),
            analysis.AnalysedAt,
            analysis.CreatedAt);
    }

    private static string Serialise(string[]? values)
    {
        return JsonSerializer.Serialize(values ?? EmptyCollection);
    }

    private static string SerialiseProjects(ResumeAnalysisPayloadProject[]? projects)
    {
        var mapped = (projects ?? [])
            .Where(p => !string.IsNullOrWhiteSpace(p.Name))
            .Select(p => new ResumeAnalysisProjectDto(
                p.Name!.Trim(),
                Trimmed(p.Description),
                p.Technologies ?? EmptyCollection))
            .ToArray();

        return JsonSerializer.Serialize(mapped);
    }

    private static string[] Deserialise(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<string[]>(json) ?? EmptyCollection;
        }
        catch (JsonException)
        {
            return EmptyCollection;
        }
    }

    private static ResumeAnalysisProjectDto[] DeserialiseProjects(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<ResumeAnalysisProjectDto[]>(json, PayloadOptions)
                ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string? NormaliseCareerLevel(string? careerLevel)
    {
        // Stored as the model reported it, only tidied. An unexpected level is weak
        // output, not a parse failure, and must not cost an otherwise usable profile.
        return string.IsNullOrWhiteSpace(careerLevel)
            ? null
            : careerLevel.Trim().ToLowerInvariant();
    }

    private static string? Trimmed(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static Result<ResumeAnalysisDto> NotFound()
    {
        return Result<ResumeAnalysisDto>.Failure(new Error(
            "RESUME_NOT_FOUND",
            "Resume was not found."));
    }

    private static Result<ResumeAnalysisDto> ParseError()
    {
        return Result<ResumeAnalysisDto>.Failure(new Error(
            "AI_PARSE_ERROR",
            "The analysis provider returned a response that could not be understood."));
    }

    private static Result<ResumeAnalysisDto> Unavailable()
    {
        return Result<ResumeAnalysisDto>.Failure(new Error(
            "AI_UNAVAILABLE",
            "The analysis provider is currently unavailable. Please try again."));
    }

    /// <summary>
    /// Logs a failed analysis. Only identifiers and timings are recorded — never the
    /// resume text, the prompt body, or the provider response.
    /// </summary>
    private void LogFailure(
        Guid resumeId,
        Stopwatch stopwatch,
        string status,
        Exception? exception)
    {
        _logger.LogWarning(
            exception,
            "Resume analysis failed. PromptName={PromptName} ResumeId={ResumeId} LatencyMs={LatencyMs} Status={Status}",
            PromptName,
            resumeId,
            stopwatch.ElapsedMilliseconds,
            status);
    }
}

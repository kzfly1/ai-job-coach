using AIJobCoach.Api.Data;
using AIJobCoach.Api.Modules.Resumes.Domain;
using AIJobCoach.Api.Modules.Resumes.Shared;
using AIJobCoach.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace AIJobCoach.Api.Modules.Resumes.Application;

public sealed class ResumeService
{
    private const long MaxFileSizeBytes = 5 * 1024 * 1024;

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ResumeMimeTypes.Pdf,
        ResumeMimeTypes.Docx
    };

    private readonly AppDbContext _dbContext;
    private readonly IFileStorageService _fileStorage;
    private readonly IResumeTextExtractor _textExtractor;

    public ResumeService(
        AppDbContext dbContext,
        IFileStorageService fileStorage,
        IResumeTextExtractor textExtractor)
    {
        _dbContext = dbContext;
        _fileStorage = fileStorage;
        _textExtractor = textExtractor;
    }

    public async Task<Result<ResumeDto>> UploadAsync(
        Guid userId,
        Stream fileStream,
        string fileName,
        string contentType,
        long fileSizeBytes,
        CancellationToken ct = default)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User id is required.", nameof(userId));
        }

        if (fileStream is null)
        {
            throw new ArgumentNullException(nameof(fileStream));
        }

        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentException("File name is required.", nameof(fileName));
        }

        if (string.IsNullOrWhiteSpace(contentType) || !AllowedContentTypes.Contains(contentType))
        {
            return Result<ResumeDto>.Failure(
                new Error("UNSUPPORTED_FILE_TYPE", "Only PDF and DOCX files are supported."));
        }
        
        if (fileSizeBytes < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(fileSizeBytes),
                "File size cannot be negative.");
        }

        if (fileSizeBytes > MaxFileSizeBytes)
        {
            return Result<ResumeDto>.Failure(
                new Error("FILE_TOO_LARGE", "File size must not exceed 5 MB."));
        }

        using var buffer = new MemoryStream();
        await fileStream.CopyToAsync(buffer, ct);

        buffer.Position = 0;
        var storagePath = await _fileStorage.SaveAsync(buffer, fileName, contentType, userId, ct);

        buffer.Position = 0;
        var contentText = await _textExtractor.ExtractAsync(buffer, contentType, ct);

        var resume = new Resume(userId, fileName, storagePath, contentText);

        _dbContext.Resumes.Add(resume);
        await _dbContext.SaveChangesAsync(ct);

        return Result<ResumeDto>.Success(ToDto(resume));
    }

    public async Task<ResumeDto?> GetByIdAsync(
        Guid resumeId,
        Guid userId,
        CancellationToken ct = default)
    {
        var resume = await _dbContext.Resumes
            .AsNoTracking()
            .FirstOrDefaultAsync(
                r => r.Id == resumeId && r.UserId == userId && r.IsActive,
                ct);

        return resume is null ? null : ToDto(resume);
    }

    public async Task<IReadOnlyList<ResumeDto>> ListByUserAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        return await _dbContext.Resumes
            .AsNoTracking()
            .Where(r => r.UserId == userId && r.IsActive)
            .OrderByDescending(r => r.UploadedAt)
            .Select(r => new ResumeDto(
                r.Id,
                r.UserId,
                r.FileName,
                r.ContentText == null ? 0 : r.ContentText.Length,
                r.IsActive,
                r.UploadedAt))
            .ToListAsync(ct);
    }

    public async Task<Result<bool>> SoftDeleteAsync(
        Guid resumeId,
        Guid userId,
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
            .FirstOrDefaultAsync(r => r.Id == resumeId && r.UserId == userId && r.IsActive, ct);

        if (resume is null)
        {
            return Result<bool>.Failure(
                new Error("RESUME_NOT_FOUND", "Resume not found."));
        }

        resume.Deactivate();
        
        await _dbContext.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }

    private static ResumeDto ToDto(Resume resume)
    {
        return new ResumeDto(
            resume.Id,
            resume.UserId,
            resume.FileName,
            resume.ContentText?.Length ?? 0,
            resume.IsActive,
            resume.UploadedAt);
    }
}

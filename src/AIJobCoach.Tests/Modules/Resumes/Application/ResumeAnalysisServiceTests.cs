using System.Text.Json;
using AIJobCoach.Api.Data;
using AIJobCoach.Api.Modules.AI.Application;
using AIJobCoach.Api.Modules.Resumes.Application;
using AIJobCoach.Api.Modules.Resumes.Domain;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AIJobCoach.Tests.Modules.Resumes.Application;

public sealed class ResumeAnalysisServiceTests : IDisposable
{
    private const string ValidPayload = """
        {
          "careerLevel": "Senior",
          "yearsOfExperience": 8,
          "careerSummary": "Backend engineer focused on .NET services.",
          "programmingLanguages": ["C#", "TypeScript"],
          "frameworks": ["ASP.NET Core"],
          "cloudPlatforms": ["Azure"],
          "databases": ["PostgreSQL"],
          "tools": ["Docker"],
          "projects": [
            { "name": "Job Coach", "description": "Career tool", "technologies": ["C#"] }
          ]
        }
        """;

    private readonly string _promptsDirectory;

    public ResumeAnalysisServiceTests()
    {
        _promptsDirectory = Path.Combine(
            Path.GetTempPath(), "resume-analysis-service-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(_promptsDirectory);
        File.WriteAllText(
            Path.Combine(_promptsDirectory, "resume-analysis.txt"),
            "You are an expert technical recruiter. Return only JSON matching the schema.");
    }

    // ---------- Analyse: happy path ----------

    [Fact]
    public async Task AnalyseAsync_ShouldPersistAnalysis_AndReturnProfile_OnFirstRun()
    {
        // Arrange
        await using var db = CreateDbContext();
        var resume = await SeedResumeAsync(db, contentText: "C# developer with 8 years.");
        var client = new FakeOpenAIClient(ValidPayload);
        var service = CreateService(db, client);

        // Act
        var result = await service.AnalyseAsync(resume.Id, resume.UserId, force: false);

        // Assert
        result.IsSuccess.Should().BeTrue();
        client.CallCount.Should().Be(1);

        var dto = result.Value!;
        dto.ResumeId.Should().Be(resume.Id);
        dto.Summary.Should().Be("Backend engineer focused on .NET services.");
        dto.YearsOfExperience.Should().Be(8);
        dto.CareerLevel.Should().Be("senior");
        dto.ProgrammingLanguages.Should().BeEquivalentTo("C#", "TypeScript");
        dto.Frameworks.Should().BeEquivalentTo("ASP.NET Core");
        dto.CloudPlatforms.Should().BeEquivalentTo("Azure");
        dto.Databases.Should().BeEquivalentTo("PostgreSQL");
        dto.Tools.Should().BeEquivalentTo("Docker");
        dto.Projects.Should().HaveCount(1);
        dto.Projects[0].Name.Should().Be("Job Coach");
        dto.Projects[0].Technologies.Should().BeEquivalentTo("C#");

        (await db.ResumeAnalysis.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task AnalyseAsync_ShouldPersistCollections_AsValidJsonArrays()
    {
        // Arrange
        await using var db = CreateDbContext();
        var resume = await SeedResumeAsync(db, contentText: "C# developer.");
        var service = CreateService(db, new FakeOpenAIClient(ValidPayload));

        // Act
        await service.AnalyseAsync(resume.Id, resume.UserId, force: false);

        // Assert
        var stored = await db.ResumeAnalysis.SingleAsync();
        JsonSerializer.Deserialize<string[]>(stored.ProgrammingLanguages)
            .Should().BeEquivalentTo("C#", "TypeScript");
        JsonSerializer.Deserialize<string[]>(stored.Tools)
            .Should().BeEquivalentTo("Docker");
        var act = () => JsonSerializer.Deserialize<JsonElement>(stored.Projects);
        act.Should().NotThrow();
    }

    [Fact]
    public async Task AnalyseAsync_ShouldSendFullContentText_AsUserPrompt()
    {
        // Arrange
        await using var db = CreateDbContext();
        var longText = new string('x', 45_000);
        var resume = await SeedResumeAsync(db, contentText: longText);
        var client = new FakeOpenAIClient(ValidPayload);
        var service = CreateService(db, client);

        // Act
        await service.AnalyseAsync(resume.Id, resume.UserId, force: false);

        // Assert — resume text is never truncated
        client.LastUserPrompt.Should().Be(longText);
        client.LastUserPrompt!.Length.Should().Be(45_000);
    }

    [Fact]
    public async Task AnalyseAsync_ShouldSendPromptFileAsSystemPrompt_WithoutResumeText()
    {
        // Arrange
        await using var db = CreateDbContext();
        var resume = await SeedResumeAsync(db, contentText: "UNIQUE-RESUME-MARKER");
        var client = new FakeOpenAIClient(ValidPayload);
        var service = CreateService(db, client);

        // Act
        await service.AnalyseAsync(resume.Id, resume.UserId, force: false);

        // Assert
        client.LastSystemPrompt.Should().Contain("Return only JSON");
        client.LastSystemPrompt.Should().NotContain("UNIQUE-RESUME-MARKER");
    }

    [Fact]
    public async Task AnalyseAsync_ShouldStoreRawResponse_ButNotReturnIt()
    {
        // Arrange
        await using var db = CreateDbContext();
        var resume = await SeedResumeAsync(db, contentText: "C# developer.");
        var service = CreateService(db, new FakeOpenAIClient(ValidPayload));

        // Act
        var result = await service.AnalyseAsync(resume.Id, resume.UserId, force: false);

        // Assert
        (await db.ResumeAnalysis.SingleAsync()).RawResponse.Should().Contain("careerSummary");
        result.Value.Should().NotBeNull();
        typeof(ResumeAnalysisDto).GetProperty("RawResponse").Should().BeNull();
    }

    [Fact]
    public async Task AnalyseAsync_ShouldDefaultMissingSections_ToEmptyArrays()
    {
        // Arrange
        await using var db = CreateDbContext();
        var resume = await SeedResumeAsync(db, contentText: "Sparse resume.");
        var sparse = """{"careerSummary":"Short.","careerLevel":"junior"}""";
        var service = CreateService(db, new FakeOpenAIClient(sparse));

        // Act
        var result = await service.AnalyseAsync(resume.Id, resume.UserId, force: false);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.ProgrammingLanguages.Should().BeEmpty();
        result.Value.Projects.Should().BeEmpty();
        result.Value.YearsOfExperience.Should().BeNull();
    }

    [Fact]
    public async Task AnalyseAsync_ShouldSucceed_WhenProviderWrapsJsonInMarkdownFence()
    {
        // Arrange
        await using var db = CreateDbContext();
        var resume = await SeedResumeAsync(db, contentText: "C# developer.");
        var fenced = "```json\n" + ValidPayload + "\n```";
        var service = CreateService(db, new FakeOpenAIClient(fenced));

        // Act
        var result = await service.AnalyseAsync(resume.Id, resume.UserId, force: false);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.CareerLevel.Should().Be("senior");
    }

    // ---------- Analyse: persisted-result reuse and force ----------

    [Fact]
    public async Task AnalyseAsync_ShouldReturnStoredAnalysis_AndNotCallProvider_WhenAnalysisExists()
    {
        // Arrange
        await using var db = CreateDbContext();
        var resume = await SeedResumeAsync(db, contentText: "C# developer.");
        var client = new FakeOpenAIClient(ValidPayload);
        var service = CreateService(db, client);
        await service.AnalyseAsync(resume.Id, resume.UserId, force: false);

        // Act
        var result = await service.AnalyseAsync(resume.Id, resume.UserId, force: false);

        // Assert
        result.IsSuccess.Should().BeTrue();
        client.CallCount.Should().Be(1);
        (await db.ResumeAnalysis.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task AnalyseAsync_ShouldUpdateInPlace_WhenForced()
    {
        // Arrange
        await using var db = CreateDbContext();
        var resume = await SeedResumeAsync(db, contentText: "C# developer.");
        var client = new FakeOpenAIClient(ValidPayload);
        var service = CreateService(db, client);

        var first = await service.AnalyseAsync(resume.Id, resume.UserId, force: false);
        var firstDto = first.Value!;

        await Task.Delay(10);
        client.Response = """
            {
              "careerLevel": "lead",
              "yearsOfExperience": 12,
              "careerSummary": "Now a lead.",
              "programmingLanguages": ["Go"],
              "frameworks": [],
              "cloudPlatforms": [],
              "databases": [],
              "tools": [],
              "projects": []
            }
            """;

        // Act
        var second = await service.AnalyseAsync(resume.Id, resume.UserId, force: true);

        // Assert
        second.IsSuccess.Should().BeTrue();
        client.CallCount.Should().Be(2);
        (await db.ResumeAnalysis.CountAsync()).Should().Be(1);

        var secondDto = second.Value!;
        secondDto.Id.Should().Be(firstDto.Id);
        secondDto.CreatedAt.Should().Be(firstDto.CreatedAt);
        secondDto.AnalysedAt.Should().BeAfter(firstDto.AnalysedAt);
        secondDto.CareerLevel.Should().Be("lead");
        secondDto.YearsOfExperience.Should().Be(12);
        secondDto.ProgrammingLanguages.Should().BeEquivalentTo("Go");
    }

    [Fact]
    public async Task AnalyseAsync_ShouldPreservePreviousAnalysis_WhenForcedReanalysisFails()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        await using var db = CreateDbContext(databaseName);
        var resume = await SeedResumeAsync(db, contentText: "C# developer.");
        var client = new FakeOpenAIClient(ValidPayload);
        var service = CreateService(db, client);

        var first = await service.AnalyseAsync(resume.Id, resume.UserId, force: false);
        var firstDto = first.Value!;

        client.Exception = new HttpRequestException("provider down");

        // Act
        var second = await service.AnalyseAsync(resume.Id, resume.UserId, force: true);

        // Assert — the failed re-analysis must not damage the stored profile
        second.IsFailure.Should().BeTrue();
        second.Error!.Code.Should().Be("AI_UNAVAILABLE");

        await using var verifyDb = CreateDbContext(databaseName);
        var stored = await verifyDb.ResumeAnalysis.SingleAsync();
        stored.Id.Should().Be(firstDto.Id);
        stored.CreatedAt.Should().Be(firstDto.CreatedAt);
        stored.AnalysedAt.Should().Be(firstDto.AnalysedAt);
        stored.CareerLevel.Should().Be("senior");
        stored.Summary.Should().Be("Backend engineer focused on .NET services.");
    }

    [Fact]
    public async Task AnalyseAsync_ShouldPreservePreviousAnalysis_WhenForcedReanalysisReturnsUnparseableOutput()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        await using var db = CreateDbContext(databaseName);
        var resume = await SeedResumeAsync(db, contentText: "C# developer.");
        var client = new FakeOpenAIClient(ValidPayload);
        var service = CreateService(db, client);

        var first = await service.AnalyseAsync(resume.Id, resume.UserId, force: false);
        var firstDto = first.Value!;

        client.Response = "I'm sorry, I can't help with that.";

        // Act
        var second = await service.AnalyseAsync(resume.Id, resume.UserId, force: true);

        // Assert
        second.IsFailure.Should().BeTrue();
        second.Error!.Code.Should().Be("AI_PARSE_ERROR");

        await using var verifyDb = CreateDbContext(databaseName);
        var stored = await verifyDb.ResumeAnalysis.SingleAsync();
        stored.AnalysedAt.Should().Be(firstDto.AnalysedAt);
        stored.CareerLevel.Should().Be("senior");
        stored.Summary.Should().Be("Backend engineer focused on .NET services.");
    }

    // ---------- Analyse: rejections ----------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("    \n\t  ")]
    public async Task AnalyseAsync_ShouldReturnContentTextMissing_WhenNoUsableText(string? contentText)
    {
        // Arrange
        await using var db = CreateDbContext();
        var resume = await SeedResumeAsync(db, contentText);
        var client = new FakeOpenAIClient(ValidPayload);
        var service = CreateService(db, client);

        // Act
        var result = await service.AnalyseAsync(resume.Id, resume.UserId, force: false);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("CONTENT_TEXT_MISSING");
        client.CallCount.Should().Be(0);
        (await db.ResumeAnalysis.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task AnalyseAsync_ShouldReturnContentTextMissing_WhenForcedAndTextMissing()
    {
        // Arrange
        await using var db = CreateDbContext();
        var resume = await SeedResumeAsync(db, contentText: null);
        var client = new FakeOpenAIClient(ValidPayload);
        var service = CreateService(db, client);

        // Act
        var result = await service.AnalyseAsync(resume.Id, resume.UserId, force: true);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("CONTENT_TEXT_MISSING");
        client.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task AnalyseAsync_ShouldReturnResumeNotFound_WhenResumeDoesNotExist()
    {
        // Arrange
        await using var db = CreateDbContext();
        var client = new FakeOpenAIClient(ValidPayload);
        var service = CreateService(db, client);

        // Act
        var result = await service.AnalyseAsync(Guid.NewGuid(), Guid.NewGuid(), force: false);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("RESUME_NOT_FOUND");
        client.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task AnalyseAsync_ShouldReturnResumeNotFound_WhenResumeBelongsToAnotherUser()
    {
        // Arrange
        await using var db = CreateDbContext();
        var resume = await SeedResumeAsync(db, contentText: "C# developer.");
        var client = new FakeOpenAIClient(ValidPayload);
        var service = CreateService(db, client);

        // Act
        var result = await service.AnalyseAsync(resume.Id, Guid.NewGuid(), force: false);

        // Assert — ownership failures must not be distinguishable from absence
        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("RESUME_NOT_FOUND");
        client.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task AnalyseAsync_ShouldReturnResumeNotFound_WhenResumeIsSoftDeleted()
    {
        // Arrange
        await using var db = CreateDbContext();
        var resume = await SeedResumeAsync(db, contentText: "C# developer.");
        resume.Deactivate();
        await db.SaveChangesAsync();
        var client = new FakeOpenAIClient(ValidPayload);
        var service = CreateService(db, client);

        // Act
        var result = await service.AnalyseAsync(resume.Id, resume.UserId, force: false);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("RESUME_NOT_FOUND");
        client.CallCount.Should().Be(0);
    }

    // ---------- Analyse: provider and parse failures ----------

    [Fact]
    public async Task AnalyseAsync_ShouldReturnParseError_WhenProviderReturnsMalformedJson()
    {
        // Arrange
        await using var db = CreateDbContext();
        var resume = await SeedResumeAsync(db, contentText: "C# developer.");
        var service = CreateService(db, new FakeOpenAIClient("Sure! Here is the profile."));

        // Act
        var result = await service.AnalyseAsync(resume.Id, resume.UserId, force: false);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("AI_PARSE_ERROR");
        (await db.ResumeAnalysis.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task AnalyseAsync_ShouldReturnParseError_WhenProviderThrowsParseException()
    {
        // Arrange
        await using var db = CreateDbContext();
        var resume = await SeedResumeAsync(db, contentText: "C# developer.");
        var client = FakeOpenAIClient.Throwing(
            new OpenAIParseException("no content", "{}"));
        var service = CreateService(db, client);

        // Act
        var result = await service.AnalyseAsync(resume.Id, resume.UserId, force: false);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("AI_PARSE_ERROR");
        (await db.ResumeAnalysis.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task AnalyseAsync_ShouldReturnUnavailable_WhenProviderRequestFails()
    {
        // Arrange
        await using var db = CreateDbContext();
        var resume = await SeedResumeAsync(db, contentText: "C# developer.");
        var client = FakeOpenAIClient.Throwing(new HttpRequestException("connection refused"));
        var service = CreateService(db, client);

        // Act
        var result = await service.AnalyseAsync(resume.Id, resume.UserId, force: false);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("AI_UNAVAILABLE");
        (await db.ResumeAnalysis.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task AnalyseAsync_ShouldReturnUnavailable_WhenProviderTimesOut()
    {
        // Arrange — a TaskCanceledException while the caller's token is still live is a timeout
        await using var db = CreateDbContext();
        var resume = await SeedResumeAsync(db, contentText: "C# developer.");
        var client = FakeOpenAIClient.Throwing(new TaskCanceledException("timed out"));
        var service = CreateService(db, client);

        // Act
        var result = await service.AnalyseAsync(resume.Id, resume.UserId, force: false);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("AI_UNAVAILABLE");
    }

    [Fact]
    public async Task AnalyseAsync_ShouldPropagateCancellation_WhenCallerCancels()
    {
        // Arrange — a cancelled caller is not a provider outage
        await using var db = CreateDbContext();
        var resume = await SeedResumeAsync(db, contentText: "C# developer.");
        using var cts = new CancellationTokenSource();
        var client = FakeOpenAIClient.CancellingWith(cts);
        var service = CreateService(db, client);

        // Act
        var act = () => service.AnalyseAsync(resume.Id, resume.UserId, force: false, cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
        client.CallCount.Should().Be(1);
    }

    // ---------- Logging ----------

    [Fact]
    public async Task AnalyseAsync_ShouldNotLogResumeContent()
    {
        // Arrange
        await using var db = CreateDbContext();
        var resume = await SeedResumeAsync(db, contentText: "SECRET-RESUME-BODY-MARKER");
        var logger = new CapturingLogger<ResumeAnalysisService>();
        var service = CreateService(db, new FakeOpenAIClient(ValidPayload), logger);

        // Act
        await service.AnalyseAsync(resume.Id, resume.UserId, force: false);

        // Assert
        logger.Messages.Should().NotBeEmpty();
        logger.Messages.Should().NotContain(m => m.Contains("SECRET-RESUME-BODY-MARKER"));
        logger.Messages.Should().Contain(m => m.Contains(resume.Id.ToString()));
        logger.Messages.Should().Contain(m => m.Contains("Analysed"));
    }

    [Fact]
    public async Task AnalyseAsync_ShouldNotLogResumeContent_WhenProviderFails()
    {
        // Arrange
        await using var db = CreateDbContext();
        var resume = await SeedResumeAsync(db, contentText: "SECRET-RESUME-BODY-MARKER");
        var logger = new CapturingLogger<ResumeAnalysisService>();
        var client = FakeOpenAIClient.Throwing(new HttpRequestException("connection refused"));
        var service = CreateService(db, client, logger);

        // Act
        await service.AnalyseAsync(resume.Id, resume.UserId, force: false);

        // Assert
        logger.Messages.Should().NotBeEmpty();
        logger.Messages.Should().NotContain(m => m.Contains("SECRET-RESUME-BODY-MARKER"));
        logger.Messages.Should().Contain(m => m.Contains("Unavailable"));
    }

    // ---------- Get ----------

    [Fact]
    public async Task GetAnalysisAsync_ShouldReturnStoredAnalysis_AndNeverCallProvider()
    {
        // Arrange
        await using var db = CreateDbContext();
        var resume = await SeedResumeAsync(db, contentText: "C# developer.");
        var client = new FakeOpenAIClient(ValidPayload);
        var service = CreateService(db, client);
        await service.AnalyseAsync(resume.Id, resume.UserId, force: false);
        var callsAfterAnalyse = client.CallCount;

        // Act
        var result = await service.GetAnalysisAsync(resume.Id, resume.UserId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.CareerLevel.Should().Be("senior");
        client.CallCount.Should().Be(callsAfterAnalyse);
    }

    [Fact]
    public async Task GetAnalysisAsync_ShouldReturnAnalysisNotFound_WhenResumeHasNoAnalysis()
    {
        // Arrange
        await using var db = CreateDbContext();
        var resume = await SeedResumeAsync(db, contentText: "C# developer.");
        var service = CreateService(db, new FakeOpenAIClient(ValidPayload));

        // Act
        var result = await service.GetAnalysisAsync(resume.Id, resume.UserId);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ANALYSIS_NOT_FOUND");
    }

    [Fact]
    public async Task GetAnalysisAsync_ShouldReturnResumeNotFound_WhenResumeBelongsToAnotherUser()
    {
        // Arrange
        await using var db = CreateDbContext();
        var resume = await SeedResumeAsync(db, contentText: "C# developer.");
        var service = CreateService(db, new FakeOpenAIClient(ValidPayload));
        await service.AnalyseAsync(resume.Id, resume.UserId, force: false);

        // Act
        var result = await service.GetAnalysisAsync(resume.Id, Guid.NewGuid());

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("RESUME_NOT_FOUND");
    }

    [Fact]
    public async Task GetAnalysisAsync_ShouldReturnResumeNotFound_WhenResumeIsSoftDeleted()
    {
        // Arrange
        await using var db = CreateDbContext();
        var resume = await SeedResumeAsync(db, contentText: "C# developer.");
        var service = CreateService(db, new FakeOpenAIClient(ValidPayload));
        await service.AnalyseAsync(resume.Id, resume.UserId, force: false);
        resume.Deactivate();
        await db.SaveChangesAsync();

        // Act
        var result = await service.GetAnalysisAsync(resume.Id, resume.UserId);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("RESUME_NOT_FOUND");
    }

    // ---------- Helpers ----------

    private static AppDbContext CreateDbContext(string? databaseName = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName ?? Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    private static async Task<Resume> SeedResumeAsync(AppDbContext db, string? contentText)
    {
        var resume = new Resume(Guid.NewGuid(), "cv.pdf", "user/cv.pdf", contentText);
        db.Resumes.Add(resume);
        await db.SaveChangesAsync();
        return resume;
    }

    private ResumeAnalysisService CreateService(
        AppDbContext db,
        FakeOpenAIClient client,
        ILogger<ResumeAnalysisService>? logger = null)
    {
        return new ResumeAnalysisService(
            db,
            client,
            new PromptLoader(_promptsDirectory),
            logger ?? NullLogger<ResumeAnalysisService>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_promptsDirectory))
        {
            Directory.Delete(_promptsDirectory, recursive: true);
        }
    }

    private sealed class FakeOpenAIClient : IOpenAIClient
    {
        private readonly CancellationTokenSource? _cancelOnCall;

        public FakeOpenAIClient(string response)
        {
            Response = response;
        }

        private FakeOpenAIClient(string response, CancellationTokenSource cancelOnCall)
        {
            Response = response;
            _cancelOnCall = cancelOnCall;
        }

        public static FakeOpenAIClient Throwing(Exception exception)
        {
            return new FakeOpenAIClient(string.Empty) { Exception = exception };
        }

        /// <summary>
        /// Cancels the caller's token from inside the call, then throws as a real
        /// HttpClient does — the service must treat that as cancellation, not an outage.
        /// </summary>
        public static FakeOpenAIClient CancellingWith(CancellationTokenSource cts)
        {
            return new FakeOpenAIClient(string.Empty, cts)
            {
                Exception = new TaskCanceledException("cancelled")
            };
        }

        public string Response { get; set; }
        public Exception? Exception { get; set; }
        public int CallCount { get; private set; }
        public string? LastSystemPrompt { get; private set; }
        public string? LastUserPrompt { get; private set; }

        public Task<string> CompleteAsync(
            string systemPrompt,
            string userPrompt,
            CancellationToken ct = default)
        {
            CallCount++;
            LastSystemPrompt = systemPrompt;
            LastUserPrompt = userPrompt;

            _cancelOnCall?.Cancel();

            if (Exception is not null)
            {
                throw Exception;
            }

            return Task.FromResult(Response);
        }
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public IDisposable BeginScope<TState>(TState state) where TState : notnull
        {
            return NullScope.Instance;
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));

            if (exception is not null)
            {
                Messages.Add(exception.ToString());
            }
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();

            public void Dispose()
            {
            }
        }
    }
}

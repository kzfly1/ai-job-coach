using System.Text;
using AIJobCoach.Api.Data;
using AIJobCoach.Api.Modules.Resumes.Application;
using AIJobCoach.Api.Modules.Resumes.Domain;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AIJobCoach.Tests.Modules.Resumes.Application;

public sealed class ResumeServiceTests
{
    private const string PdfContentType = "application/pdf";
    private const string DocxContentType =
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    private static MemoryStream CreateFileStream()
    {
        return new MemoryStream(Encoding.UTF8.GetBytes("fake file content"));
    }

    [Fact]
    public async Task UploadAsync_ShouldReturnUnsupportedFileType_AndNotCallSave_WhenContentTypeIsInvalid()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var storage = new FakeFileStorageService();
        var service = new ResumeService(dbContext, storage);
        await using var stream = CreateFileStream();

        // Act
        var result = await service.UploadAsync(
            Guid.NewGuid(),
            stream,
            "image.png",
            "image/png",
            fileSizeBytes: 1024);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("UNSUPPORTED_FILE_TYPE");
        storage.SaveCallCount.Should().Be(0);
        (await dbContext.Resumes.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task UploadAsync_ShouldReturnFileTooLarge_AndNotCallSave_WhenFileExceedsMaxSize()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var storage = new FakeFileStorageService();
        var service = new ResumeService(dbContext, storage);
        await using var stream = CreateFileStream();

        // Act
        var result = await service.UploadAsync(
            Guid.NewGuid(),
            stream,
            "resume.pdf",
            PdfContentType,
            fileSizeBytes: (5 * 1024 * 1024) + 1);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("FILE_TOO_LARGE");
        storage.SaveCallCount.Should().Be(0);
        (await dbContext.Resumes.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task UploadAsync_ShouldPersistResumeWithNullContentText_AndCallSaveOnce_WhenPdfIsValid()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var storage = new FakeFileStorageService();
        var service = new ResumeService(dbContext, storage);
        var userId = Guid.NewGuid();
        await using var stream = CreateFileStream();

        // Act
        var result = await service.UploadAsync(
            userId,
            stream,
            "resume.pdf",
            PdfContentType,
            fileSizeBytes: 1024);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.FileName.Should().Be("resume.pdf");
        result.Value.UserId.Should().Be(userId);
        result.Value.ContentTextLength.Should().Be(0);
        result.Value.IsActive.Should().BeTrue();

        storage.SaveCallCount.Should().Be(1);

        var persisted = await dbContext.Resumes.SingleAsync();
        persisted.UserId.Should().Be(userId);
        persisted.StoragePath.Should().Be(storage.ReturnedStoragePath);
        persisted.ContentText.Should().BeNull();
    }

    [Fact]
    public async Task UploadAsync_ShouldReturnSuccess_WhenDocxIsValid()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var storage = new FakeFileStorageService();
        var service = new ResumeService(dbContext, storage);
        await using var stream = CreateFileStream();

        // Act
        var result = await service.UploadAsync(
            Guid.NewGuid(),
            stream,
            "resume.docx",
            DocxContentType,
            fileSizeBytes: 2048);

        // Assert
        result.IsSuccess.Should().BeTrue();
        storage.SaveCallCount.Should().Be(1);
        (await dbContext.Resumes.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnNull_WhenResumeBelongsToAnotherUser()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var service = new ResumeService(dbContext, new FakeFileStorageService());

        var ownerId = Guid.NewGuid();
        var resume = new Resume(ownerId, "resume.pdf", "path/resume.pdf", contentText: null);
        dbContext.Resumes.Add(resume);
        await dbContext.SaveChangesAsync();

        // Act
        var result = await service.GetByIdAsync(resume.Id, Guid.NewGuid());

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ListByUserAsync_ShouldReturnOnlyResumesBelongingToTheGivenUser()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var service = new ResumeService(dbContext, new FakeFileStorageService());

        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();

        dbContext.Resumes.Add(new Resume(userId, "a.pdf", "p/a.pdf", null));
        dbContext.Resumes.Add(new Resume(userId, "b.pdf", "p/b.pdf", null));
        dbContext.Resumes.Add(new Resume(otherUserId, "c.pdf", "p/c.pdf", null));
        await dbContext.SaveChangesAsync();

        // Act
        var result = await service.ListByUserAsync(userId);

        // Assert
        result.Should().HaveCount(2);
        result.Should().OnlyContain(r => r.UserId == userId);
    }

    private sealed class FakeFileStorageService : IFileStorageService
    {
        public int SaveCallCount { get; private set; }

        public string ReturnedStoragePath { get; } = $"{Guid.NewGuid()}/stored-file.pdf";

        public Task<string> SaveAsync(
            Stream fileStream,
            string fileName,
            string contentType,
            Guid userId,
            CancellationToken ct = default)
        {
            SaveCallCount++;
            return Task.FromResult(ReturnedStoragePath);
        }

        public Task DeleteAsync(string storagePath, CancellationToken ct = default)
        {
            return Task.CompletedTask;
        }
    }
}

using System.Text;
using AIJobCoach.Api.Modules.Resumes.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.Configuration;

namespace AIJobCoach.Tests.Modules.Resumes.Infrastructure;

public sealed class LocalFileStorageServiceTests : IDisposable
{
    private readonly string _tempRootPath;
    private readonly LocalFileStorageService _service;

    public LocalFileStorageServiceTests()
    {
        _tempRootPath = Path.Combine(Path.GetTempPath(), $"aijobcoach-tests-{Guid.NewGuid()}");

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["FileStorage:LocalPath"] = _tempRootPath
            })
            .Build();

        _service = new LocalFileStorageService(configuration);
    }

    [Fact]
    public async Task SaveAsync_ShouldSaveFileAndReturnStoragePath_WhenPdfIsValid()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var content = "fake pdf content";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));

        // Act
        var storagePath = await _service.SaveAsync(
            stream,
            "resume.pdf",
            "application/pdf",
            userId);

        // Assert
        storagePath.Should().NotBeNullOrWhiteSpace();
        storagePath.Should().Contain(userId.ToString());
        storagePath.Should().EndWith(".pdf");

        var fullPath = Path.Combine(_tempRootPath, storagePath);
        File.Exists(fullPath).Should().BeTrue();

        var savedContent = await File.ReadAllTextAsync(fullPath);
        savedContent.Should().Be(content);
    }

    [Fact]
    public async Task SaveAsync_ShouldSaveFileWithDocxExtension_WhenDocxIsValid()
    {
        // Arrange
        var userId = Guid.NewGuid();
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("fake docx content"));

        // Act
        var storagePath = await _service.SaveAsync(
            stream,
            "resume.docx",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            userId);

        // Assert
        storagePath.Should().Contain(userId.ToString());
        storagePath.Should().EndWith(".docx");

        var fullPath = Path.Combine(_tempRootPath, storagePath);
        File.Exists(fullPath).Should().BeTrue();
    }

    [Fact]
    public async Task SaveAsync_ShouldThrowArgumentException_WhenContentTypeIsUnsupported()
    {
        // Arrange
        var userId = Guid.NewGuid();
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("invalid file"));

        // Act
        var action = async () => await _service.SaveAsync(
            stream,
            "image.png",
            "image/png",
            userId);

        // Assert
        await action.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Unsupported file type*");
    }

    [Fact]
    public async Task DeleteAsync_ShouldDeleteFile_WhenFileExists()
    {
        // Arrange
        var userId = Guid.NewGuid();
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("fake pdf content"));

        var storagePath = await _service.SaveAsync(
            stream,
            "resume.pdf",
            "application/pdf",
            userId);

        var fullPath = Path.Combine(_tempRootPath, storagePath);
        File.Exists(fullPath).Should().BeTrue();

        // Act
        await _service.DeleteAsync(storagePath);

        // Assert
        File.Exists(fullPath).Should().BeFalse();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRootPath))
        {
            Directory.Delete(_tempRootPath, recursive: true);
        }
    }
}
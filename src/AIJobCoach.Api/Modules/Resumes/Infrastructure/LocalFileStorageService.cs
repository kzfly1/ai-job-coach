using AIJobCoach.Api.Modules.Resumes.Application;

namespace AIJobCoach.Api.Modules.Resumes.Infrastructure;

public sealed class LocalFileStorageService : IFileStorageService
{
    private readonly string _rootPath;

    public LocalFileStorageService(IConfiguration configuration)
    {
        _rootPath = configuration["FileStorage:LocalPath"] ?? "./uploads";
    }

    public async Task<string> SaveAsync(
        Stream fileStream,
        string fileName,
        string contentType,
        Guid userId,
        CancellationToken ct = default)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("User id is required.", nameof(userId));
        
        if (fileStream is null)
            throw new ArgumentNullException(nameof(fileStream));
        
        if (string.IsNullOrWhiteSpace(contentType))
        {
            throw new ArgumentException("Content type is required.", nameof(contentType));
        }

        var extension = GetExtensionFromContentType(contentType);
        var storedFileName = $"{Guid.NewGuid()}{extension}";
        var relativePath = Path.Combine(userId.ToString(), storedFileName);
        
        var fullPath = Path.Combine(_rootPath, relativePath);
        var directoryPath = Path.GetDirectoryName(fullPath);

        if (directoryPath is null)
            throw new InvalidOperationException("Could not determine the file directory.");
        
        Directory.CreateDirectory(directoryPath);

        await using var outputStream = File.Create(fullPath);
        await fileStream.CopyToAsync(outputStream, ct);

        return relativePath;
    }

    public Task DeleteAsync(
        string storagePath,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(storagePath))
        {
            throw new ArgumentException("Storage path is required.", nameof(storagePath));
        }

        var fullPath = Path.Combine(_rootPath, storagePath);

        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }
        
        return Task.CompletedTask;
    }
    
    private static string GetExtensionFromContentType(string contentType)
    {
        return contentType.ToLowerInvariant() switch
        {
            "application/pdf" => ".pdf",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document" => ".docx",
            _ => throw new ArgumentException("Unsupported file type.", nameof(contentType))
        };
    }
}
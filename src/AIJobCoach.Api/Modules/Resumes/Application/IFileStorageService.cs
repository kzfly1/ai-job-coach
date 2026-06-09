namespace AIJobCoach.Api.Modules.Resumes.Application;

public interface IFileStorageService
{
    Task<string> SaveAsync(
        Stream fileStream,
        string fileName,
        string contentType,
        Guid userId,
        CancellationToken ct = default);

    Task DeleteAsync(
        string storagePath,
        CancellationToken ct = default);
}
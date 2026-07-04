namespace AIJobCoach.Api.Modules.Resumes.Application;

public interface IResumeTextExtractor
{
    Task<string?> ExtractAsync(
        Stream stream,
        string mimeType,
        CancellationToken cancellationToken = default);
}
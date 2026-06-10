namespace AIJobCoach.Api.Modules.Resumes.Application;

public sealed record ResumeDto(
    Guid Id,
    Guid UserId,
    string FileName,
    int ContentTextLength,
    bool IsActive,
    DateTimeOffset UploadedAt);

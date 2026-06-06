namespace AIJobCoach.Api.Modules.Resumes.Domain;

public class Resume
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string StoragePath { get; set; } = string.Empty;
    public string? ContentText { get; set; }
    public bool IsActive { get; set; }
    public DateTimeOffset UploadedAt { get; set; }
}
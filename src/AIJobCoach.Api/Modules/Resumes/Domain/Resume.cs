namespace AIJobCoach.Api.Modules.Resumes.Domain;

public sealed class Resume
{
    private Resume()
    {
    }

    public Resume(
        Guid userId,
        string fileName,
        string storagePath,
        string? contentText)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User id is required.", nameof(userId));
        }
        
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentException("File name is required.", nameof(fileName));
        }
        
        if (string.IsNullOrWhiteSpace(storagePath))
        {
            throw new ArgumentException("Storage path is required.", nameof(storagePath));
        }
        
        Id = Guid.NewGuid();
        UserId = userId;
        FileName = fileName;
        StoragePath = storagePath;
        ContentText = contentText;
        IsActive = true;
        UploadedAt = DateTimeOffset.UtcNow;
    }
    public Guid Id { get; private set; }
    
    public Guid UserId { get; private set; }
    
    public string FileName { get; private set; } = string.Empty;
    
    public string StoragePath { get; private set; } = string.Empty;
    
    public string? ContentText { get; private set; }
    
    public bool IsActive { get; private set; }
    
    public DateTimeOffset UploadedAt { get; private set; }

    public void Deactivate()
    {
        if (!IsActive)
            return;
        IsActive = false;
    }
}
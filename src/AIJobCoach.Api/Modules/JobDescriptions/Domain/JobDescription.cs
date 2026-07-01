namespace AIJobCoach.Api.Modules.JobDescriptions.Domain;

public sealed class JobDescription
{
    private JobDescription()
    {
        
    }

    public JobDescription(
        Guid userId,
        string title,
        string? company,
        string rawText)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User id is required.", nameof(userId));
        }

        if (string.IsNullOrEmpty(title))
        {
            throw new ArgumentException("Title is required.", nameof(title));
        }

        if (string.IsNullOrEmpty(rawText))
        {
            throw new ArgumentException("Raw text is required.", nameof(rawText));
        }
        
        Id = Guid.NewGuid();
        UserId = userId;
        Title = title.Trim();
        Company = string.IsNullOrEmpty(company) ? null : company.Trim();
        RawText = rawText.Trim();
        Requirements = null;
        CreatedAt = DateTimeOffset.UtcNow;
    }
    
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }

    public string Title { get; private set; } = string.Empty;
    public string? Company { get; private set; }
    public string RawText { get; private set; } = string.Empty;
    
    public string? Requirements { get; private set; }
    
    public DateTimeOffset CreatedAt { get; private set; }
}
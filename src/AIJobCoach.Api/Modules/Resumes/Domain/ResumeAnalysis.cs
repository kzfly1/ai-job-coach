namespace AIJobCoach.Api.Modules.Resumes.Domain;

public sealed class ResumeAnalysis
{
    private ResumeAnalysis()
    {
        
    }

    public ResumeAnalysis(
        Guid resumeId,
        string? summary,
        int? yearsOfExperience,
        string? careerLevel,
        string programmingLanguages,
        string frameworks,
        string cloudPlatforms,
        string databases,
        string tools,
        string projects,
        string? rawResponse)
    {
        if (resumeId == Guid.Empty)
        {
            throw new ArgumentException("Resume id is required", nameof(resumeId));
        }

        Id = Guid.NewGuid();
        ResumeId = resumeId;
        Summary = summary;
        YearsOfExperience = yearsOfExperience;
        CareerLevel = careerLevel;
        ProgrammingLanguages = string.IsNullOrWhiteSpace(programmingLanguages) ? "[]" : programmingLanguages;
        Frameworks = string.IsNullOrWhiteSpace(frameworks) ? "[]" : frameworks;
        CloudPlatforms = string.IsNullOrWhiteSpace(cloudPlatforms) ? "[]" : cloudPlatforms;
        Databases = string.IsNullOrWhiteSpace(databases) ? "[]" : databases;
        Tools = string.IsNullOrWhiteSpace(tools) ? "[]" : tools;
        Projects = string.IsNullOrWhiteSpace(projects) ? "[]" : projects;
        RawResponse = rawResponse;
        AnalysedAt = DateTimeOffset.UtcNow;
        CreatedAt = DateTimeOffset.UtcNow;
    }
    
    public Guid Id { get; private set; }
    public Guid ResumeId { get; private set; }
    
    public string? Summary { get; private set; }
    public int? YearsOfExperience { get; private set; }
    public string? CareerLevel { get; private set; }

    public string ProgrammingLanguages { get; private set; } = "[]";
    public string Frameworks { get; private set; } = "[]";
    public string CloudPlatforms { get; private set; } = "[]";
    public string Databases { get; private set; } = "[]";
    public string Tools { get; private set; } = "[]";
    public string Projects { get; private set; } = "[]";
    
    public string? RawResponse { get; private set; }
    
    public DateTimeOffset AnalysedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    
    public Resume? Resume { get; private set; }
}
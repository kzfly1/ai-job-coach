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
        CreatedAt = DateTimeOffset.UtcNow;

        Update(
            summary,
            yearsOfExperience,
            careerLevel,
            programmingLanguages,
            frameworks,
            cloudPlatforms,
            databases,
            tools,
            projects,
            rawResponse);
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

    /// <summary>
    /// Replaces the profile fields from a fresh analysis and stamps <see cref="AnalysedAt"/>.
    /// Identity and <see cref="CreatedAt"/> are preserved so a re-analysis updates the
    /// existing row rather than replacing it.
    /// </summary>
    public void Update(
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
        Summary = summary;
        YearsOfExperience = yearsOfExperience;
        CareerLevel = careerLevel;
        ProgrammingLanguages = NormaliseCollection(programmingLanguages);
        Frameworks = NormaliseCollection(frameworks);
        CloudPlatforms = NormaliseCollection(cloudPlatforms);
        Databases = NormaliseCollection(databases);
        Tools = NormaliseCollection(tools);
        Projects = NormaliseCollection(projects);
        RawResponse = rawResponse;
        AnalysedAt = DateTimeOffset.UtcNow;
    }

    private static string NormaliseCollection(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "[]" : value;
    }
}
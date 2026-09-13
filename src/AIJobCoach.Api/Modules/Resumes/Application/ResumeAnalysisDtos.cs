namespace AIJobCoach.Api.Modules.Resumes.Application;

/// <summary>A project extracted from a resume, as returned to clients.</summary>
public sealed record ResumeAnalysisProjectDto(
    string Name,
    string? Description,
    string[] Technologies);

/// <summary>
/// The Developer Profile as returned to clients. Collections are typed arrays here,
/// although they are persisted as JSON strings in jsonb columns. RawResponse is
/// deliberately absent: it is unbounded diagnostic data with no client use.
/// </summary>
public sealed record ResumeAnalysisDto(
    Guid Id,
    Guid ResumeId,
    string? Summary,
    int? YearsOfExperience,
    string? CareerLevel,
    string[] ProgrammingLanguages,
    string[] Frameworks,
    string[] CloudPlatforms,
    string[] Databases,
    string[] Tools,
    ResumeAnalysisProjectDto[] Projects,
    DateTimeOffset AnalysedAt,
    DateTimeOffset CreatedAt);

/// <summary>
/// The shape the resume-analysis prompt asks the model to return. Deserialised
/// case-insensitively, which is how the prompt's camelCase "careerSummary" reaches
/// the entity's Summary. Every member is nullable because the model's output is
/// untrusted input, not a guarantee.
/// </summary>
internal sealed record ResumeAnalysisPayload(
    string? CareerLevel,
    int? YearsOfExperience,
    string? CareerSummary,
    string[]? ProgrammingLanguages,
    string[]? Frameworks,
    string[]? CloudPlatforms,
    string[]? Databases,
    string[]? Tools,
    ResumeAnalysisPayloadProject[]? Projects);

internal sealed record ResumeAnalysisPayloadProject(
    string? Name,
    string? Description,
    string[]? Technologies);

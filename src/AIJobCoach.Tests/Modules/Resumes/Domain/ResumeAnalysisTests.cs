using AIJobCoach.Api.Modules.Resumes.Domain;
using FluentAssertions;

namespace AIJobCoach.Tests.Modules.Resumes.Domain;

public sealed class ResumeAnalysisTests
{
    [Fact]
    public void Constructor_WithValidValues_CreatesResumeAnalysis()
    {
        var resumeId = Guid.NewGuid();
        
        var analysis = new ResumeAnalysis(
            resumeId,
            "Backend-focused developer",
            2,
            "junior",
            """["C#","TypeScript"]""",
            """["ASP.NET Core","Next.js"]""",
            """["AWS"]""",
            """["PostgreSQL"]""",
            """["Docker"]""",
            """[]""",
            "{}");
        
        analysis.ResumeId.Should().Be(resumeId);
        analysis.Summary.Should().Be("Backend-focused developer");
        analysis.ProgrammingLanguages.Should().Contain("C#");
        analysis.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }
    
    [Fact]
    public void Constructor_WithEmptyResumeId_ThrowsArgumentException()
    {
        var act = () => new ResumeAnalysis(
            Guid.Empty,
            null,
            null,
            null,
            "[]",
            "[]",
            "[]",
            "[]",
            "[]",
            "[]",
            null);

        act.Should().Throw<ArgumentException>()
            .WithParameterName("resumeId");
    }
}
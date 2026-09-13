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

    [Fact]
    public async Task Update_ShouldReplaceProfileFields_AndPreserveIdentityAndCreatedAt()
    {
        // Arrange
        var analysis = new ResumeAnalysis(
            Guid.NewGuid(),
            "Original summary",
            2,
            "junior",
            """["C#"]""",
            """["ASP.NET Core"]""",
            """["AWS"]""",
            """["PostgreSQL"]""",
            """["Docker"]""",
            """[]""",
            "{}");

        var originalId = analysis.Id;
        var originalResumeId = analysis.ResumeId;
        var originalCreatedAt = analysis.CreatedAt;
        var originalAnalysedAt = analysis.AnalysedAt;

        await Task.Delay(10);

        // Act
        analysis.Update(
            "Updated summary",
            7,
            "senior",
            """["C#","Go"]""",
            """["Next.js"]""",
            """["Azure"]""",
            """["Redis"]""",
            """["Terraform"]""",
            """[{"name":"Coach","description":"App","technologies":["C#"]}]""",
            """{"ok":true}""");

        // Assert
        analysis.Id.Should().Be(originalId);
        analysis.ResumeId.Should().Be(originalResumeId);
        analysis.CreatedAt.Should().Be(originalCreatedAt);
        analysis.AnalysedAt.Should().BeAfter(originalAnalysedAt);

        analysis.Summary.Should().Be("Updated summary");
        analysis.YearsOfExperience.Should().Be(7);
        analysis.CareerLevel.Should().Be("senior");
        analysis.ProgrammingLanguages.Should().Be("""["C#","Go"]""");
        analysis.Frameworks.Should().Be("""["Next.js"]""");
        analysis.CloudPlatforms.Should().Be("""["Azure"]""");
        analysis.Databases.Should().Be("""["Redis"]""");
        analysis.Tools.Should().Be("""["Terraform"]""");
        analysis.Projects.Should().Contain("Coach");
        analysis.RawResponse.Should().Be("""{"ok":true}""");
    }

    [Fact]
    public void Update_ShouldNormaliseBlankCollections_ToEmptyJsonArray()
    {
        // Arrange
        var analysis = new ResumeAnalysis(
            Guid.NewGuid(),
            null, null, null,
            """["C#"]""", """["ASP.NET Core"]""", """["AWS"]""",
            """["PostgreSQL"]""", """["Docker"]""", """[]""",
            null);

        // Act
        analysis.Update(
            null, null, null,
            "", "   ", "", "", "", "",
            null);

        // Assert
        analysis.ProgrammingLanguages.Should().Be("[]");
        analysis.Frameworks.Should().Be("[]");
        analysis.CloudPlatforms.Should().Be("[]");
        analysis.Databases.Should().Be("[]");
        analysis.Tools.Should().Be("[]");
        analysis.Projects.Should().Be("[]");
    }
}

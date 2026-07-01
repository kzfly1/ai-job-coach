using AIJobCoach.Api.Modules.JobDescriptions.Domain;
using FluentAssertions;

namespace AIJobCoach.Tests.Modules.JobDescriptions.Domain;

public sealed class JobDescriptionTests
{
    [Fact]
    public void Constructor_WithValidValues_CreatesJobDescription()
    {
        var userId = Guid.NewGuid();

        var jobDescription = new JobDescription(
            userId,
            "Backend Developer",
            "Canva",
            "We are looking for a backend developer with C#, PostgreSQL, and cloud experience.");

        jobDescription.UserId.Should().Be(userId);
        jobDescription.Title.Should().Be("Backend Developer");
        jobDescription.Company.Should().Be("Canva");
        jobDescription.RawText.Should().Contain("backend developer");
        jobDescription.Requirements.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithEmptyUserId_ThrowsArgumentException()
    {
        var act = () => new JobDescription(
            Guid.Empty,
            "Backend Developer",
            "Canva",
            "Some raw job description text.");

        act.Should().Throw<ArgumentException>()
            .WithParameterName("userId");
    }

    [Fact]
    public void Constructor_WithEmptyTitle_ThrowsArgumentException()
    {
        var act = () => new JobDescription(
            Guid.NewGuid(),
            "",
            "Canva",
            "Some raw job description text.");

        act.Should().Throw<ArgumentException>()
            .WithParameterName("title");
    }

    [Fact]
    public void Constructor_WithEmptyRawText_ThrowsArgumentException()
    {
        var act = () => new JobDescription(
            Guid.NewGuid(),
            "Backend Developer",
            "Canva",
            "");

        act.Should().Throw<ArgumentException>()
            .WithParameterName("rawText");
    }
}
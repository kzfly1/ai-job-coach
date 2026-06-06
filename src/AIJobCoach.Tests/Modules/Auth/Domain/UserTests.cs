using AIJobCoach.Api.Modules.Auth.Domain;
using FluentAssertions;

namespace AIJobCoach.Tests.Modules.Auth.Domain;

public class UserTests
{
    [Fact]
    public void Constructor_ShouldSetProperties()
    {
        var user = new User(
            email: "test@example.com",
            passwordHash: "hashed-password",
            fullName: "Test User",
            headline: "Junior Developer");

        user.Id.Should().NotBe(Guid.Empty);
        user.Email.Should().Be("test@example.com");
        user.PasswordHash.Should().Be("hashed-password");
        user.FullName.Should().Be("Test User");
        user.Headline.Should().Be("Junior Developer");
        user.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
        user.UpdatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Constructor_WhenHeadlineIsNull_ShouldAllowNullHeadline()
    {
        var user = new User(
            email: "test@example.com",
            passwordHash: "hashed-password",
            fullName: "Test User");
        
        user.Headline.Should().BeNull();
    }
}
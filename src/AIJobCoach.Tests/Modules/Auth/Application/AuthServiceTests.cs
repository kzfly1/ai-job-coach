using AIJobCoach.Api.Data;
using AIJobCoach.Api.Modules.Auth.Application;
using AIJobCoach.Api.Modules.Auth.Domain;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace AIJobCoach.Tests.Modules.Auth.Application;

public sealed class AuthServiceTests
{
    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    private static JwtService CreateJwtService()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = "test-secret-key-must-be-at-least-32-chars",
                ["Jwt:Issuer"] = "AIJobCoach.Tests",
                ["Jwt:Audience"] = "AIJobCoach.Tests",
            })
            .Build();

        return new JwtService(configuration);
    }

    private static AuthService CreateAuthService(AppDbContext dbContext)
    {
        return new AuthService(dbContext, CreateJwtService());
    }

    [Fact]
    public async Task RegisterAsync_ShouldSaveHashedPasswordAndReturnTokenAndUser_WhenRequestIsValid()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var service = CreateAuthService(dbContext);

        var request = new RegisterRequest(
            Email: "test@example.com",
            Password: "Password123",
            FullName: "Test User",
            Headline: "Junior Developer");

        // Act
        var result = await service.RegisterAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();

        var value = result.Value
            ?? throw new InvalidOperationException("Expected successful auth result.");

        value.Token.Should().NotBeNullOrWhiteSpace();
        value.User.Id.Should().NotBeEmpty();
        value.User.Email.Should().Be("test@example.com");
        value.User.FullName.Should().Be("Test User");
        value.User.Headline.Should().Be("Junior Developer");

        var user = await dbContext.Users.SingleAsync();

        user.Email.Should().Be("test@example.com");
        user.PasswordHash.Should().NotBe("Password123");
        BCrypt.Net.BCrypt.Verify("Password123", user.PasswordHash).Should().BeTrue();
    }

    [Fact]
    public async Task LoginAsync_ShouldReturnTokenAndUser_WhenCredentialsAreValid()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var service = CreateAuthService(dbContext);

        var user = new User(
            email: "test@example.com",
            passwordHash: BCrypt.Net.BCrypt.HashPassword("Password123"),
            fullName: "Test User");

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var request = new LoginRequest(
            Email: "test@example.com",
            Password: "Password123");

        // Act
        var result = await service.LoginAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();

        var value = result.Value
            ?? throw new InvalidOperationException("Expected successful auth result.");

        value.Token.Should().NotBeNullOrWhiteSpace();
        value.User.Id.Should().Be(user.Id);
        value.User.Email.Should().Be("test@example.com");
        value.User.FullName.Should().Be("Test User");
        value.User.Headline.Should().BeNull();
    }

    [Fact]
    public async Task LoginAsync_ShouldReturnInvalidCredentials_WhenEmailDoesNotExist()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var service = CreateAuthService(dbContext);

        var request = new LoginRequest(
            Email: "missing@example.com",
            Password: "Password123");

        // Act
        var result = await service.LoginAsync(request);

        // Assert
        result.IsFailure.Should().BeTrue();

        var error = result.Error
            ?? throw new InvalidOperationException("Expected error result.");

        error.Code.Should().Be("INVALID_CREDENTIALS");
        error.Message.Should().Be("Invalid email or password.");
    }

    [Fact]
    public async Task LoginAsync_ShouldReturnInvalidCredentials_WhenPasswordIsWrong()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var service = CreateAuthService(dbContext);

        var user = new User(
            email: "test@example.com",
            passwordHash: BCrypt.Net.BCrypt.HashPassword("Password123", workFactor: 12),
            fullName: "Test User");

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var request = new LoginRequest(
            Email: "test@example.com",
            Password: "WrongPassword");

        // Act
        var result = await service.LoginAsync(request);

        // Assert
        result.IsFailure.Should().BeTrue();

        var error = result.Error
            ?? throw new InvalidOperationException("Expected error result.");

        error.Code.Should().Be("INVALID_CREDENTIALS");
        error.Message.Should().Be("Invalid email or password.");
    }
}
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
    public async Task RegisterAsync_ShouldSaveHashedPassword_WhenRequestIsValid()
    {
        //Arrange
        await using var dbContext = CreateDbContext();
        var service  = CreateAuthService(dbContext);

        var request = new RegisterRequest(
            Email: "test@example.com",
            Password: "Password123",
            FullName: "Test User",
            Headline: "Junior Developer");
        
        //Act 
        var result = await service.RegisterAsync(request);
        
        //Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();

        var value = result.Value!;
        
        value.Token.Should().NotBeNullOrWhiteSpace();

        var user = await dbContext.Users.SingleAsync();

        user.Email.Should().Be("test@example.com");
        user.PasswordHash.Should().NotBe("Password123");
        BCrypt.Net.BCrypt.Verify("Password123", user.PasswordHash).Should().BeTrue();
    }

    [Fact]
    public async Task LoginAsync_ShouldReturnToken_WhenCredentialsAreValid()
    {
        //Arrange
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
        
        //Act
        var result = await service.LoginAsync(request);
        
        //Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Token.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task LoginAsync_ShouldReturnInvalidCredentials_WhenEmailDoesNotExit()
    {
        //Arrange
        await using var dbContext = CreateDbContext();
        var service = CreateAuthService(dbContext);
        
        var request = new LoginRequest(
            Email: "missing@example.com",
            Password: "Password123");
        
        //Act
        var result = await service.LoginAsync(request);
        
        //Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().NotBeNull();
        result.Error!.code.Should().Be("INVALID_CREDENTIALS");
        result.Error.Message.Should().Be("Invalid email or password.");
    }

    [Fact]
    public async Task LoginAsync_ShouldReturnInvalidCredentials_WhenPasswordIsWrong()
    {
        //Arrange
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
        
        //Act
        var result = await service.LoginAsync(request);
        
        //Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().NotBeNull();
        result.Error!.code.Should().Be("INVALID_CREDENTIALS");
        result.Error.Message.Should().Be("Invalid email or password.");
    }
}
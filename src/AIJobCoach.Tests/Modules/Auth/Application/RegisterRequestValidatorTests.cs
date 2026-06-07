using AIJobCoach.Api.Modules.Auth.Application;
using FluentAssertions;

namespace AIJobCoach.Tests.Modules.Auth.Application;

public sealed class RegisterRequestValidatorTests
{
    private readonly RegisterRequestValidator _validator = new();

    [Fact]
    public void Validate_ShouldPass_WhenRequestIsValid()
    {
        //Arrange
        var request = new RegisterRequest(
            Email: "test@example.com",
            Password: "Password123",
            FullName: "Test User",
            Headline: "Junior Developer");
        
        //Act
        var result = _validator.Validate(request);
        
        //Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_ShouldFail_WhenEmailIsInvalid()
    {
        //Arrange
        var request = new RegisterRequest(
            Email: "invalid-email",
            Password: "Password123",
            FullName: "Test User",
            Headline: null);
        
        //Act
        var result = _validator.Validate(request);
        
        //Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(RegisterRequest.Email));
    }

    [Fact]
    public void Validate_ShouldFail_WhenPasswordIsTooShort()
    {
        //Arrange
        var request = new RegisterRequest(
            Email: "test@example.com",
            Password: "short",
            FullName: "Test User",
            Headline: null);
        
        //Act
        var result = _validator.Validate(request);
        
        //Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(RegisterRequest.Password));
    }

    [Fact]
    public void Validate_ShouldFail_WhenFullnameIsEmpty()
    {
        //Arrange
        var request = new RegisterRequest(
            Email: "test@example.com",
            Password: "Password123",
            FullName: "",
            Headline: null);
        
        //Act
        var result = _validator.Validate(request);
        
        //Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(RegisterRequest.FullName));
    }
}
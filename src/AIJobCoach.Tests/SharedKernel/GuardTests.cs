using AIJobCoach.SharedKernel;
using FluentAssertions;

namespace AIJobCoach.Tests.SharedKernel;

public class GuardTests
{
    [Fact]
    public void AgainstNull_ShouldThrow_WhenValueIsNull()
    {
        string? value = null;
        
        var act = () => Guard.AgainstNull(value, nameof(value));

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AgainstNull_ShouldReturnValue_WhenValueIsNotNull()
    {
        var value = "test";

        var result = Guard.AgainstNull(value, nameof(value));

        result.Should().Be(value);
    }

    [Fact]
    public void AgainstNullOrEmpty_ShouldThrow_WhenValueIsNull()
    {
        string? value = null;
        
        var act = () => Guard.AgainstNullOrEmpty(value, nameof(value));

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AgainstNullOrEmpty_ShouldThrow_WhenValueIsEmpty()
    {
        var value = "";
        
        var act = () => Guard.AgainstNullOrEmpty(value, nameof(value));
        
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AgainstNullOrEmpty_ShouldThrow_WhenValueIsWhiteSpace()
    {
        var value = " ";
        
        var act = () => Guard.AgainstNullOrEmpty(value, nameof(value));
        
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AgainstNullOrEmpty_ShouldReturnValue_WhenValueIsvalid()
    {
        var value = "test";
        
        var result = Guard.AgainstNullOrEmpty(value, nameof(value));
        
        result.Should().Be(value);
    }
}
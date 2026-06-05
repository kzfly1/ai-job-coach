using AIJobCoach.SharedKernel;
using FluentAssertions;

namespace AIJobCoach.Tests.SharedKernel;

public class ResultTests
{
    [Fact]
    public void Success_ShouldCreateSuccessfulResult()
    {
        var result = Result<string>.Success("ok");
        
        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
        result.Value.Should().Be("ok");
        result.Error.Should().BeNull();
    }

    [Fact]
    public void Failure_ShouldCreateFailureResult()
    {
        var error = new Error("TEST_ERROR", "Something failed");

        var result = Result<string>.Failure(error);
        result.IsSuccess.Should().BeFalse();
        result.IsFailure.Should().BeTrue();
        result.Value.Should().BeNull();
        result.Error.Should().Be(error);
    }
}
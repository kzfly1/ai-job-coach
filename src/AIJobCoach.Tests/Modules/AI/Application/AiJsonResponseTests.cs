using AIJobCoach.Api.Modules.AI.Application;
using FluentAssertions;

namespace AIJobCoach.Tests.Modules.AI.Application;

public sealed class AiJsonResponseTests
{
    [Fact]
    public void Unwrap_ShouldReturnJsonUnchanged_WhenNotFenced()
    {
        var raw = """{"careerLevel":"senior"}""";

        AiJsonResponse.Unwrap(raw).Should().Be("""{"careerLevel":"senior"}""");
    }

    [Fact]
    public void Unwrap_ShouldStripFence_WhenTaggedAsJson()
    {
        var raw = "```json\n{\"careerLevel\":\"senior\"}\n```";

        AiJsonResponse.Unwrap(raw).Should().Be("""{"careerLevel":"senior"}""");
    }

    [Fact]
    public void Unwrap_ShouldStripFence_WhenUntagged()
    {
        var raw = "```\n{\"careerLevel\":\"mid\"}\n```";

        AiJsonResponse.Unwrap(raw).Should().Be("""{"careerLevel":"mid"}""");
    }

    [Fact]
    public void Unwrap_ShouldStripFence_WhenSurroundedByWhitespace()
    {
        var raw = "\n\n  ```json\n{\"careerLevel\":\"lead\"}\n```  \n";

        AiJsonResponse.Unwrap(raw).Should().Be("""{"careerLevel":"lead"}""");
    }

    [Fact]
    public void Unwrap_ShouldNotExtractJson_WhenWrappedInProse()
    {
        var raw = "Here is the profile you asked for: {\"careerLevel\":\"senior\"} Hope that helps.";

        AiJsonResponse.Unwrap(raw).Should().Be(raw);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   \n  ")]
    public void Unwrap_ShouldReturnEmpty_WhenInputIsBlank(string? raw)
    {
        AiJsonResponse.Unwrap(raw).Should().BeEmpty();
    }
}

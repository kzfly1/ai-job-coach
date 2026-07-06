using System.Collections.Generic;
using AIJobCoach.Api.Modules.AI.Application;
using FluentAssertions;

namespace AIJobCoach.Tests.Modules.AI.Application;

public sealed class PromptLoaderTests : IDisposable
{
    private readonly string _directory;

    public PromptLoaderTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "prompt-loader-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(_directory);
    }

    [Fact]
    public void Load_ShouldReturnTemplate_WhenPromptFileExists()
    {
        // Arrange
        WritePrompt("greeting", "Hello {{name}}.");
        var loader = new PromptLoader(_directory);

        // Act
        var template = loader.Load("greeting");

        // Assert
        template.Should().Be("Hello {{name}}.");
    }

    [Fact]
    public void Load_ShouldThrow_WhenPromptFileMissing()
    {
        // Arrange
        var loader = new PromptLoader(_directory);

        // Act
        var act = () => loader.Load("does-not-exist");

        // Assert
        act.Should().Throw<FileNotFoundException>();
    }

    [Fact]
    public void Render_ShouldSubstitutePlaceholders()
    {
        // Arrange
        WritePrompt("greeting", "Hello {{name}}, welcome to {{place}}.");
        var loader = new PromptLoader(_directory);

        // Act
        var result = loader.Render("greeting", new Dictionary<string, string>
        {
            ["name"] = "Sam",
            ["place"] = "AI Job Coach"
        });

        // Assert
        result.Should().Be("Hello Sam, welcome to AI Job Coach.");
    }

    [Fact]
    public void Render_ShouldThrow_WhenPlaceholderValueMissing()
    {
        // Arrange
        WritePrompt("greeting", "Hello {{name}}.");
        var loader = new PromptLoader(_directory);

        // Act
        var act = () => loader.Render("greeting", new Dictionary<string, string>());

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*name*");
    }

    private void WritePrompt(string name, string content)
    {
        File.WriteAllText(Path.Combine(_directory, name + ".txt"), content);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}

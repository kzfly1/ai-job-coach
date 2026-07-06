using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace AIJobCoach.Api.Modules.AI.Application;

/// <summary>
/// Loads prompt templates from plain text files and caches them. Templates use
/// {{placeholder}} tokens which <see cref="Render"/> substitutes. Prompts live as
/// .txt files so they are readable, diffable, and editable without touching C#.
/// </summary>
public sealed class PromptLoader
{
    private static readonly Regex PlaceholderPattern =
        new(@"\{\{\s*(?<key>[A-Za-z0-9_]+)\s*\}\}", RegexOptions.Compiled);

    private readonly string _promptsDirectory;
    private readonly ConcurrentDictionary<string, string> _cache = new();

    public PromptLoader(string promptsDirectory)
    {
        _promptsDirectory = promptsDirectory;
    }

    /// <summary>
    /// Reads the raw template for the given prompt name (without extension), caching the result.
    /// </summary>
    /// <exception cref="FileNotFoundException">Thrown when the prompt file does not exist.</exception>
    public string Load(string promptName)
    {
        return _cache.GetOrAdd(promptName, name =>
        {
            var path = Path.Combine(_promptsDirectory, name + ".txt");
            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"Prompt file not found: {name}.txt", path);
            }

            return File.ReadAllText(path);
        });
    }

    /// <summary>
    /// Loads a prompt template and substitutes every {{placeholder}} with the matching value.
    /// </summary>
    /// <exception cref="FileNotFoundException">Thrown when the prompt file does not exist.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the template contains a placeholder with no corresponding value.
    /// </exception>
    public string Render(string promptName, IReadOnlyDictionary<string, string> values)
    {
        var template = Load(promptName);

        return PlaceholderPattern.Replace(template, match =>
        {
            var key = match.Groups["key"].Value;
            if (!values.TryGetValue(key, out var value))
            {
                throw new InvalidOperationException(
                    $"Missing value for placeholder '{key}' in prompt '{promptName}'.");
            }

            return value;
        });
    }
}

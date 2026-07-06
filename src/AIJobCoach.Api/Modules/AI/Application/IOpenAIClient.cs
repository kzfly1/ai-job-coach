namespace AIJobCoach.Api.Modules.AI.Application;

/// <summary>
/// Low-level transport client for OpenAI chat completions. Consuming services
/// (resume analysis, JD analysis, matching) build their prompts, call this
/// client, and parse the returned content string into their own typed shapes.
/// </summary>
public interface IOpenAIClient
{
    /// <summary>
    /// Sends a system + user prompt to the OpenAI chat completions endpoint and
    /// returns the raw assistant message content.
    /// </summary>
    /// <exception cref="OpenAIParseException">
    /// Thrown when the OpenAI response envelope cannot be parsed or contains no content.
    /// </exception>
    Task<string> CompleteAsync(
        string systemPrompt,
        string userPrompt,
        CancellationToken ct = default);
}

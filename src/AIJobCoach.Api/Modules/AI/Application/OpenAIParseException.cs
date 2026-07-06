namespace AIJobCoach.Api.Modules.AI.Application;

/// <summary>
/// Thrown when the OpenAI response cannot be parsed into a usable completion,
/// or when the response envelope contains no assistant content. The raw response
/// body is attached to aid debugging. Consuming services translate this into an
/// explicit Result error code (for example, AI_PARSE_ERROR) at their layer.
/// </summary>
public sealed class OpenAIParseException : Exception
{
    public OpenAIParseException(string message, string? rawResponse = null)
        : base(message)
    {
        RawResponse = rawResponse;
    }

    public OpenAIParseException(string message, string? rawResponse, Exception innerException)
        : base(message, innerException)
    {
        RawResponse = rawResponse;
    }

    /// <summary>The raw OpenAI response body, if one was received.</summary>
    public string? RawResponse { get; }
}

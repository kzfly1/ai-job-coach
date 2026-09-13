namespace AIJobCoach.Api.Modules.AI.Application;

/// <summary>
/// Normalises raw provider output into text that can be handed to a JSON deserialiser.
/// It strips a single markdown code fence — which models emit even when told not to —
/// and does nothing else. It deliberately does not search prose for a JSON object:
/// output that is not JSON is a provider failure, and the calling service surfaces it
/// as a parse error rather than salvaging a fragment of it.
/// </summary>
public static class AiJsonResponse
{
    private const string Fence = "```";

    public static string Unwrap(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        var text = raw.Trim();

        if (!text.StartsWith(Fence, StringComparison.Ordinal))
        {
            return text;
        }

        var firstLineBreak = text.IndexOf('\n');
        if (firstLineBreak < 0)
        {
            return text;
        }

        if (!text.EndsWith(Fence, StringComparison.Ordinal))
        {
            return text;
        }

        var start = firstLineBreak + 1;
        var length = text.Length - Fence.Length - start;

        return length <= 0 ? string.Empty : text.Substring(start, length).Trim();
    }
}

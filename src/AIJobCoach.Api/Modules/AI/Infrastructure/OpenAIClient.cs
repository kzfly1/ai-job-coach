using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AIJobCoach.Api.Modules.AI.Application;

namespace AIJobCoach.Api.Modules.AI.Infrastructure;

/// <summary>
/// OpenAI chat completions transport client. Uses the named "openai" HttpClient
/// (base URL, 30s timeout, and Polly retry configured in Program.cs) and returns
/// the raw assistant content. Prompt building and response parsing into typed
/// shapes are the responsibility of the consuming service.
/// </summary>
public sealed class OpenAIClient : IOpenAIClient
{
    public const string HttpClientName = "openai";

    private const string DefaultModel = "gpt-4o-mini";
    private const string CompletionsPath = "v1/chat/completions";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<OpenAIClient> _logger;
    private readonly string _model;

    public OpenAIClient(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<OpenAIClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;

        var configuredModel = configuration["OpenAI:Model"];
        _model = string.IsNullOrWhiteSpace(configuredModel) ? DefaultModel : configuredModel;
    }

    public async Task<string> CompleteAsync(
        string systemPrompt,
        string userPrompt,
        CancellationToken ct = default)
    {
        var client = _httpClientFactory.CreateClient(HttpClientName);

        var request = new ChatCompletionRequest(
            _model,
            new[]
            {
                new ChatMessage("system", systemPrompt),
                new ChatMessage("user", userPrompt)
            },
            Temperature: 0);

        _logger.LogInformation("Sending OpenAI chat completion request. Model={Model}", _model);

        using var response = await client.PostAsJsonAsync(CompletionsPath, request, ct);
        response.EnsureSuccessStatusCode();

        var raw = await response.Content.ReadAsStringAsync(ct);
        return ParseContent(raw);
    }

    private static string ParseContent(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new OpenAIParseException("OpenAI returned an empty response body.", raw);
        }

        ChatCompletionResponse? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<ChatCompletionResponse>(raw);
        }
        catch (JsonException ex)
        {
            throw new OpenAIParseException("Failed to parse OpenAI response as JSON.", raw, ex);
        }

        var content = parsed?.Choices is { Count: > 0 }
            ? parsed.Choices[0].Message?.Content
            : null;

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new OpenAIParseException("OpenAI response contained no message content.", raw);
        }

        return content;
    }

    private sealed record ChatCompletionRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("messages")] IReadOnlyList<ChatMessage> Messages,
        [property: JsonPropertyName("temperature")] double Temperature);

    private sealed record ChatMessage(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string Content);

    private sealed record ChatCompletionResponse(
        [property: JsonPropertyName("choices")] IReadOnlyList<ChatChoice>? Choices);

    private sealed record ChatChoice(
        [property: JsonPropertyName("message")] ChatMessage? Message);
}

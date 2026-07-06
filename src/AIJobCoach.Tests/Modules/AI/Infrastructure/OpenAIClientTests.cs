using System.Net;
using System.Net.Http;
using System.Text;
using AIJobCoach.Api.Modules.AI.Application;
using AIJobCoach.Api.Modules.AI.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Polly;
using Polly.Extensions.Http;

namespace AIJobCoach.Tests.Modules.AI.Infrastructure;

public sealed class OpenAIClientTests
{
    private const string ValidCompletion =
        """{"choices":[{"message":{"role":"assistant","content":"hello world"}}]}""";

    [Fact]
    public async Task CompleteAsync_ShouldReturnContent_WhenResponseIsValid()
    {
        // Arrange
        var handler = new StubHandler(_ => JsonResponse(HttpStatusCode.OK, ValidCompletion));
        var client = CreateClient(handler);

        // Act
        var result = await client.CompleteAsync("system", "user");

        // Assert
        result.Should().Be("hello world");
    }

    [Fact]
    public async Task CompleteAsync_ShouldThrowParseException_WhenJsonIsMalformed()
    {
        // Arrange
        var handler = new StubHandler(_ => JsonResponse(HttpStatusCode.OK, "not-json{"));
        var client = CreateClient(handler);

        // Act
        var act = () => client.CompleteAsync("system", "user");

        // Assert
        await act.Should().ThrowAsync<OpenAIParseException>();
    }

    [Fact]
    public async Task CompleteAsync_ShouldThrowParseException_WhenNoContent()
    {
        // Arrange
        var handler = new StubHandler(_ => JsonResponse(HttpStatusCode.OK, """{"choices":[]}"""));
        var client = CreateClient(handler);

        // Act
        var act = () => client.CompleteAsync("system", "user");

        // Assert
        await act.Should().ThrowAsync<OpenAIParseException>();
    }

    [Fact]
    public async Task CompleteAsync_ShouldRetryTransientErrors_ThenSucceed()
    {
        // Arrange: fail with 500 twice, then succeed. Mirrors the production transient-error
        // policy (Program.cs uses 3 retries at 2s/4s/8s); zero delays keep the test fast.
        var responses = new Queue<HttpResponseMessage>(new[]
        {
            JsonResponse(HttpStatusCode.InternalServerError, "{}"),
            JsonResponse(HttpStatusCode.InternalServerError, "{}"),
            JsonResponse(HttpStatusCode.OK, ValidCompletion)
        });

        var handler = new StubHandler(_ => responses.Dequeue());
        var retryPolicy = HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(new[] { TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero });
        var policyHandler = new PolicyHttpMessageHandler(retryPolicy) { InnerHandler = handler };
        var client = CreateClient(policyHandler);

        // Act
        var result = await client.CompleteAsync("system", "user");

        // Assert
        result.Should().Be("hello world");
        handler.CallCount.Should().Be(3);
    }

    private static OpenAIClient CreateClient(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.openai.com/") };
        var factory = new StubHttpClientFactory(httpClient);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["OpenAI:Model"] = "gpt-4o-mini" })
            .Build();

        return new OpenAIClient(factory, configuration, NullLogger<OpenAIClient>.Instance);
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode status, string body)
    {
        return new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
    }

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpClient _client;

        public StubHttpClientFactory(HttpClient client)
        {
            _client = client;
        }

        public HttpClient CreateClient(string name) => _client;
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            _responder = responder;
        }

        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(_responder(request));
        }
    }
}

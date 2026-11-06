using System.Net;
using System.Net.Http;
using System.Text;
using Aurora.App.Services;
using Aurora.App.ViewModels;
using Xunit;

namespace AuroraUpdater.Tests;

public sealed class DemoReliabilityTests
{
    [Theory]
    [InlineData("Hey Aurora tell me a story about the sea", "tell me a story about the sea")]
    [InlineData("Hey, Aurora, plan dinner and a walk", "plan dinner and a walk")]
    [InlineData("okay aurora", "")]
    public void Wake_prefix_preserves_the_complete_request(string input, string expected)
    {
        Assert.True(WakeWordMatcher.TryExtractCommand(input, 0.95f, out var command));
        Assert.Equal(expected, command);
        Assert.False(WakeWordMatcher.TryExtractCommand(input, 0.1f, out _));
    }

    [Fact]
    public void Unrelated_speech_is_not_routed()
    {
        Assert.False(WakeWordMatcher.TryExtractCommand("I said hey aurora yesterday", 0.95f, out _));
        Assert.False(WakeWordMatcher.TryExtractCommand("hey aurorabelle", 0.95f, out _));
    }

    [Fact]
    public void Selected_items_have_human_readable_fallbacks()
    {
        Assert.Equal("Research", new FolderOption { Name = "Research" }.ToString());
        Assert.Equal("Gemini", new CompanionViewModel.ProviderOption { Name = "Gemini" }.ToString());
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "API key")]
    [InlineData(HttpStatusCode.TooManyRequests, "quota")]
    [InlineData(HttpStatusCode.NotFound, "model")]
    [InlineData(HttpStatusCode.ServiceUnavailable, "Try again later")]
    public void Provider_errors_explain_a_recovery_action(HttpStatusCode status, string action) =>
        Assert.Contains(action, ProviderErrors.Format("Gemini", status));

    [Fact]
    public async Task Missing_custom_gemini_model_falls_back_once_and_reports_original_failure()
    {
        var handler = new ModelHandler();
        var client = new GeminiClient("fake-key", "retired-model", new HttpClient(handler));
        var reply = await client.SendWithToolsAsync("system", [], "hello", [], (_, _) => throw new Exception("No tool should run"));
        Assert.Contains("HTTP 404", reply);
        Assert.Contains("saved model is unchanged", reply);
        Assert.EndsWith("Hello", reply);
        Assert.Equal(3, handler.Requests.Count);
        Assert.Contains("retired-model:generateContent", handler.Requests[0]);
        Assert.Contains(AIProviderCatalog.Get("gemini").DefaultModel + ":generateContent", handler.Requests[2]);
    }

    [Fact]
    public async Task Authentication_failure_never_retries_or_exposes_response_body()
    {
        var handler = new ModelHandler { Failure = HttpStatusCode.Unauthorized };
        var client = new GeminiClient("fake-key", "retired-model", new HttpClient(handler));
        var reply = await client.SendWithToolsAsync("system", [], "hello", [], (_, _) => Task.FromResult("unused"));
        Assert.Contains("API key", reply);
        Assert.DoesNotContain("private", reply);
        Assert.Single(handler.Requests);
    }

    private sealed class ModelHandler : HttpMessageHandler
    {
        public HttpStatusCode Failure { get; init; } = HttpStatusCode.NotFound;
        public List<string> Requests { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            Requests.Add(path);
            var model = AIProviderCatalog.Get("gemini").DefaultModel;
            var body = path.Contains("retired-model")
                ? "{\"error\":{\"status\":\"NOT_FOUND\",\"message\":\"private model unavailable\"}}"
                : path.EndsWith("/models")
                    ? "{\"models\":[{\"name\":\"models/" + model + "\",\"supportedGenerationMethods\":[\"generateContent\"]}]}"
                    : "{\"candidates\":[{\"content\":{\"parts\":[{\"text\":\"Hello\"}]}}]}";
            return Task.FromResult(new HttpResponseMessage(path.Contains("retired-model") ? Failure : HttpStatusCode.OK)
            { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }
}

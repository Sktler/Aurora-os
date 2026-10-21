using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Aurora.App;
using Aurora.App.Services;
using Xunit;

namespace AuroraUpdater.Tests;

[Collection("App.Settings")]
public sealed class ImageGenerationToolTests
{
    [Fact]
    public async Task GenerateImageTool_ReturnsClientMessage()
    {
        var previousSettings = App.Settings;
        var previousImageGen = GetStaticProperty<ImageGenClient?>("ImageGen");

        try
        {
            App.Settings = new AppSettings();
            SetStaticProperty("ImageGen", new ImageGenClient("", "gemini"));

            using var document = JsonDocument.Parse("""{"prompt":"a small robot in a raincoat"}""");
            var result = await SystemTools.ExecuteAsync("generate_image", document.RootElement);

            Assert.StartsWith("[No image generation API key set", result);
        }
        finally
        {
            App.Settings = previousSettings ?? new AppSettings();
            SetStaticProperty("ImageGen", previousImageGen);
        }
    }

    private sealed class StubHandler : System.Net.Http.HttpMessageHandler
    {
        private readonly System.Collections.Generic.Queue<System.Net.HttpStatusCode> _codes;
        public int Calls;
        public StubHandler(params System.Net.HttpStatusCode[] codes) => _codes = new(codes);

        protected override Task<System.Net.Http.HttpResponseMessage> SendAsync(
            System.Net.Http.HttpRequestMessage request, System.Threading.CancellationToken ct)
        {
            Calls++;
            var code = _codes.Count > 1 ? _codes.Dequeue() : _codes.Peek();
            var body = code == System.Net.HttpStatusCode.OK
                ? """{"candidates":[{"content":{"parts":[{"inlineData":{"mimeType":"image/png","data":"AAAA"}}]}}]}"""
                : "RESOURCE_EXHAUSTED";
            return Task.FromResult(new System.Net.Http.HttpResponseMessage(code)
            {
                Content = new System.Net.Http.StringContent(body)
            });
        }
    }

    [Fact]
    public async Task GenerateImage_RetriesOn429ThenSucceeds()
    {
        var handler = new StubHandler((System.Net.HttpStatusCode)429, System.Net.HttpStatusCode.OK);
        var client = new ImageGenClient("key", "gemini", new System.Net.Http.HttpClient(handler), _ => Task.CompletedTask);

        var result = await client.GenerateImageAsync("robot");

        Assert.StartsWith("data:image/png;base64,", result);
        Assert.Equal(2, handler.Calls);
    }

    [Fact]
    public async Task GenerateImage_PersistentRateLimit_ReturnsFriendlyMessage()
    {
        var handler = new StubHandler((System.Net.HttpStatusCode)429);
        var client = new ImageGenClient("key", "gemini", new System.Net.Http.HttpClient(handler), _ => Task.CompletedTask);

        var result = await client.GenerateImageAsync("robot");

        Assert.Contains("rate limited", result);
        Assert.Equal(4, handler.Calls);
    }

    private static T? GetStaticProperty<T>(string name)
    {
        var property = typeof(App).GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        return property == null ? default : (T?)property.GetValue(null);
    }

    private static void SetStaticProperty<T>(string name, T value)
    {
        var property = typeof(App).GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        var setter = property?.GetSetMethod(true);
        setter?.Invoke(null, new object?[] { value });
    }
}

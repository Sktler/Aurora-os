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

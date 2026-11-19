using System.Net;
using System.Net.Http;
using System.Text.Json;
using Aurora.App;
using Aurora.App.Services;
using Xunit;

namespace AuroraUpdater.Tests;

[Collection("App.Settings")]
public sealed class BrowserApprovalTests
{
    [Theory]
    [InlineData("{\"AbstractText\":\"Saturn has rings.\",\"AbstractSource\":\"Test source\"}", "Saturn has rings. (via Test source)")]
    [InlineData("{}", "No instant answer found. Full search results: https://www.google.com/search?q=rings%20%26%20moons")]
    [InlineData(null, "No instant answer found. Full search results: https://www.google.com/search?q=rings%20%26%20moons")]
    public async Task SearchTool_ReturnsInstantAnswerOrResultsLink(string? response, string expected)
    {
        using var storage = new AppSettingsTestScope();
        using var http = new HttpClient(new SearchHandler(response));
        typeof(App).GetProperty(nameof(App.WebSearch))!.SetValue(null, new WebSearchClient(http));
        var input = JsonSerializer.SerializeToElement(new { query = "rings & moons" });

        Assert.Equal(expected, await SystemTools.ExecuteAsync("web_search", input));
    }

    [Fact]
    public async Task BrowserSearchTool_RequiresFreshApprovalEvenWithTrustedToolsAndSavedGrant()
    {
        using var storage = new AppSettingsTestScope();
        const string tool = "web_search_open_browser";
        App.Settings.ActionApprovalMode = "AllowTrusted";
        App.Settings.TrustedTools.Add(tool);
        App.Settings.PersistentGrantedTools.Add(tool);
        using var http = new HttpClient(new SearchHandler(null));
        typeof(App).GetProperty(nameof(App.WebSearch))!.SetValue(null, new WebSearchClient(http));
        var input = JsonSerializer.SerializeToElement(new { query = "rings & moons" });

        var result = await SystemTools.ExecuteAsync(tool, input);

        Assert.Equal("Browser opening was not approved. Full search results: https://www.google.com/search?q=rings%20%26%20moons", result);
        Assert.Contains(tool, App.Settings.PersistentGrantedTools);
    }

    [Theory]
    [InlineData("web_search")]
    [InlineData("web_search_open_browser")]
    public async Task WebTools_RespectLocalOnlyMode(string tool)
    {
        using var storage = new AppSettingsTestScope();
        App.Settings.LocalOnlyMode = true;
        var input = JsonSerializer.SerializeToElement(new { query = "rings" });

        Assert.Equal(App.LocalOnlyModeBlockedMessage("web search"), await SystemTools.ExecuteAsync(tool, input));
    }

    [Theory]
    [InlineData("https://example.invalid/")]
    [InlineData("microsoft-edge:https://example.invalid/")]
    [InlineData("results.url")]
    [InlineData("chrome.exe")]
    public void ShellTools_RequireApprovalBeforeDispatchingTarget(string target)
    {
        using var storage = new AppSettingsTestScope();
        App.Settings.PersistentGrantedTools.Add("windows_launch_application");
        var service = new WindowsAutomationService { ApplicationsEnabled = true, FilesEnabled = true };

        Assert.Contains("denied by Aurora approval policy", Assert.Throws<UnauthorizedAccessException>(() => service.Launch(target)).Message);
        Assert.Contains("denied by Aurora approval policy", Assert.Throws<UnauthorizedAccessException>(() => service.OpenPath(target)).Message);
    }

    private sealed class SearchHandler(string? response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (response == null)
                throw new HttpRequestException("Test lookup failed.");

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(response) });
        }
    }
}

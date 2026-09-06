using System.Net.Http.Headers;
using System.Text.Json;
using Aurora.Core;

namespace Aurora.AndroidApp.Updates;

public sealed class GitHubReleaseUpdateSource(HttpClient http) : IUpdateSource
{
    private const string ReleasesEndpoint = "https://api.github.com/repos/Sktler/Aurora-os/releases/latest";

    public async Task<UpdateInfo?> CheckForUpdateAsync(string currentVersion, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, ReleasesEndpoint);
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("Aurora", "1.0"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

        using var response = await http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode) return null;

        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
        var root = document.RootElement;
        var version = root.TryGetProperty("tag_name", out var tag) ? tag.GetString() : null;
        if (string.IsNullOrWhiteSpace(version) || string.Equals(version.TrimStart('v'), currentVersion.TrimStart('v'), StringComparison.OrdinalIgnoreCase)) return null;

        string? apk = null;
        if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
        {
            apk = assets.EnumerateArray()
                .Where(a => a.TryGetProperty("name", out var n) && n.GetString()?.EndsWith(".apk", StringComparison.OrdinalIgnoreCase) == true)
                .Select(a => a.TryGetProperty("browser_download_url", out var u) ? u.GetString() : null)
                .FirstOrDefault(u => !string.IsNullOrWhiteSpace(u));
        }

        return string.IsNullOrWhiteSpace(apk) ? null : new UpdateInfo(version!, apk, root.TryGetProperty("body", out var body) ? body.GetString() : null);
    }
}

using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;

namespace ZoeyOS.App.Services;

public sealed record AuroraUpdateInfo(string Version, string DownloadUrl, string ReleaseUrl, string? ReleaseNotes);

public sealed class WindowsUpdateService
{
    private const string LatestReleaseUrl = "https://api.github.com/repos/Sktler/Aurora-os/releases/latest";
    private const string AssetName = "Aurora-Windows.zip";
    private readonly HttpClient _http;

    public WindowsUpdateService(HttpClient? http = null)
    {
        _http = http ?? new HttpClient();
        _http.DefaultRequestHeaders.UserAgent.Clear();
        _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Aurora", "1.0"));
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
    }

    public string CurrentVersion => Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "0.0.0";

    public async Task<AuroraUpdateInfo?> CheckAsync(CancellationToken cancellationToken = default)
    {
        using var response = await _http.GetAsync(LatestReleaseUrl, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = json.RootElement;
        var tag = root.TryGetProperty("tag_name", out var tagName) ? tagName.GetString() : null;
        var version = NormalizeVersion(tag);
        if (version is null || !Version.TryParse(CurrentVersion, out var current) || version <= current)
            return null;

        string? download = null;
        if (root.TryGetProperty("assets", out var assets))
        {
            foreach (var asset in assets.EnumerateArray())
            {
                if (asset.TryGetProperty("name", out var name) && name.GetString() == AssetName &&
                    asset.TryGetProperty("browser_download_url", out var url))
                {
                    download = url.GetString();
                    break;
                }
            }
        }

        return string.IsNullOrWhiteSpace(download)
            ? null
            : new AuroraUpdateInfo(version.ToString(3), download!, root.GetProperty("html_url").GetString() ?? "https://github.com/Sktler/Aurora-os/releases", root.TryGetProperty("body", out var body) ? body.GetString() : null);
    }

    public async Task InstallAsync(AuroraUpdateInfo update, IProgress<int>? progress = null, CancellationToken cancellationToken = default)
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "AuroraUpdate", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        var zipPath = Path.Combine(tempRoot, AssetName);
        var extractPath = Path.Combine(tempRoot, "payload");

        try
        {
            using var response = await _http.GetAsync(update.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();
            var total = response.Content.Headers.ContentLength;
            await using (var input = await response.Content.ReadAsStreamAsync(cancellationToken))
            await using (var output = File.Create(zipPath))
            {
                var buffer = new byte[81920];
                long read = 0;
                int count;
                while ((count = await input.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    await output.WriteAsync(buffer.AsMemory(0, count), cancellationToken);
                    read += count;
                    if (total is > 0) progress?.Report((int)Math.Clamp(read * 100 / total.Value, 0, 100));
                }
            }

            ZipFile.ExtractToDirectory(zipPath, extractPath);
            var appPath = Environment.ProcessPath ?? throw new InvalidOperationException("Aurora process path is unavailable.");
            var appDirectory = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
            var script = Path.Combine(tempRoot, "apply-update.cmd");
            var payload = Directory.GetFiles(extractPath, "*", SearchOption.AllDirectories);
            if (payload.Length == 0) throw new InvalidDataException("The update package is empty.");

            var scriptText = "@echo off\r\n" +
                "timeout /t 2 /nobreak >nul\r\n" +
                $"robocopy \"{extractPath}\" \"{appDirectory}\" /E /IS /IT /NFL /NDL /NJH /NJS >nul\r\n" +
                $"start \"\" \"{appPath}\"\r\n" +
                $"rmdir /S /Q \"{tempRoot}\"\r\n";
            await File.WriteAllTextAsync(script, scriptText, cancellationToken);

            Process.Start(new ProcessStartInfo
            {
                FileName = script,
                WorkingDirectory = tempRoot,
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden
            });
        }
        catch
        {
            try { Directory.Delete(tempRoot, true); } catch { }
            throw;
        }
    }

    private static Version? NormalizeVersion(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag)) return null;
        tag = tag.Trim().TrimStart('v', 'V');
        var dash = tag.IndexOf('-');
        if (dash >= 0) tag = tag[..dash];
        return Version.TryParse(tag, out var version) ? version : null;
    }
}

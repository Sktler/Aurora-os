using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using System.Windows.Controls;
using Xunit;
using ZoeyOS.App.Services;
using ZoeyOS.App.Views;

namespace AuroraUpdater.Tests;

public class WindowsUpdaterServiceTests
{
    [Fact]
    public void WindowsAutomationService_FromSettingsMapsClipboardAndApplicationPermissions()
    {
        var settings = new AppSettings
        {
            WindowsFilesEnabled = true,
            WindowsScreenEnabled = true,
            WindowsClipboardEnabled = true,
            WindowsApplicationsEnabled = true,
            WindowsTerminalEnabled = true,
            WindowsUiAutomationEnabled = true,
            WindowsNetworkEnabled = true,
            WindowsPowerEnabled = true
        };

        var service = WindowsAutomationService.FromSettings(settings);

        Assert.True(service.FilesEnabled);
        Assert.True(service.ScreenEnabled);
        Assert.True(service.ClipboardEnabled);
        Assert.True(service.ApplicationsEnabled);
        Assert.True(service.TerminalEnabled);
        Assert.True(service.UiAutomationEnabled);
        Assert.True(service.NetworkEnabled);
        Assert.True(service.PowerEnabled);
    }

    [Fact]
    public void WindowsAutomationService_RejectsClipboardAndApplicationAccessWhenDisabled()
    {
        var service = new WindowsAutomationService();

        Assert.Throws<UnauthorizedAccessException>(() => service.GetClipboardText());
        Assert.Throws<UnauthorizedAccessException>(() => service.GetProcesses());
    }

    [Fact]
    public void NormalizeVersionString_StripsLeadingVAndKeepsNumericCore()
    {
        Assert.Equal("1.2.3", WindowsUpdaterService.NormalizeVersionString("v1.2.3"));
        Assert.Equal("2.4.5", WindowsUpdaterService.NormalizeVersionString("2.4.5-beta"));
    }

    [Fact]
    public void IsVersionGreater_UsesSemVerComparison()
    {
        Assert.True(WindowsUpdaterService.IsVersionGreater("1.2.3", "1.2.4"));
        Assert.False(WindowsUpdaterService.IsVersionGreater("1.2.4", "1.2.4"));
        Assert.False(WindowsUpdaterService.IsVersionGreater("2.0.0", "1.9.9"));
    }

    [Fact]
    public void ParseReleasePayload_ChoosesInstallerAssetAndFlagsUpdate()
    {
        const string payload = """
        {
          "tag_name": "v2.1.0",
          "html_url": "https://github.com/Sktler/Aurora-os/releases/tag/v2.1.0",
          "body": "Includes a safer installer flow.",
          "assets": [
            { "name": "Aurora-2.1.0.sha256", "browser_download_url": "https://example.invalid/sha256" },
            { "name": "Aurora-2.1.0.zip", "browser_download_url": "https://example.invalid/aurora.zip" }
          ]
        }
        """;

        var result = WindowsUpdaterService.ParseReleasePayload(payload, "2.0.0");

        Assert.True(result.UpdateAvailable);
        Assert.Equal("2.1.0", result.LatestVersion);
        Assert.Equal("https://example.invalid/aurora.zip", result.DownloadUrl);
        Assert.Equal("https://github.com/Sktler/Aurora-os/releases/tag/v2.1.0", result.ReleaseUrl);
    }

    [Fact]
    public void BuildPackageFileName_CreatesStableFilename()
    {
        var name = WindowsUpdaterService.BuildPackageFileName("v3.2.1", "https://example.invalid/Aurora-3.2.1.zip");
        Assert.Equal("Aurora-3-2-1.zip", name);
    }

    [Fact]
    public void SelectPreferredAssetUrl_RejectsNonHttpsAndChecksumAssets()
    {
        using var document = System.Text.Json.JsonDocument.Parse("""
        {
          "assets": [
            { "name": "Aurora.sha256", "browser_download_url": "https://example.invalid/Aurora.sha256" },
            { "name": "Aurora.zip", "browser_download_url": "http://example.invalid/Aurora.zip" },
            { "name": "Aurora.zip", "browser_download_url": "https://example.invalid/Aurora.zip" }
          ]
        }
        """);

        Assert.Equal(
            "https://example.invalid/Aurora.zip",
            WindowsUpdaterService.SelectPreferredAssetUrl(document.RootElement));
    }

    [Fact]
    public void SelectPreferredChecksumUrl_ChoosesSha256Asset()
    {
        using var document = System.Text.Json.JsonDocument.Parse("""
        {
          "assets": [
            { "name": "Aurora.zip", "browser_download_url": "https://example.invalid/Aurora.zip" },
            { "name": "Aurora.sha256", "browser_download_url": "https://example.invalid/Aurora.sha256" }
          ]
        }
        """);

        Assert.Equal(
            "https://example.invalid/Aurora.sha256",
            WindowsUpdaterService.SelectPreferredChecksumUrl(document.RootElement));
    }

    [Fact]
    public void AuroraOrbLoader_PrefersTheOrbImageOverOtherImagesInTheVisualTree()
    {
        Exception? threadException = null;
        string? orbTag = null;

        var thread = new Thread(() =>
        {
            try
            {
                var root = new System.Windows.Controls.Grid();
                var albumArt = new Image { Tag = "AlbumArt" };
                var orb = new Image { Tag = "AuroraOrb" };
                var nested = new System.Windows.Controls.Grid();
                nested.Children.Add(orb);
                root.Children.Add(albumArt);
                root.Children.Add(nested);

                var orbLoaderType = typeof(MainWindow).Assembly.GetType("ZoeyOS.App.Services.AuroraOrbLoader", throwOnError: true)!;
                var method = orbLoaderType.GetMethod("FindTargetImage", BindingFlags.NonPublic | BindingFlags.Static);
                Assert.NotNull(method);

                var orbResult = Assert.IsType<Image>(method.Invoke(null, new object[] { root }));
                orbTag = orbResult.Tag as string;
            }
            catch (Exception ex)
            {
                threadException = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        Assert.Null(threadException);
        Assert.Equal("AuroraOrb", orbTag);
    }

    [Fact]
    public async Task VerifySha256Async_ValidatesPackageContents()
    {
        var packagePath = Path.Combine(Path.GetTempPath(), $"aurora-{Guid.NewGuid():N}.bin");
        var contents = System.Text.Encoding.UTF8.GetBytes("aurora update");
        await File.WriteAllBytesAsync(packagePath, contents);

        try
        {
            var hash = Convert.ToHexString(SHA256.HashData(contents));
            Assert.True(await WindowsUpdaterService.VerifySha256Async(packagePath, hash));
            Assert.False(await WindowsUpdaterService.VerifySha256Async(packagePath, new string('0', 64)));
        }
        finally
        {
            File.Delete(packagePath);
        }
    }
}

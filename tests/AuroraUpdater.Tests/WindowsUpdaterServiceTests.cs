using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Xunit;
using Aurora.App.Services;
using Aurora.App.Views;

namespace AuroraUpdater.Tests;

public class WindowsUpdaterServiceTests
{
    [Fact]
    public void ResetAll_RemovesExternalDatabaseAndSqliteSidecars()
    {
        var root = Path.Combine(Path.GetTempPath(), $"aurora-reset-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var databasePath = Path.Combine(root, "aurora.db");
        var configDirectory = Path.Combine(root, "config");
        Directory.CreateDirectory(configDirectory);
        File.WriteAllText(Path.Combine(configDirectory, "settings.json"), "settings");
        File.WriteAllText(databasePath, "database");
        File.WriteAllText($"{databasePath}-wal", "wal");
        File.WriteAllText($"{databasePath}-shm", "shm");

        try
        {
            AppSettings.ResetAll(databasePath, configDirectory);

            Assert.False(Directory.Exists(configDirectory));
            Assert.False(File.Exists(databasePath));
            Assert.False(File.Exists($"{databasePath}-wal"));
            Assert.False(File.Exists($"{databasePath}-shm"));
        }

        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

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

                var orbLoaderType = typeof(MainWindow).Assembly.GetType("Aurora.App.Services.AuroraOrbLoader", throwOnError: true)!;
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
    public void MiniCompanionWindow_CloseButton_InvokesExitRequested()
    {
        MiniCompanionWindow? window = null;
        Exception? threadException = null;
        var raised = false;

        var thread = new Thread(() =>
        {
            try
            {
                window = new MiniCompanionWindow();
                window.ExitRequested += (_, _) => raised = true;

                var method = typeof(MiniCompanionWindow).GetMethod("Close_Click", BindingFlags.NonPublic | BindingFlags.Instance);
                Assert.NotNull(method);

                method.Invoke(window, new object[] { window, new RoutedEventArgs() });
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
        Assert.True(raised);
    }

    [Fact]
    public void SetupWindow_HasSkipClickHandler()
    {
        var method = typeof(SetupWindow).GetMethod("Skip_Click", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(method);
    }

    [Fact]
    public void SystemTools_DefinitionsExposeTerminalPowerAndNetworkTools()
    {
        var names = SystemTools.Definitions
            .Select(definition => definition.GetType().GetProperty("name")?.GetValue(definition)?.ToString())
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains("windows_run_powershell", names);
        Assert.Contains("windows_run_cmd", names);
        Assert.Contains("windows_power_control", names);
        Assert.Contains("windows_network_status", names);
        Assert.Contains("windows_wifi_status", names);
        Assert.Contains("windows_wifi_toggle", names);
        Assert.Contains("windows_bluetooth_status", names);
    }

    [Fact]
    public void Globe3D_AccentColorRetintsOrbImage()
    {
        Exception? threadException = null;
        Color? tintedColor = null;

        var thread = new Thread(() =>
        {
            try
            {
                var globe = new Globe3D();
                var orb = Assert.IsType<Image>(globe.FindName("AuroraOrbImage"));

                var bitmap = new WriteableBitmap(1, 1, 96, 96, PixelFormats.Bgra32, null);
                bitmap.WritePixels(new Int32Rect(0, 0, 1, 1), new byte[] { 100, 100, 100, 255 }, 4, 0);
                orb.Source = bitmap;

                globe.AccentColor = "#FF0000";

                var tinted = Assert.IsAssignableFrom<BitmapSource>(orb.Source);
                var pixels = new byte[4];
                tinted.CopyPixels(pixels, 4, 0);
                tintedColor = Color.FromArgb(pixels[3], pixels[2], pixels[1], pixels[0]);
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
        Assert.True(tintedColor.HasValue);
        Assert.True(tintedColor.Value.R > tintedColor.Value.G);
        Assert.True(tintedColor.Value.R > tintedColor.Value.B);
        Assert.Equal(255, tintedColor.Value.A);
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

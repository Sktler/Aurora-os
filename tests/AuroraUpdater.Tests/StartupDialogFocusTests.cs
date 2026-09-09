using System.IO;
using Xunit;

namespace AuroraUpdater.Tests;

public sealed class StartupDialogFocusTests
{
    [Fact]
    public void FirstRunWindow_is_configured_to_foreground_itself()
    {
        var source = ReadViewSource("FirstRunWindow.cs");

        Assert.DoesNotContain("TimeSpan.FromSeconds(1)", source);
        Assert.Contains("TimeSpan.FromMilliseconds(250)", source);
        Assert.Contains("Topmost = true;", source);
        Assert.Contains("ShowInTaskbar = true;", source);
        Assert.Contains("WindowState = WindowState.Normal;", source);
        Assert.Contains("Loaded += (_, _) =>", source);
        Assert.Contains("Activate();", source);
        Assert.Contains("Focus();", source);
    }

    [Fact]
    public void StartupPermissionWindow_and_choice_dialog_are_configured_to_foreground_themselves()
    {
        var source = ReadViewSource("StartupPermissionWindow.cs");

        Assert.Equal(2, CountOccurrences(source, "Topmost = true;"));
        Assert.Equal(2, CountOccurrences(source, "ShowInTaskbar = true;"));
        Assert.Equal(3, CountOccurrences(source, "WindowState = WindowState.Normal;"));
        Assert.Equal(1, CountOccurrences(source, "dialog.Activate();"));
        Assert.Equal(2, CountOccurrences(source, "Activate();"));
        Assert.Equal(2, CountOccurrences(source, "Focus();"));
    }

    [Fact]
    public void SetupWindow_is_configured_to_foreground_itself()
    {
        var xaml = ReadViewSource("SetupWindow.xaml");
        var code = ReadViewSource("SetupWindow.xaml.cs");

        Assert.Contains("Topmost=\"True\"", xaml);
        Assert.Contains("WindowState=\"Normal\"", xaml);
        Assert.Contains("Loaded=\"Window_Loaded\"", xaml);
        Assert.Contains("WindowState = WindowState.Normal;", code);
        Assert.Contains("Activate();", code);
        Assert.Contains("Focus();", code);
    }

    private static string ReadViewSource(string fileName)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            var path = Path.Combine(directory.FullName, "Aurora.App", "Views", fileName);
            if (File.Exists(path))
                return File.ReadAllText(path);

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException($"Could not locate {fileName}.");
    }

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }
}

using System.IO;
using Xunit;

namespace AuroraUpdater.Tests;

public sealed class CompactPanelLayoutTests
{
    [Fact]
    public void Setup_and_settings_windows_are_tall_enough_to_avoid_unneeded_scrollbars()
    {
        Assert.Contains("Height=\"900\" Width=\"560\"", Read("Aurora.App", "Views", "SetupWindow.xaml"));
        Assert.Contains("Height=\"840\" Width=\"720\"", Read("Aurora.App", "Views", "IntegrationsWindow.xaml"));
    }

    [Fact]
    public void Widgets_panel_keeps_scrolling_but_has_compact_spacing()
    {
        var xaml = Read("Aurora.App", "Views", "MainWindow.xaml");

        Assert.Contains("Padding=\"16,10,16,10\"><ScrollViewer VerticalScrollBarVisibility=\"Auto\">", xaml.Replace("\r\n", "\n").Replace("\n                ", ""));
        Assert.DoesNotContain("Margin=\"0,26,0,10\"", xaml);
    }

    [Fact]
    public void Change_name_dialog_sizes_to_its_content()
    {
        var code = Read("Aurora.App", "Views", "MainWindow.xaml.cs");

        Assert.Contains("SizeToContent = SizeToContent.Height", code);
    }

    private static string Read(params string[] parts)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Aurora.sln")))
            dir = dir.Parent;
        var root = dir?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
        return File.ReadAllText(Path.Combine([root, .. parts]));
    }
}

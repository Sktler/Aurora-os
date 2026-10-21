using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace AuroraUpdater.Tests;

public sealed class ButtonContrastTests
{
    [Fact]
    public void Default_button_style_uses_dark_background_with_light_text()
    {
        var xaml = ReadAppXaml();
        var style = Regex.Match(xaml, "<Style TargetType=\"\\{x:Type Button\\}\">.*?</Style>", RegexOptions.Singleline).Value;

        Assert.NotEmpty(style);
        Assert.Contains("Property=\"Background\" Value=\"{StaticResource BgCardBrush}\"", style);
        Assert.Contains("Property=\"Foreground\" Value=\"{StaticResource TextPrimaryBrush}\"", style);
        Assert.Contains("TextElement.Foreground=\"{TemplateBinding Foreground}\"", style);
    }

    [Fact]
    public void Theme_text_colors_have_readable_contrast_on_card_background()
    {
        Assert.True(Contrast("#FFFFFF", "#171C28") >= 7);
        Assert.True(Contrast("#D0D7E2", "#171C28") >= 7);
    }

    private static double Contrast(string a, string b)
    {
        var (l1, l2) = (Luminance(a), Luminance(b));
        return (Math.Max(l1, l2) + 0.05) / (Math.Min(l1, l2) + 0.05);
    }

    private static double Luminance(string hex)
    {
        double Channel(int i)
        {
            var c = Convert.ToInt32(hex.Substring(1 + i * 2, 2), 16) / 255.0;
            return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }
        return 0.2126 * Channel(0) + 0.7152 * Channel(1) + 0.0722 * Channel(2);
    }

    private static string ReadAppXaml()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Aurora.sln")))
            dir = dir.Parent;
        var root = dir?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
        return File.ReadAllText(Path.Combine(root, "Aurora.App", "App.xaml"));
    }
}

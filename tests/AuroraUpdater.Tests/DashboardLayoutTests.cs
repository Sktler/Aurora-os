using Aurora.App.Services;
using Xunit;

namespace AuroraUpdater.Tests;

public sealed class DashboardLayoutTests
{
    [Fact]
    public void Default_preset_shows_both_panels_at_default_widths()
    {
        var layout = DashboardLayout.ForPreset(DashboardLayoutPreset.Default);

        Assert.True(layout.LeftVisible);
        Assert.True(layout.RightVisible);
        Assert.Equal(DashboardLayout.DefaultLeftWidth, layout.LeftWidth);
        Assert.Equal(DashboardLayout.DefaultRightWidth, layout.RightWidth);
    }

    [Fact]
    public void Chat_focused_hides_both_panels_and_compact_hides_info_panel()
    {
        var chat = DashboardLayout.ForPreset(DashboardLayoutPreset.ChatFocused);
        Assert.False(chat.LeftVisible);
        Assert.False(chat.RightVisible);

        var compact = DashboardLayout.ForPreset(DashboardLayoutPreset.Compact);
        Assert.True(compact.LeftVisible);
        Assert.False(compact.RightVisible);
    }

    [Fact]
    public void Normalize_clamps_invalid_widths()
    {
        var layout = new DashboardLayout(true, true, double.NaN, 5000).Normalize();

        Assert.Equal(DashboardLayout.DefaultLeftWidth, layout.LeftWidth);
        Assert.Equal(DashboardLayout.MaxPanelWidth, layout.RightWidth);
        Assert.Equal(DashboardLayout.MinPanelWidth, new DashboardLayout(true, true, 1, 1).Normalize().LeftWidth);
    }

    [Theory]
    [InlineData(null, DashboardLayoutPreset.Default)]
    [InlineData("garbage", DashboardLayoutPreset.Default)]
    [InlineData("99", DashboardLayoutPreset.Default)]
    [InlineData("compact", DashboardLayoutPreset.Compact)]
    [InlineData("Custom", DashboardLayoutPreset.Custom)]
    public void ParsePreset_falls_back_to_default(string? value, DashboardLayoutPreset expected)
    {
        Assert.Equal(expected, DashboardLayout.ParsePreset(value));
    }
}

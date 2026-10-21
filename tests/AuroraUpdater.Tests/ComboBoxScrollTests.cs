using System.IO;
using Xunit;

namespace AuroraUpdater.Tests;

public sealed class ComboBoxScrollTests
{
    [Fact]
    public void Combo_box_dropdown_is_scrollable_and_honors_max_dropdown_height()
    {
        var xaml = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "Aurora.App", "App.xaml"));

        Assert.Contains("MaxHeight=\"{TemplateBinding MaxDropDownHeight}\"", xaml);
        Assert.Contains("<ScrollViewer CanContentScroll=\"True\" VerticalScrollBarVisibility=\"Auto\" HorizontalScrollBarVisibility=\"Disabled\" Margin=\"2\"><ItemsPresenter", xaml);
    }

    [Fact]
    public void Model_picker_sets_a_bounded_dropdown_height()
    {
        var xaml = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "Aurora.App", "Views", "IntegrationsWindow.xaml"));

        Assert.Contains("MaxDropDownHeight=\"360\"", xaml);
    }

    private static string FindRepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Aurora.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }
}

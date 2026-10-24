using System.Collections.Specialized;
using System.IO;
using System.Text.Json;
using System.Xml.Linq;
using Aurora.App.Models;
using Aurora.App.Services;
using Aurora.App.ViewModels;
using Xunit;

namespace AuroraUpdater.Tests;

public sealed class SettingsNavigationTests
{
    [Fact]
    public void Existing_settings_without_pins_start_with_a_clean_sidebar()
    {
        var settings = JsonSerializer.Deserialize<AppSettings>("{}")!;
        var navigation = new SettingsNavigationViewModel(settings);

        Assert.Empty(navigation.PinnedEntries);
        Assert.All(navigation.Entries, entry => Assert.False(entry.IsPinned));
        Assert.Contains(navigation.Entries, entry => entry.Section == SettingsSection.Memory);
        Assert.Contains(navigation.Entries, entry => entry.Section == SettingsSection.Tools);
        Assert.Equal(navigation.Entries.Count, navigation.Entries.Select(entry => entry.Section).Distinct().Count());
    }

    [Fact]
    public void Pin_and_unpin_update_the_shared_sidebar_and_survive_serialization()
    {
        var settings = new AppSettings();
        string? savedJson = null;
        var saves = 0;
        var navigation = new SettingsNavigationViewModel(settings, () =>
        {
            saves++;
            savedJson = JsonSerializer.Serialize(settings);
        });
        var notifications = new List<NotifyCollectionChangedAction>();
        ((INotifyCollectionChanged)navigation.PinnedEntries).CollectionChanged += (_, e) => notifications.Add(e.Action);

        foreach (var entry in navigation.Entries)
        {
            navigation.TogglePinCommand.Execute(entry);
            Assert.True(entry.IsPinned);
            Assert.Contains(entry, navigation.PinnedEntries);
        }

        var restored = new SettingsNavigationViewModel(JsonSerializer.Deserialize<AppSettings>(savedJson!)!);
        Assert.Equal(navigation.Entries.Select(entry => entry.Section), restored.PinnedEntries.Select(entry => entry.Section));
        Assert.Equal(navigation.Entries.Count, saves);

        foreach (var entry in navigation.Entries)
        {
            navigation.TogglePinCommand.Execute(entry);
            Assert.False(entry.IsPinned);
        }

        Assert.Empty(navigation.PinnedEntries);
        Assert.Empty(settings.PinnedSettingsSections);
        Assert.Contains(NotifyCollectionChangedAction.Add, notifications);
        Assert.Contains(NotifyCollectionChangedAction.Remove, notifications);
    }

    [Theory]
    [InlineData("{\"PinnedSettingsSections\":null}", 0)]
    [InlineData("{\"PinnedSettingsSections\":[\"music\",\"Music\",\"Tools\",\"Hub\",\"999\",\"unknown\",null]}", 2)]
    public void Invalid_and_duplicate_saved_pins_are_ignored(string json, int expectedCount)
    {
        var navigation = new SettingsNavigationViewModel(JsonSerializer.Deserialize<AppSettings>(json)!);

        Assert.Equal(expectedCount, navigation.PinnedEntries.Count);
        Assert.All(navigation.PinnedEntries, entry => Assert.Contains(entry, navigation.Entries));
    }

    [Fact]
    public void Pins_follow_hub_order_even_when_selected_in_reverse()
    {
        var navigation = new SettingsNavigationViewModel(new AppSettings(), () => { });
        foreach (var entry in navigation.Entries.Reverse()) navigation.TogglePinCommand.Execute(entry);

        Assert.Equal(navigation.Entries, navigation.PinnedEntries);
    }

    [Fact]
    public void Save_failure_keeps_the_original_pin_state_and_reports_the_error()
    {
        var settings = new AppSettings { PinnedSettingsSections = new() { "Music" } };
        var navigation = new SettingsNavigationViewModel(settings, () => throw new IOException("Disk full"));
        var music = navigation.Entries.Single(entry => entry.Section == SettingsSection.Music);
        var tools = navigation.Entries.Single(entry => entry.Section == SettingsSection.Tools);

        navigation.TogglePinCommand.Execute(music);
        Assert.True(music.IsPinned);
        navigation.TogglePinCommand.Execute(tools);
        Assert.False(tools.IsPinned);
        Assert.Same(music, Assert.Single(navigation.PinnedEntries));
        Assert.Equal(new[] { "Music" }, settings.PinnedSettingsSections);
        Assert.Contains("Disk full", navigation.Status);
    }

    [Fact]
    public void Separate_profiles_keep_separate_pin_preferences()
    {
        var first = new SettingsNavigationViewModel(new AppSettings(), () => { });
        var second = new SettingsNavigationViewModel(new AppSettings(), () => { });
        first.TogglePinCommand.Execute(first.Entries[0]);

        Assert.Single(first.PinnedEntries);
        Assert.Empty(second.PinnedEntries);
        second.TogglePinCommand.Execute(first.Entries[0]);
        Assert.Empty(second.PinnedEntries);
    }

    [Fact]
    public void Sidebar_and_settings_templates_use_shared_navigation_and_existing_windows()
    {
        XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        var home = XDocument.Load(FindView("MainWindow.xaml"));
        var fixedDestinations = home.Descendants(xaml + "Button")
            .Select(button => (string?)button.Attribute("Tag"))
            .Where(tag => tag != null && !tag.StartsWith('{')).ToArray();
        Assert.Equal(new[] { "home", "chat", "settings" }, fixedDestinations);
        Assert.Contains(home.Descendants(xaml + "ItemsControl"), control =>
            (string?)control.Attribute("ItemsSource") == "{Binding SettingsNavigation.PinnedEntries}");

        var settings = XDocument.Load(FindView("IntegrationsWindow.xaml"));
        var entries = settings.Descendants(xaml + "ItemsControl").Single(control =>
            (string?)control.Attribute("ItemsSource") == "{Binding SettingsNavigation.Entries}");
        var buttons = entries.Descendants(xaml + "Button").ToArray();
        Assert.Equal(2, buttons.Length);
        Assert.Empty(buttons[0].Descendants(xaml + "Button"));
        Assert.Contains("GoToSectionCommand", (string?)buttons[0].Attribute("Command"));
        Assert.Contains("TogglePinCommand", (string?)buttons[1].Attribute("Command"));
        Assert.Contains(settings.Descendants(xaml + "Button"), button => (string?)button.Attribute("Click") == "OpenMemory_Click");
        Assert.Contains(settings.Descendants(xaml + "Button"), button => (string?)button.Attribute("Click") == "OpenTools_Click");
    }

    private static string FindView(string file)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "Aurora.sln"))) directory = directory.Parent;
        return Path.Combine(directory?.FullName ?? throw new DirectoryNotFoundException(), "Aurora.App", "Views", file);
    }
}

using System.IO;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;
using System.Windows.Media;
using System.Xml.Linq;
using Aurora.App.Services;
using Aurora.App.Views;
using Xunit;

namespace AuroraUpdater.Tests;

[Collection("Localization UI")]
public sealed class DemoPresentationTests
{
    [Fact]
    public void Connection_labels_refresh_without_reconnecting()
    {
        RunOnSta(() =>
        {
            var previous = Localizer.Language;
            var label = new TextBlock { DataContext = new { Connected = true } };
            var extension = new LocBindingExtension("Connected") { TrueText = "Connected", FalseText = "Not connected" };
            label.SetBinding(TextBlock.TextProperty, extension.CreateBinding());
            try
            {
                foreach (var language in new[] { "en", "es", "fr", "de" })
                {
                    Localizer.SetLanguage(language);
                    Assert.Equal(Localizer.GetUiText("Connected", language), label.Text);
                }
            }
            finally { Localizer.SetLanguage(previous); }
        });
    }

    [Fact]
    public void Formatted_labels_refresh_across_all_languages_without_changing_data()
    {
        RunOnSta(() =>
        {
            var previous = Localizer.Language;
            var userName = "User with spaces";
            var label = new TextBlock { DataContext = new { Name = userName } };
            var extension = new LocBindingExtension("Name") { Template = "Connected as {0}" };
            label.SetBinding(TextBlock.TextProperty, extension.CreateBinding());
            try
            {
                foreach (var language in new[] { "en", "es", "fr", "de", "en" })
                {
                    Localizer.SetLanguage(language);
                    Assert.Equal(string.Format(Localizer.GetUiText("Connected as {0}", language), userName), label.Text);
                    Assert.Contains(userName, label.Text);
                    Assert.NotNull(BindingOperations.GetMultiBinding(label, TextBlock.TextProperty));
                }
            }
            finally { Localizer.SetLanguage(previous); }
        });
    }

    [Fact]
    public void Updated_control_labels_are_not_replaced_with_their_old_translation()
    {
        RunOnSta(() =>
        {
            var previous = Localizer.Language;
            var text = new TextBlock { Text = "Allow" };
            try
            {
                Localizer.SetLanguage("es");
                Localizer.ApplyToElement(text);
                text.Text = "Deny";
                Localizer.SetLanguage("de");
                Localizer.ApplyToElement(text);
                Assert.Equal(Localizer.GetUiText("Deny", "de"), text.Text);
            }
            finally { Localizer.SetLanguage(previous); }
        });
    }

    [Fact]
    public void Existing_controls_follow_theme_changes_even_with_frozen_brushes()
    {
        RunOnSta(() =>
        {
            XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
            XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";
            var dictionary = XDocument.Load(Path.Combine(Root(), "Aurora.App", "App.xaml"))
                .Descendants(presentation + "ResourceDictionary").First();
            dictionary.SetAttributeValue(XNamespace.Xmlns + "x", xaml.NamespaceName);
            var resources = (ResourceDictionary)XamlReader.Parse(dictionary.ToString());
            foreach (var key in resources.Keys.Cast<object>().ToArray())
                if (resources[key] is SolidColorBrush brush && brush.CanFreeze) brush.Freeze();
            var window = new Window { Width = 400, Height = 300, Resources = resources };
            var button = new Button { Content = "Send" };
            var combo = new ComboBox { ItemsSource = new[] { "One", "Two" }, SelectedIndex = 0 };
            var panel = new StackPanel();
            panel.Children.Add(button);
            panel.Children.Add(combo);
            window.Content = panel;
            window.SetResourceReference(Control.BackgroundProperty, "BgDeepTranslucentBrush");
            try
            {
                window.Show();
                foreach (var theme in ThemeCatalog.Themes)
                {
                    ThemeCatalog.Apply(theme.Name, resources);
                    window.UpdateLayout();
                    Assert.Equal((Color)ColorConverter.ConvertFromString(theme.BgCard), ((SolidColorBrush)button.Background).Color);
                    Assert.Equal((Color)ColorConverter.ConvertFromString(theme.BgDeep), ((SolidColorBrush)combo.Background).Color);
                    var background = ((SolidColorBrush)window.Background).Color;
                    Assert.Equal(0xE6, background.A);
                    Assert.Equal(((Color)ColorConverter.ConvertFromString(theme.BgDeep)).R, background.R);
                    Assert.NotNull(combo.Template.FindName("PART_EditableTextBox", combo));
                }
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public void Literal_Xaml_labels_have_supported_translations_or_are_technical_identifiers()
    {
        var unchanged = new HashSet<string>(StringComparer.Ordinal)
        { "alloy", "echo", "fable", "onyx", "nova", "shimmer", "Spotify", "filesystem", "npx", "-y @modelcontextprotocol/server-filesystem C:\\\\Projects" };
        foreach (var file in Directory.EnumerateFiles(Path.Combine(Root(), "Aurora.App"), "*.xaml", SearchOption.AllDirectories))
        foreach (var attribute in XDocument.Load(file).Descendants().Attributes().Where(a =>
            new[] { "Text", "Content", "Header", "Title", "ToolTip", "AutomationProperties.Name" }.Contains(a.Name.LocalName)))
        {
            var text = attribute.Value;
            if (text.StartsWith('{') || !text.Any(char.IsLetter) || unchanged.Contains(text) || text.StartsWith("-y @modelcontextprotocol/") || text.All(c => !char.IsLetter(c) || char.IsUpper(c))) continue;
            foreach (var language in new[] { "es", "fr", "de" })
                Assert.True(Localizer.HasUiTranslation(text, language), $"Missing {language} translation in {Path.GetFileName(file)}: {text}");
        }
    }

    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "Aurora.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException();
    }
    private static void RunOnSta(Action action)
    {
        Exception? error = null;
        var thread = new Thread(() => { try { action(); } catch (Exception ex) { error = ex; } }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)));
        if (error != null) ExceptionDispatchInfo.Capture(error).Throw();
    }
}

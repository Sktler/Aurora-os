using System.IO;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;
using System.Xml.Linq;
using Aurora.App;
using Aurora.App.Services;
using Aurora.App.ViewModels;
using Aurora.App.Views;
using Xunit;

namespace AuroraUpdater.Tests;

[Collection("App.Settings")]
public sealed class DemoWindowRegressionTests
{
    [Fact]
    public void Welcome_keeps_readable_theme_and_translated_continue()
    {
        RunOnSta(() =>
        {
            using var scope = new AppSettingsTestScope();
            var resources = LoadResources();
            var window = new FirstRunWindow { Resources = resources, Topmost = false, ShowInTaskbar = false };
            try
            {
                window.Show();
                var picker = Descendants<LanguagePicker>(window).Single();
                var next = Descendants<Button>(window).Single();
                foreach (var theme in ThemeCatalog.Themes)
                {
                    ThemeCatalog.Apply(theme.Name, resources);
                    foreach (var language in new[] { "en", "es", "fr", "de" })
                    {
                        Localizer.SetLanguage(language);
                        Localizer.ApplyToWindow(window);
                        window.UpdateLayout();
                        Assert.Equal((Color)ColorConverter.ConvertFromString(theme.BgDeep), ((SolidColorBrush)window.Background).Color);
                        Assert.Equal((Color)ColorConverter.ConvertFromString(theme.TextPrimary), ((SolidColorBrush)window.Foreground).Color);
                        Assert.Equal((Color)ColorConverter.ConvertFromString(theme.BgDeep), ((SolidColorBrush)picker.Background).Color);
                        Assert.NotNull(picker.Template.FindName("PART_Popup", picker));
                        Assert.Equal(Localizer.GetUiText("Continue", language), next.Content);
                        if (language != "en") Assert.NotEqual("Continue", next.Content);
                    }
                }
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public void Settings_tiles_refresh_when_the_window_owns_its_navigation()
    {
        RunOnSta(() =>
        {
            using var scope = new AppSettingsTestScope();
            SetService(nameof(App.SmartThings), new SmartThingsClient(""));
            SetService(nameof(App.HomeAssistant), new HomeAssistantClient("", ""));
            SetService(nameof(App.Hubitat), new HubitatClient("", ""));
            SetService(nameof(App.AI), new GeminiClient("", AIProviderCatalog.Get("gemini").DefaultModel));
            using var viewModel = new IntegrationsViewModel([]);
            var entry = viewModel.SettingsNavigation.Entries[0];
            var label = new TextBlock();
            label.SetBinding(TextBlock.TextProperty, new Binding(nameof(SettingsNavigationEntry.Title)) { Source = entry });
            foreach (var language in new[] { "en", "es", "fr", "de", "en" })
            {
                Localizer.SetLanguage(language);
                Assert.Equal(Localizer.GetUiText("AI engine", language), label.Text);
            }
        });
    }

    [Fact]
    public void Profile_template_and_speaker_toggle_translate_after_loading()
    {
        RunOnSta(() =>
        {
            using var scope = new AppSettingsTestScope();
            var profiles = new ProfileManager();
            SetService(nameof(App.Profiles), profiles);
            profiles.Initialize(App.Settings);
            var window = new ProfileWindow { Resources = LoadResources(), ShowInTaskbar = false };
            try
            {
                window.Show();
                window.UpdateLayout();
                var delete = Descendants<Button>(window).Single(button => BindingOperations.GetBinding(button, ContentControl.ContentProperty)?.Path.Path == "[Delete]");
                var suggestions = (CheckBox)window.FindName("SpeakerSuggestionsBox");
                foreach (var language in new[] { "en", "es", "fr", "de", "en" })
                {
                    Localizer.SetLanguage(language);
                    Assert.Equal(Localizer.GetUiText("Delete", language), delete.Content);
                    Assert.Equal(Localizer.GetUiText("Enable speaker suggestions", language), suggestions.Content);
                }
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public void Dashboard_composer_accepts_the_shift_enter_editing_command()
    {
        RunOnSta(() =>
        {
            var source = XDocument.Load(Path.Combine(Root(), "Aurora.App", "Views", "MainWindow.xaml"));
            XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";
            var composer = new XElement(source.Descendants().Single(element => (string?)element.Attribute(xaml + "Name") == "ComposerTextBox"));
            composer.Attribute(xaml + "Name")!.Remove();
            var box = (TextBox)XamlReader.Parse(composer.ToString());
            var window = new Window { Content = box, Resources = LoadResources(), Width = 500, Height = 200, ShowInTaskbar = false };
            try
            {
                window.Show();
                box.Focus();
                box.Text = "first line";
                box.CaretIndex = box.Text.Length;
                Assert.True(EditingCommands.EnterLineBreak.CanExecute(null, box));
                EditingCommands.EnterLineBreak.Execute(null, box);
                Assert.Contains("\n", box.Text);
            }
            finally { window.Close(); }
        });
    }

    private static void SetService(string name, object value) => typeof(App).GetProperty(name)!.SetValue(null, value);

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        if (root is T result) yield return result;
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
            foreach (var child in Descendants<T>(VisualTreeHelper.GetChild(root, index))) yield return child;
    }

    private static ResourceDictionary LoadResources()
    {
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";
        var dictionary = XDocument.Load(Path.Combine(Root(), "Aurora.App", "App.xaml"))
            .Descendants(presentation + "ResourceDictionary").First();
        dictionary.SetAttributeValue(XNamespace.Xmlns + "x", xaml.NamespaceName);
        return (ResourceDictionary)XamlReader.Parse(dictionary.ToString());
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
        var thread = new Thread(() =>
        {
            var previousLanguage = Localizer.Language;
            try { action(); }
            catch (Exception ex) { error = ex; }
            finally { Localizer.SetLanguage(previousLanguage); }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "The WPF regression check did not complete.");
        if (error != null) ExceptionDispatchInfo.Capture(error).Throw();
    }
}

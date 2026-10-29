using System.IO;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Interop;
using System.Xml.Linq;
using Aurora.App.Services;
using Aurora.App.Views;
using Xunit;

namespace AuroraUpdater.Tests;

[CollectionDefinition("Localization UI", DisableParallelization = true)]
public sealed class LocalizationUiCollection { }

[Collection("Localization UI")]
public sealed class LocalizedDialogLayoutTests
{
    [Fact]
    public void Existing_dialog_labels_translate_from_english_and_back_without_replacing_bindings()
    {
        RunOnSta(() =>
        {
            var previousLanguage = Localizer.Language;
            var heading = new TextBlock { Text = "Aurora AI Provider Setup" };
            var save = new Button { Content = "Save and Continue" };
            var userText = new TextBlock();
            var binding = new Binding("Value") { Source = new { Value = "Cancel" } };
            userText.SetBinding(TextBlock.TextProperty, binding);
            var panel = new StackPanel();
            panel.Children.Add(heading);
            panel.Children.Add(save);
            panel.Children.Add(userText);
            var window = new Window { Title = "Aurora Setup", Content = panel };

            try
            {
                foreach (var language in new[] { "en", "es", "fr", "de", "en" })
                {
                    Localizer.SetLanguage(language);
                    Localizer.ApplyToWindow(window);

                    Assert.Equal(Localizer.GetUiText("Aurora AI Provider Setup", language), heading.Text);
                    Assert.Equal(Localizer.GetUiText("Save and Continue", language), save.Content);
                    Assert.Equal(Localizer.GetUiText("Aurora Setup", language), window.Title);
                    Assert.Same(binding, BindingOperations.GetBinding(userText, TextBlock.TextProperty));
                    Assert.Equal("Cancel", userText.Text);
                }

                // A label that becomes bound after translation must keep its binding.
                heading.SetBinding(TextBlock.TextProperty, binding);
                Localizer.SetLanguage("de");
                Localizer.ApplyToWindow(window);
                Assert.Same(binding, BindingOperations.GetBinding(heading, TextBlock.TextProperty));
                Assert.Equal("Cancel", heading.Text);
            }
            finally
            {
                window.Close();
                Localizer.SetLanguage(previousLanguage);
            }
        });
    }

    [Fact]
    public void Translated_dialog_content_grows_and_scrolls_when_height_is_limited()
    {
        RunOnSta(() =>
        {
            var previousLanguage = Localizer.Language;
            var heading = new TextBlock
            {
                Text = "Aurora AI Provider Setup",
                FontSize = 22,
                TextWrapping = TextWrapping.Wrap
            };
            var window = new Window { Width = 320, Content = heading };
            DialogLayout.UseContentHeight(window);
            var scroller = Assert.IsType<ScrollViewer>(window.Content);

            try
            {
                Localizer.SetLanguage("en");
                Localizer.ApplyToWindow(window);
                scroller.Measure(new Size(320, double.PositiveInfinity));
                var englishHeight = scroller.DesiredSize.Height;

                Localizer.SetLanguage("es");
                Localizer.ApplyToWindow(window);
                scroller.Measure(new Size(320, double.PositiveInfinity));
                Assert.True(scroller.DesiredSize.Height > englishHeight);
                Assert.Equal(320, window.Width);
                Assert.Equal(SizeToContent.Height, window.SizeToContent);
                Assert.Equal(ResizeMode.CanResize, window.ResizeMode);

                scroller.Measure(new Size(320, englishHeight));
                scroller.Arrange(new Rect(0, 0, 320, englishHeight));
                scroller.UpdateLayout();
                Assert.True(scroller.ScrollableHeight > 0);
                Assert.Equal(ScrollBarVisibility.Disabled, scroller.HorizontalScrollBarVisibility);

                Localizer.SetLanguage("en");
                Localizer.ApplyToWindow(window);
                scroller.Measure(new Size(320, double.PositiveInfinity));
                Assert.Equal(englishHeight, scroller.DesiredSize.Height);

                window.SizeToContent = SizeToContent.Manual;
                window.Width = 400;
                window.Height = 240;
                Localizer.SetLanguage("de");
                Localizer.ApplyToWindow(window);
                Assert.Equal(400, window.Width);
                Assert.Equal(240, window.Height);
                Assert.Equal(SizeToContent.Manual, window.SizeToContent);
            }
            finally
            {
                window.Close();
                Localizer.SetLanguage(previousLanguage);
            }
        });
    }

    [Fact]
    public void Monitor_bounds_limit_large_minimum_sizes_without_enabling_content_sizing()
    {
        RunOnSta(() =>
        {
            var window = new Window { Width = 720, Height = 840, MinWidth = 10000, MinHeight = 10000 };
            try
            {
                DialogLayout.ConstrainToWorkArea(window);
                new WindowInteropHelper(window).EnsureHandle();

                Assert.True(double.IsFinite(window.MaxWidth));
                Assert.True(double.IsFinite(window.MaxHeight));
                Assert.InRange(window.MinWidth, 0, window.MaxWidth);
                Assert.InRange(window.MinHeight, 0, window.MaxHeight);
                Assert.Equal(SizeToContent.Manual, window.SizeToContent);
            }
            finally { window.Close(); }
        });
    }

    [Theory]
    [InlineData("SetupWindow.xaml", "SaveContinueButton")]
    [InlineData("RecoveryCodeWindow.xaml", "DoneButton")]
    [InlineData("ProfileWindow.xaml", "AddProfile_Click")]
    [InlineData("CapabilitiesWindow.xaml", "RefreshCameras_Click")]
    [InlineData("ChatWindow.xaml", "{Binding ApplyProviderModelCommand}")]
    [InlineData("MemoryWindow.xaml", "{Binding DeleteFolderCommand}")]
    public void Translated_action_rows_wrap_at_the_available_width(string file, string action)
    {
        var document = ReadView(file);
        var button = document.Descendants(Presentation + "Button").Single(element =>
            element.Attributes().Any(attribute => attribute.Value == action));

        Assert.Equal(Presentation + "WrapPanel", button.Parent?.Name);
    }

    [Fact]
    public void Setup_heading_and_language_picker_have_separate_columns()
    {
        var document = ReadView("SetupWindow.xaml");
        var heading = document.Descendants(Presentation + "TextBlock").Single(element =>
            (string?)element.Attribute(Xaml + "Name") == "HeaderText");
        var picker = document.Descendants(Presentation + "ComboBox").Single(element =>
            (string?)element.Attribute(Xaml + "Name") == "LanguageBox");

        Assert.Same(heading.Parent, picker.Parent);
        Assert.Equal("1", (string?)picker.Attribute("Grid.Column"));
        Assert.Equal("Wrap", (string?)heading.Attribute("TextWrapping"));
        Assert.Equal(2, heading.Parent!.Element(Presentation + "Grid.ColumnDefinitions")!.Elements().Count());
    }

    [Fact]
    public void Settings_tiles_have_no_fixed_height_that_could_clip_translations()
    {
        var document = ReadView("IntegrationsWindow.xaml");
        var tile = document.Descendants(Presentation + "Button").Single(element =>
            (string?)element.Attribute("Style") == "{StaticResource HubTileStyle}");

        Assert.Null(tile.Attribute("Height"));
        Assert.Null(tile.Parent?.Attribute("Height"));
        var style = document.Descendants(Presentation + "Style").Single(element =>
            (string?)element.Attribute(Xaml + "Key") == "HubTileStyle");
        Assert.DoesNotContain(style.Elements(Presentation + "Setter"), setter =>
            (string?)setter.Attribute("Property") == "Height");
    }

    [Fact]
    public void Lockdown_recovery_scrolls_and_translated_notices_wrap()
    {
        var document = ReadView("LockdownWindow.xaml");
        var recoveryButton = document.Descendants(Presentation + "Button").Single();
        Assert.Contains(recoveryButton.Ancestors(Presentation + "ScrollViewer"), scroller =>
            (string?)scroller.Attribute("VerticalScrollBarVisibility") == "Auto");
        var title = document.Descendants(Presentation + "TextBlock").Single(element =>
            (string?)element.Attribute("Text") == "AURORA SECURITY LOCKDOWN");
        Assert.Equal("Wrap", (string?)title.Attribute("TextWrapping"));
    }

    private static readonly XNamespace Presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

    private static XDocument ReadView(string file)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "Aurora.sln")))
            directory = directory.Parent;
        return XDocument.Load(Path.Combine(directory?.FullName ?? throw new DirectoryNotFoundException(), "Aurora.App", "Views", file));
    }

    private static void RunOnSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception ex) { failure = ex; }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(10)), "The WPF layout check did not complete.");
        if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}

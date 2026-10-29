using System.IO;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml.Linq;

// Load only presentation resources, never Aurora's startup code or user settings.
internal static class Program
{
    private static int failures;

    [STAThread]
    private static int Main(string[] args)
    {
        var repo = Path.GetFullPath(args.Length > 0 ? args[0] : ".");
        bool baseline = args.Contains("--baseline");
        var output = Path.Combine(repo, "artifacts", "menu-theme", baseline ? "before" : "after");
        Directory.CreateDirectory(output);
        XNamespace wpf = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var appXml = baseline ? XDocument.Parse(ReadBaseline(repo))
            : XDocument.Load(Path.Combine(repo, "Aurora.App", "App.xaml"));
        var resources = new XElement(appXml.Descendants(wpf + "ResourceDictionary").First());
        resources.SetAttributeValue(XNamespace.Xmlns + "x", x.NamespaceName);
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Resources = (ResourceDictionary)XamlReader.Parse(resources.ToString());

        // Use the real composer menu declarations, omitting only code-behind handlers.
        var windowXml = XDocument.Load(Path.Combine(repo, "Aurora.App", "Views", "MainWindow.xaml"));
        var menuXml = new XElement(windowXml.Descendants(wpf + "ContextMenu").Single());
        menuXml.SetAttributeValue(XNamespace.Xmlns + "x", x.NamespaceName);
        menuXml.Attribute(x + "Key")?.Remove();
        foreach (var click in menuXml.Descendants().Attributes("Click").ToList()) click.Remove();
        var composer = (ContextMenu)XamlReader.Parse(menuXml.ToString());
        CheckMenu(composer, "composer", output);

        // Match ProfileMenu_Click's dynamically-created controls: no explicit style.
        var profile = new ContextMenu();
        profile.Items.Add(new MenuItem { Header = "👥  Profiles" });
        profile.Items.Add(new MenuItem { Header = "✎  Change name" });
        CheckMenu(profile, "profile", output);
        CheckHeaderContent(output);
        app.Shutdown();
        Console.WriteLine($"{(failures == 0 ? "PASS" : "FAIL")}: {failures} failed checks. Rendered menus: {output}");
        return failures == 0 ? 0 : 1;
    }

    private static void CheckMenu(ContextMenu menu, string name, string output)
    {
        Layout(menu);
        var items = menu.Items.OfType<MenuItem>().ToList();
        var first = items.First();
        var presenter = VisualChildren<ContentPresenter>(first).Single(p => p.ContentSource == "Header");
        var text = VisualChildren<TextBlock>(presenter).First(t => t.Visibility == Visibility.Visible
            && t.ActualWidth > 0 && (!string.IsNullOrEmpty(t.Text) || t.Inlines.Count > 0));
        Check(text.Foreground.ToString() == first.Foreground.ToString(),
            $"{name}: rendered normal text follows item foreground");
        Check(Contrast(text.Foreground, menu.Background) >= 4.5,
            $"{name}: rendered text {text.Foreground}, menu background {menu.Background} have >= 4.5:1 contrast");
        Save(menu, Path.Combine(output, name + "-normal.png"));

        // IsHighlighted is the state set by WPF for pointer/keyboard navigation.
        // The protected setter uses WPF's read-only property key. No input is sent
        // to the user's desktop; this exercises the same template state offscreen.
        typeof(MenuItem).GetProperty(nameof(MenuItem.IsHighlighted))!.SetValue(first, true);
        Layout(menu);
        var border = first.Template.FindName("ItemBorder", first) as Border;
        Check(border != null, $"{name}: shared item template applied");
        if (border != null)
            Check(Contrast(text.Foreground, border.Background) >= 4.5,
                $"{name}: highlighted foreground/background have >= 4.5:1 contrast");
        Check(text.Foreground.ToString() == first.Foreground.ToString(),
            $"{name}: rendered highlighted text follows item foreground");
        Save(menu, Path.Combine(output, name + "-highlighted.png"));

        first.IsEnabled = false;
        Layout(menu);
        var source = DependencyPropertyHelper.GetValueSource(text, TextBlock.ForegroundProperty);
        Console.WriteLine($"{name}: disabled header foreground source: {source.BaseValueSource} (expression: {source.IsExpression}); "
            + $"presenter foreground: {System.Windows.Documents.TextElement.GetForeground(presenter)}");
        Check(first.Foreground.ToString() == Application.Current.Resources["TextMutedBrush"].ToString(),
            $"{name}: disabled item uses TextMutedBrush");
        if (border != null)
        {
            Check(Contrast(text.Foreground, border.Background) >= 4.5,
                $"{name}: disabled foreground/background remain readable");
            Check(text.Foreground.ToString() == first.Foreground.ToString(),
                $"{name}: rendered disabled text {text.Foreground} follows item foreground {first.Foreground}");
        }
        Save(menu, Path.Combine(output, name + "-disabled.png"));
        first.IsEnabled = true;
        typeof(MenuItem).GetProperty(nameof(MenuItem.IsHighlighted))!.SetValue(first, false);
        Layout(menu);
        Check(text.Foreground.ToString() == first.Foreground.ToString()
            && first.Foreground.ToString() == Application.Current.Resources["TextPrimaryBrush"].ToString(),
            $"{name}: re-enabled header returns to TextPrimaryBrush");

        bool clicked = false;
        first.Click += (_, _) => clicked = true;
        first.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        Check(clicked, $"{name}: click event routing remains available");

        foreach (var separator in menu.Items.OfType<Separator>())
        {
            var line = VisualChildren<Border>(separator).FirstOrDefault();
            Check(line?.Background?.ToString() == "#FF3A4356",
                $"{name}: separator uses the shared dark-theme border brush");
        }
    }

    private static void CheckHeaderContent(string output)
    {
        var menu = (ContextMenu)XamlReader.Parse("""
            <ContextMenu xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
                <MenuItem Header="_Öffnen" InputGestureText="Ctrl+O">
                    <MenuItem.Icon><TextBlock Text="★" Foreground="Orange"/></MenuItem.Icon>
                    <MenuItem Header="Child"/>
                </MenuItem>
                <MenuItem><MenuItem.Header><TextBlock Text="名前を変更"/></MenuItem.Header></MenuItem>
                <MenuItem Header="Profil ändern">
                    <MenuItem.HeaderTemplate><DataTemplate><TextBlock Text="{Binding}"/></DataTemplate></MenuItem.HeaderTemplate>
                </MenuItem>
                <MenuItem><MenuItem.Header><TextBlock Text="Custom color" Foreground="Orange"/></MenuItem.Header></MenuItem>
            </ContextMenu>
            """);
        CheckMenu(menu, "custom-headers", output);
        var first = (MenuItem)menu.Items[0];
        var accessText = VisualChildren<AccessText>(first).Single();
        Check(char.ToUpperInvariant(accessText.AccessKey) == 'Ö' && (string)first.Header == "_Öffnen",
            "custom headers: localized access key is preserved");
        Check(VisualChildren<TextBlock>(first).Any(t => t.Text == "Ctrl+O")
            && VisualChildren<TextBlock>(first).Any(t => t.Text == "★" && t.Foreground.ToString() == Brushes.Orange.ToString()),
            "custom headers: input gesture and custom icon are preserved");
        var popup = (Popup)first.Template.FindName("PART_Popup", first);
        var arrow = (System.Windows.Shapes.Path)first.Template.FindName("SubmenuArrow", first);
        Check(first.HasItems && arrow.Visibility == Visibility.Visible && popup.Child != null
            && System.Windows.Data.BindingOperations.IsDataBound(popup, Popup.IsOpenProperty),
            "custom headers: submenu arrow, content and open binding are preserved");
        var presenter = VisualChildren<ContentPresenter>(first).Single(p => p.ContentSource == "Header");
        Check(first.Focusable && presenter.RecognizesAccessKey && !presenter.IsHitTestVisible
            && KeyboardNavigation.GetDirectionalNavigation(VisualChildren<ItemsPresenter>(menu).First()) == KeyboardNavigationMode.Cycle,
            "custom headers: menu focus and keyboard navigation settings are preserved");

        var originalMuted = Application.Current.Resources["TextMutedBrush"];
        try
        {
            // Replace the resource to cover live theme changes, including frozen brushes.
            Application.Current.Resources["TextMutedBrush"] = new SolidColorBrush(Color.FromRgb(201, 214, 242));
            foreach (var item in menu.Items.OfType<MenuItem>()) item.IsEnabled = false;
            Layout(menu);
            foreach (var item in menu.Items.OfType<MenuItem>().Take(3))
            {
                var header = VisualChildren<ContentPresenter>(item).Single(p => p.ContentSource == "Header");
                var text = VisualChildren<TextBlock>(header).First(t => !string.IsNullOrEmpty(t.Text) || t.Inlines.Count > 0);
                Check(text.Foreground.ToString() == "#FFC9D6F2" && text.Foreground.ToString() == item.Foreground.ToString(),
                    $"custom headers: disabled header {menu.Items.IndexOf(item)} follows the updated theme brush");
            }
            var explicitHeader = (TextBlock)((MenuItem)menu.Items[3]).Header;
            Check(explicitHeader.Foreground.ToString() == Brushes.Orange.ToString(),
                "custom headers: explicit header foreground is preserved");
        }
        finally
        {
            Application.Current.Resources["TextMutedBrush"] = originalMuted;
        }
    }

    private static string ReadBaseline(string repo)
    {
        using var process = Process.Start(new ProcessStartInfo("git", "show HEAD:Aurora.App/App.xaml")
        {
            WorkingDirectory = repo, RedirectStandardOutput = true,
            UseShellExecute = false, CreateNoWindow = true
        })!;
        var text = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0) throw new InvalidOperationException("Cannot read baseline App.xaml.");
        return text;
    }

    private static IEnumerable<T> VisualChildren<T>(DependencyObject root) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match) yield return match;
            foreach (var descendant in VisualChildren<T>(child)) yield return descendant;
        }
    }

    private static void Layout(FrameworkElement element)
    {
        element.ApplyTemplate();
        element.Measure(new Size(400, 1000));
        element.Arrange(new Rect(element.DesiredSize));
        element.UpdateLayout();
    }

    private static void Save(FrameworkElement element, string path)
    {
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(element.ActualWidth),
            (int)Math.Ceiling(element.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(element);
        var png = new PngBitmapEncoder();
        png.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path);
        png.Save(stream);
    }

    private static double Contrast(Brush foreground, Brush background)
    {
        if (foreground is not SolidColorBrush fg || background is not SolidColorBrush bg) return 0;
        static double Luminance(Color c)
        {
            static double Linear(byte channel)
            {
                double s = channel / 255.0;
                return s <= 0.04045 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
            }
            return 0.2126 * Linear(c.R) + 0.7152 * Linear(c.G) + 0.0722 * Linear(c.B);
        }
        var a = Luminance(fg.Color);
        var b = Luminance(bg.Color);
        return (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);
    }

    private static void Check(bool passed, string message)
    {
        Console.WriteLine($"{(passed ? "PASS" : "FAIL")} {message}");
        if (!passed) failures++;
    }
}

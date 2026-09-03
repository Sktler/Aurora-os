using System.IO;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
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
            : XDocument.Load(Path.Combine(repo, "ZoeyOS.App", "App.xaml"));
        var resources = new XElement(appXml.Descendants(wpf + "ResourceDictionary").First());
        resources.SetAttributeValue(XNamespace.Xmlns + "x", x.NamespaceName);
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Resources = (ResourceDictionary)XamlReader.Parse(resources.ToString());

        // Use the real composer menu declarations, omitting only code-behind handlers.
        var windowXml = XDocument.Load(Path.Combine(repo, "ZoeyOS.App", "Views", "MainWindow.xaml"));
        var menuXml = new XElement(windowXml.Descendants(wpf + "ContextMenu").Single());
        menuXml.SetAttributeValue(XNamespace.Xmlns + "x", x.NamespaceName);
        menuXml.Attribute(x + "Key")?.Remove();
        foreach (var click in menuXml.Descendants().Attributes("Click").ToList()) click.Remove();
        var composer = (ContextMenu)XamlReader.Parse(menuXml.ToString());
        CheckMenu(composer, "composer", output);

        // Match ProfileMenu_Click's dynamically-created controls: no explicit style.
        var profile = new ContextMenu();
        profile.Items.Add(new MenuItem { Header = "Settings" });
        profile.Items.Add(new Separator());
        profile.Items.Add(new MenuItem { Header = "Tools & Capabilities" });
        CheckMenu(profile, "profile", output);
        app.Shutdown();
        Console.WriteLine($"{(failures == 0 ? "PASS" : "FAIL")}: {failures} failed checks. Rendered menus: {output}");
        return failures == 0 ? 0 : 1;
    }

    private static void CheckMenu(ContextMenu menu, string name, string output)
    {
        Layout(menu);
        var items = menu.Items.OfType<MenuItem>().ToList();
        var first = items.First();
        var text = VisualChildren<TextBlock>(first).First(t => t.Visibility == Visibility.Visible
            && t.ActualWidth > 0 && !string.IsNullOrEmpty(t.Text));
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
        Save(menu, Path.Combine(output, name + "-highlighted.png"));

        first.IsEnabled = false;
        Layout(menu);
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

    private static string ReadBaseline(string repo)
    {
        using var process = Process.Start(new ProcessStartInfo("git", "show HEAD:ZoeyOS.App/App.xaml")
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

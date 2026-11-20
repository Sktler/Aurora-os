using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
using System.Xml.Linq;
using Aurora.App;
using Aurora.App.Models;
using Aurora.App.Services;
using Aurora.App.ViewModels;
using Aurora.App.Views;
using AuroraUpdater.Tests;
using Screen = System.Windows.Forms.Screen;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        using var storage = new AppSettingsTestScope();
        App.Settings.LocalOnlyMode = true;
        var profiles = new ProfileManager();
        profiles.Initialize(App.Settings);
        using var memory = new MemoryStore(":memory:");
        memory.Initialize();
        SetService(nameof(App.Profiles), profiles);
        SetService(nameof(App.Memory), memory);
        SetService(nameof(App.Security), new InstallationSecurityService(Path.Combine(storage.DirectoryPath, "security.lock")));
        SetService(nameof(App.SmartThings), new SmartThingsClient(""));
        SetService(nameof(App.HomeAssistant), new HomeAssistantClient("", ""));
        SetService(nameof(App.Hubitat), new HubitatClient("", ""));
        SetService(nameof(App.AI), new GeminiClient("", AIProviderCatalog.Get("gemini").DefaultModel));

        var repository = Path.GetFullPath(args.FirstOrDefault() ?? ".");
        var app = new Application { Resources = LoadResources(repository), ShutdownMode = ShutdownMode.OnExplicitShutdown };
        EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent,
            new RoutedEventHandler((sender, _) => { if (sender is Window window) Localizer.ApplyToWindow(window); }));
        try
        {
            CheckSetupAndChat(profiles, args.Contains("--all-monitors"));
            CheckSettingsPasswordToggle();
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
        finally
        {
            foreach (Window window in app.Windows.Cast<Window>().ToArray()) window.Close();
            app.Shutdown();
        }
    }

    private static void CheckSetupAndChat(ProfileManager profiles, bool allMonitors)
    {
        const string profileName = "Disposable Profile";
        const string password = "disposable-demo-password";
        const string model = "disposable-model";
        var setup = new SetupWindow(restartOnSave: false) { ShowInTaskbar = false, Topmost = false };
        var chat = new ChatWindow(new CompanionViewModel(new Companion { Id = "demo-test" }))
            { ShowInTaskbar = false, ShowActivated = false };
        var provider = Find<ProviderSettingsView>(setup, "ProviderSettings").ViewModel;
        // The fixture key prevents Setup from reading the real clipboard on activation.
        provider.ApiKey = "unused-demo-test-key";
        provider.Model = model;
        var setupContent = (FrameworkElement)setup.Content;
        var chatContent = (FrameworkElement)chat.Content;
        using var emergencyStop = new EmergencyStopButtonService();
        chat.Show();
        var chatStop = emergencyStop.AttachToWindow(chat)!;

        string? recoveryCode = null;
        var check = setup.Dispatcher.InvokeAsync(() =>
        {
            try
            {
                var setupStop = emergencyStop.AttachToWindow(setup)!;
                var name = Find<TextBox>(setup, "ProfileNameBox");
                var save = Find<Button>(setup, "SaveContinueButton");
                var error = Find<TextBlock>(setup, "ErrorText");
                name.Clear();
                Click(save);
                Check(error.IsVisible, "Setup did not show the missing-name error.");
                foreach (var language in Localizer.SupportedLanguages)
                {
                    Localizer.SetLanguage(language.Code);
                    Pump();
                    Check(error.Text == Localizer.Get("Setup.ProfileNameRequired"),
                        $"Setup name validation did not refresh in {language.Code}.");
                    CheckStopAboveContent(setupStop, setupContent, "Setup");
                    CheckStopAboveContent(chatStop, chatContent, "Chat");
                }
                if (allMonitors) CheckEmergencyStopOnEachMonitor(setup, setupStop, setupContent);
                Console.WriteLine("PASS: Emergency Stop stays above Setup and Chat content in all four languages.");

                name.Text = profileName;
                Click(save);
                Localizer.SetLanguage("es");
                Check(error.Text == Localizer.Get("Setup.ProfilePasswordRequired") && name.Text == profileName,
                    "Changing language left stale password validation or changed the entered profile name.");
                Check(!setup.KeySaved, "Setup saved before a password was entered.");
                Localizer.SetLanguage("en");
                recoveryCode = GenerateRecoveryCode(setup);
                Find<PasswordBox>(setup, "ProfilePasswordBox").Password = password;
                Click(save);
            }
            finally { if (setup.IsVisible) setup.Close(); }
        }, DispatcherPriority.ApplicationIdle);

        var accepted = setup.ShowDialog();
        Pump();
        check.Task.GetAwaiter().GetResult();
        if (allMonitors) CheckEmergencyStopOnEachMonitor(chat, chatStop, chatContent);
        chat.Close();
        Check(accepted == true && setup.KeySaved, "Setup did not save the disposable profile.");
        var reopened = AppSettings.LoadOrCreate();
        Check(reopened.UserName == profileName && reopened.GeminiModel == model,
            "The profile name or custom model did not reload from settings.");
        Check(new ProfilePasswordStore().VerifyPassword(profiles.ActiveProfile.Id, password), "The saved password did not verify.");
        Check(recoveryCode != null && new ProfileRecoveryCodeStore().VerifyCode(profiles.ActiveProfile.Id, recoveryCode),
            "The generated recovery code did not verify after saving.");
        Console.WriteLine("PASS: Setup validation and the recovery dialog refresh live; the generated code, password, and profile settings persist.");
    }

    private static string? GenerateRecoveryCode(SetupWindow setup)
    {
        var generate = VisualChildren<Button>(setup).Single(button => Equals(button.Content, "Generate recovery code"));
        var readDialog = setup.Dispatcher.InvokeAsync(() =>
        {
            var dialog = setup.OwnedWindows.Cast<Window>().Single();
            try
            {
                var panel = (StackPanel)((ScrollViewer)dialog.Content).Content;
                var lead = (TextBlock)panel.Children[0];
                var body = (TextBlock)panel.Children[1];
                var code = panel.Children.OfType<TextBox>().Single();
                var original = code.Text;
                Check(AuroraRecoveryService.NormalizeRecoveryCode(original).Length == 32, "Setup generated an invalid recovery code.");
                foreach (var language in Localizer.SupportedLanguages)
                {
                    Localizer.SetLanguage(language.Code);
                    Check(dialog.Title == Localizer.Get("Setup.RecoveryTitle")
                        && lead.Text == Localizer.Get("Setup.RecoveryLead")
                        && body.Text == Localizer.Get("Setup.RecoveryBody")
                        && code.Text == original, $"Recovery dialog localization failed in {language.Code}.");
                }
                return original;
            }
            finally { dialog.Close(); }
        }, DispatcherPriority.ApplicationIdle);
        Click(generate);
        Pump();
        return readDialog.Task.GetAwaiter().GetResult();
    }

    private static void CheckSettingsPasswordToggle()
    {
        var settings = new IntegrationsWindow([]) { ShowInTaskbar = false };
        try
        {
            settings.Show();
            var toggle = Find<Button>(settings, "DeveloperPasswordToggleButton");
            Localizer.SetLanguage("fr");
            Check(Equals(toggle.Content, Localizer.GetUiText("Show")), "The password toggle did not start in French.");
            Click(toggle);
            Check(Equals(toggle.Content, Localizer.GetUiText("Hide")), "Revealing the password reset the toggle to English.");
            Localizer.SetLanguage("de");
            Check(Equals(toggle.Content, Localizer.GetUiText("Hide")), "The revealed state did not refresh in German.");
            Click(toggle);
            Check(Equals(toggle.Content, Localizer.GetUiText("Show")), "Masking the password reset the toggle to English.");
            Console.WriteLine("PASS: Settings preserves the password visibility label across clicks and language changes.");
        }
        finally { settings.Close(); }
    }

    private static void CheckStopAboveContent(Popup popup, FrameworkElement content, string name)
    {
        Check(popup.IsOpen, $"{name}: Emergency Stop was closed while the window was visible.");
        var root = popup.PlacementTarget;
        var child = popup.Child;
        var size = child.RenderSize;
        var topLeft = root.PointFromScreen(child.PointToScreen(new Point()));
        var bottomRight = root.PointFromScreen(child.PointToScreen(new Point(size.Width, size.Height)));
        Check(topLeft.X >= -1 && topLeft.Y >= -1 && bottomRight.X <= root.RenderSize.Width + 1
            && bottomRight.Y <= content.TranslatePoint(new Point(), root).Y + 1,
            $"{name}: Emergency Stop overlaps content or extends outside the window.");
    }

    private static void CheckEmergencyStopOnEachMonitor(Window window, Popup popup, FrameworkElement content)
    {
        var handle = new WindowInteropHelper(window).Handle;
        foreach (var monitor in Screen.AllScreens)
        {
            var clientDpi = VisualTreeHelper.GetDpi(window);
            var area = monitor.WorkingArea;
            var left = area.Left + Math.Max(0, (area.Width - (int)(window.ActualWidth * clientDpi.DpiScaleX)) / 2);
            var top = area.Top + Math.Max(0, (area.Height - (int)(window.ActualHeight * clientDpi.DpiScaleY)) / 2);
            const uint keepSizeOrderAndFocus = 0x0001 | 0x0004 | 0x0010;
            if (!SetWindowPos(handle, IntPtr.Zero, left, top, 0, 0, keepSizeOrderAndFocus))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            Pump();
            var name = window.GetType().Name;
            Check(Screen.FromHandle(handle).DeviceName == monitor.DeviceName, $"{name} did not move to the selected monitor.");
            CheckStopAboveContent(popup, content, name);
            Console.WriteLine($"PASS: {name} Emergency Stop on {monitor.Bounds.Width}x{monitor.Bounds.Height}, WPF scale {VisualTreeHelper.GetDpi(window).DpiScaleX:P0}.");
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr window, IntPtr insertAfter, int x, int y, int width, int height, uint flags);

    private static void SetService(string property, object value) => typeof(App).GetProperty(property)!.SetValue(null, value);
    private static T Find<T>(Window window, string name) => (T)window.FindName(name);
    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static void Pump() => Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.SystemIdle);

    private static ResourceDictionary LoadResources(string root)
    {
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";
        var dictionary = XDocument.Load(Path.Combine(root, "Aurora.App", "App.xaml")).Descendants(presentation + "ResourceDictionary").First();
        dictionary.SetAttributeValue(XNamespace.Xmlns + "x", xaml.NamespaceName);
        return (ResourceDictionary)XamlReader.Parse(dictionary.ToString());
    }

    private static IEnumerable<T> VisualChildren<T>(DependencyObject root) where T : DependencyObject
    {
        if (root is T child) yield return child;
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
            foreach (var match in VisualChildren<T>(VisualTreeHelper.GetChild(root, index))) yield return match;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

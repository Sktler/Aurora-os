using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Aurora.App.Services;

namespace Aurora.App.Views
{
    /// <summary>Runs live health checks across Aurora's major subsystems and shows plain-language results.</summary>
    public sealed class DiagnosticsWindow : Window
    {
        private readonly StackPanel _results = new();
        private readonly TextBlock _summary = new() { FontSize = 14, Margin = new Thickness(0, 0, 0, 10), TextWrapping = TextWrapping.Wrap };
        private readonly Button _run = new() { Content = "Run diagnostics again", Padding = new Thickness(14, 8, 14, 8), Margin = new Thickness(0, 0, 8, 0) };
        private readonly Button _copy = new() { Content = "Copy report", Padding = new Thickness(14, 8, 14, 8) };
        private string _report = "";

        public DiagnosticsWindow()
        {
            Title = "Aurora diagnostics";
            Width = 640;
            Height = 640;
            MinWidth = 480;
            MinHeight = 400;
            SetResourceReference(BackgroundProperty, "BgDeepBrush");
            SetResourceReference(ForegroundProperty, "TextPrimaryBrush");
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };
            buttons.Children.Add(_run);
            buttons.Children.Add(_copy);
            var scroll = new ScrollViewer { Content = _results, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var grid = new Grid { Margin = new Thickness(18) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.Children.Add(_summary);
            Grid.SetRow(scroll, 1);
            grid.Children.Add(scroll);
            Grid.SetRow(buttons, 2);
            grid.Children.Add(buttons);
            Content = grid;
            DialogLayout.ConstrainToWorkArea(this);

            _run.Click += async (_, _) => await RunAsync();
            _copy.Click += (_, _) => { try { Clipboard.SetText(_report); } catch { } };
            Loaded += async (_, _) => await RunAsync();
        }

        private async Task RunAsync()
        {
            _run.IsEnabled = false;
            _results.Children.Clear();
            _summary.Text = "Running checks...";
            var checks = new List<(string Name, string Status, string Detail, string Code)>();
            void Add(string name, string status, string detail, string code = "")
            {
                if (status == "OK") code = ""; checks.Add((name, status, detail, code));
                var color = status == "OK" ? Brushes.LightGreen : status == "WARN" ? Brushes.Orange : Brushes.Tomato;
                var row = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
                row.Children.Add(new TextBlock { Text = $"{(status == "OK" ? "âœ”" : status == "WARN" ? "!" : "âœ–")}  {name}", FontWeight = FontWeights.SemiBold, Foreground = color });
                row.Children.Add(new TextBlock { Text = detail, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(20, 2, 0, 0), Style = (Style)Application.Current.FindResource("MutedText") });
                _results.Children.Add(row);
            }

            try { await Task.Run(() => Probe(new AddCheck(Add))); } catch (Exception ex) { Add("Diagnostics", "FAIL", ex.Message, "AUR-900"); }
            await ProbeNetworkAsync(new AddCheck(Add));

            var fail = checks.Count(c => c.Status == "FAIL");
            var warn = checks.Count(c => c.Status == "WARN");
            _summary.Text = fail == 0 && warn == 0 ? "Everything checks out." : $"{fail} problem(s) and {warn} warning(s) found. Details below.";
            _report = BuildReport(checks);
            _run.IsEnabled = true;
        }


        private delegate void AddCheck(string name, string status, string detail, string code = "");

        private static readonly (string Code, string Meaning)[] Catalog =
        {
            ("AUR-100", "Settings folder is not readable or writable."),
            ("AUR-110", "Conversation memory database (aurora.db) not found."),
            ("AUR-120", "Emergency Stop is engaged; all AI keys are disabled."),
            ("AUR-130", "Local-only mode is on; cloud AI is disabled."),
            ("AUR-200", "No usable API key for the selected AI provider."),
            ("AUR-210", "AI provider test failed (unknown error)."),
            ("AUR-211", "AI provider rate limit or quota exceeded (HTTP 429)."),
            ("AUR-212", "AI provider rejected the key or access (HTTP 401/403)."),
            ("AUR-300", "Selected voice provider has no key; using Windows voice."),
            ("AUR-400", "Microphone permission is off in Aurora."),
            ("AUR-410", "Wake word listener is not running."),
            ("AUR-500", "No weather location or page URL set."),
            ("AUR-600", "Phone link server is not running."),
            ("AUR-700", "Cannot reach the internet."),
            ("AUR-701", "Internet check returned an unexpected HTTP status."),
            ("AUR-900", "Diagnostics itself crashed unexpectedly."),
        };

        private static string AiCode(string reply) =>
            reply.Contains("429") ? "AUR-211" : reply.Contains("401") || reply.Contains("403") ? "AUR-212" : "AUR-210";

        private static string BuildReport(List<(string Name, string Status, string Detail, string Code)> checks)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Aurora diagnostics report - {DateTime.Now:g}");
            sb.AppendLine();
            foreach (var c in checks)
                sb.AppendLine($"[{c.Status}]{(c.Code.Length > 0 ? " " + c.Code : "")} {c.Name}: {c.Detail}");
            var active = checks.Where(c => c.Code.Length > 0).Select(c => c.Code).Distinct().ToList();
            sb.AppendLine();
            sb.AppendLine("Error codes found this run: " + (active.Count == 0 ? "none" : string.Join(", ", active)));
            sb.AppendLine();
            sb.AppendLine("Full error code list:");
            foreach (var (code, meaning) in Catalog)
                sb.AppendLine($"  {code}{(active.Contains(code) ? " *" : "  ")} {meaning}");
            return sb.ToString();
        }
        private static void Probe(AddCheck add)
        {
            var s = App.Settings;
            try
            {
                Directory.CreateDirectory(AppSettings.ConfigDir);
                var probe = Path.Combine(AppSettings.ConfigDir, ".diag");
                File.WriteAllText(probe, "ok");
                File.Delete(probe);
                add("Settings folder", "OK", AppSettings.ConfigDir + " is readable and writable.");
            }
            catch (Exception ex) { add("Settings folder", "FAIL", ex.Message, "AUR-100"); }

            add("Memory database", File.Exists(Path.Combine(AppSettings.ConfigDir, "aurora.db")) ? "OK" : "WARN",
                "Conversation memory stored in aurora.db.", "AUR-110");

            if (App.Security?.IsLockedDown == true)
                add("Emergency stop", "FAIL", "Aurora is locked down by Emergency Stop, so every AI key is disabled. Restart Aurora or unlock it from the security settings.", "AUR-120");
            else
                add("Emergency stop", "OK", "Not engaged.");

            if (s.LocalOnlyMode)
                add("Local-only mode", "WARN", "On: cloud AI is disabled by design. Turn it off in Settings to chat with Gemini, OpenAI or Claude.", "AUR-130");

            var provider = string.IsNullOrWhiteSpace(s.ChatProvider) ? "gemini" : s.ChatProvider;
            var configured = App.AI?.IsConfigured == true;
            add($"AI provider ({provider})", configured ? "OK" : "FAIL",
                configured ? $"A key is loaded for {provider}." : $"No usable API key for {provider}. Open Settings, AI engine, and enter the key again.", "AUR-200");

            add("Voice output", s.TtsProvider is "windows" or "" ? "OK" : (HasTtsKey(s) ? "OK" : "WARN"),
                s.TtsProvider is "windows" or "" ? "Using the built-in Windows voice." : HasTtsKey(s) ? $"Using {s.TtsProvider}." : $"{s.TtsProvider} is selected but has no key, so Aurora falls back to the Windows voice.", "AUR-300");
            add("Microphone", s.WindowsMicrophoneEnabled == true ? "OK" : "WARN", s.WindowsMicrophoneEnabled == true ? "Permission granted in Aurora." : "Microphone permission is off in Aurora's capabilities.", "AUR-400");
            add("Wake word", App.WakeWord?.IsEnabled == true ? "OK" : "WARN", App.WakeWord == null ? "Not initialised." : App.WakeWord.Status, "AUR-410");

            var weatherSet = !string.IsNullOrWhiteSpace(s.WeatherLocation) || !string.IsNullOrWhiteSpace(s.WeatherPageUrl);
            add("Weather", weatherSet ? "OK" : "WARN", weatherSet ? $"Source: {s.WeatherSource}." : "No manual location; relies on Windows location permission.", "AUR-500");

            if (App.PhoneLink.IsRunning)
            {
                var addr = PhoneLinkService.GetLanAddresses().FirstOrDefault() ?? "no LAN address found";
                add("Phone link", "OK", $"Running on {addr}:{App.PhoneLink.Port}. {App.PhoneLink.PairedDeviceCount} phone(s) paired.");
            }
            else add("Phone link", "WARN", "Not started. Click Phone in the sidebar and press Start phone link.", "AUR-600");
        }

        private static bool HasTtsKey(AppSettings s) => s.TtsProvider switch
        {
            "openai" => !string.IsNullOrWhiteSpace(s.OpenAIApiKey),
            _ => true
        };

        private static async Task ProbeNetworkAsync(AddCheck add)
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
            try
            {
                using var r = await http.GetAsync("https://www.gstatic.com/generate_204");
                add("Internet", r.IsSuccessStatusCode ? "OK" : "WARN", $"HTTP {(int)r.StatusCode}.", "AUR-701");
            }
            catch (Exception ex) { add("Internet", "FAIL", "Cannot reach the internet: " + ex.Message, "AUR-700"); return; }

            if (App.AI?.IsConfigured != true || App.Settings.LocalOnlyMode) return;
            try
            {
                var reply = await App.AI.SendAsync("Reply with the single word OK.", new List<Aurora.App.Models.ChatMessage>(), "ping");
                var bad = reply.Contains("request failed") || reply.StartsWith("[");
                add("AI provider live test", bad ? "FAIL" : "OK", bad ? reply : "The provider answered a test message.", AiCode(reply));
            }
            catch (Exception ex) { add("AI provider live test", "FAIL", ex.Message, "AUR-210"); }
        }
    }
}



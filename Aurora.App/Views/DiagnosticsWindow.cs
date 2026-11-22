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
            var checks = new List<(string Name, string Status, string Detail)>();
            void Add(string name, string status, string detail)
            {
                checks.Add((name, status, detail));
                var color = status == "OK" ? Brushes.LightGreen : status == "WARN" ? Brushes.Orange : Brushes.Tomato;
                var row = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
                row.Children.Add(new TextBlock { Text = $"{(status == "OK" ? "✔" : status == "WARN" ? "!" : "✖")}  {name}", FontWeight = FontWeights.SemiBold, Foreground = color });
                row.Children.Add(new TextBlock { Text = detail, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(20, 2, 0, 0), Style = (Style)Application.Current.FindResource("MutedText") });
                _results.Children.Add(row);
            }

            try { await Task.Run(() => Probe(Add)); } catch (Exception ex) { Add("Diagnostics", "FAIL", ex.Message); }
            await ProbeNetworkAsync(Add);

            var fail = checks.Count(c => c.Status == "FAIL");
            var warn = checks.Count(c => c.Status == "WARN");
            _summary.Text = fail == 0 && warn == 0 ? "Everything checks out." : $"{fail} problem(s) and {warn} warning(s) found. Details below.";
            _report = string.Join(Environment.NewLine, checks.Select(c => $"[{c.Status}] {c.Name}: {c.Detail}"));
            _run.IsEnabled = true;
        }

        private static void Probe(Action<string, string, string> add)
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
            catch (Exception ex) { add("Settings folder", "FAIL", ex.Message); }

            add("Memory database", File.Exists(Path.Combine(AppSettings.ConfigDir, "aurora.db")) ? "OK" : "WARN",
                "Conversation memory stored in aurora.db.");

            if (App.Security?.IsLockedDown == true)
                add("Emergency stop", "FAIL", "Aurora is locked down by Emergency Stop, so every AI key is disabled. Restart Aurora or unlock it from the security settings.");
            else
                add("Emergency stop", "OK", "Not engaged.");

            if (s.LocalOnlyMode)
                add("Local-only mode", "WARN", "On: cloud AI is disabled by design. Turn it off in Settings to chat with Gemini, OpenAI or Claude.");

            var provider = string.IsNullOrWhiteSpace(s.ChatProvider) ? "gemini" : s.ChatProvider;
            var configured = App.AI?.IsConfigured == true;
            add($"AI provider ({provider})", configured ? "OK" : "FAIL",
                configured ? $"A key is loaded for {provider}." : $"No usable API key for {provider}. Open Settings, AI engine, and enter the key again.");

            add("Voice output", s.TtsProvider is "windows" or "" ? "OK" : (HasTtsKey(s) ? "OK" : "WARN"),
                s.TtsProvider is "windows" or "" ? "Using the built-in Windows voice." : HasTtsKey(s) ? $"Using {s.TtsProvider}." : $"{s.TtsProvider} is selected but has no key, so Aurora falls back to the Windows voice.");
            add("Microphone", s.WindowsMicrophoneEnabled == true ? "OK" : "WARN", s.WindowsMicrophoneEnabled == true ? "Permission granted in Aurora." : "Microphone permission is off in Aurora's capabilities.");
            add("Wake word", App.WakeWord?.IsEnabled == true ? "OK" : "WARN", App.WakeWord == null ? "Not initialised." : App.WakeWord.Status);

            var weatherSet = !string.IsNullOrWhiteSpace(s.WeatherLocation) || !string.IsNullOrWhiteSpace(s.WeatherPageUrl);
            add("Weather", weatherSet ? "OK" : "WARN", weatherSet ? $"Source: {s.WeatherSource}." : "No manual location; relies on Windows location permission.");

            if (App.PhoneLink.IsRunning)
            {
                var addr = PhoneLinkService.GetLanAddresses().FirstOrDefault() ?? "no LAN address found";
                add("Phone link", "OK", $"Running on {addr}:{App.PhoneLink.Port}. {App.PhoneLink.PairedDeviceCount} phone(s) paired.");
            }
            else add("Phone link", "WARN", "Not started. Click Phone in the sidebar and press Start phone link.");
        }

        private static bool HasTtsKey(AppSettings s) => s.TtsProvider switch
        {
            "openai" => !string.IsNullOrWhiteSpace(s.OpenAIApiKey),
            _ => true
        };

        private static async Task ProbeNetworkAsync(Action<string, string, string> add)
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
            try
            {
                using var r = await http.GetAsync("https://www.gstatic.com/generate_204");
                add("Internet", r.IsSuccessStatusCode ? "OK" : "WARN", $"HTTP {(int)r.StatusCode}.");
            }
            catch (Exception ex) { add("Internet", "FAIL", "Cannot reach the internet: " + ex.Message); return; }

            if (App.AI?.IsConfigured != true || App.Settings.LocalOnlyMode) return;
            try
            {
                var reply = await App.AI.SendAsync("Reply with the single word OK.", new List<Aurora.App.Models.ChatMessage>(), "ping");
                var bad = reply.Contains("request failed") || reply.StartsWith("[");
                add("AI provider live test", bad ? "FAIL" : "OK", bad ? reply : "The provider answered a test message.");
            }
            catch (Exception ex) { add("AI provider live test", "FAIL", ex.Message); }
        }
    }
}

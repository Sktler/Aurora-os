using System;
using System.Windows;
using System.Windows.Threading;
using ZoeyOS.App.Services;

namespace ZoeyOS.App
{
    public partial class App : Application
    {
        public static AppSettings Settings { get; private set; } = null!;
        public static MemoryStore Memory { get; private set; } = null!;
        public static IChatEngine AI { get; private set; } = null!;
        public static ImageGenClient ImageGen { get; private set; } = null!;
        public static SmartThingsClient SmartThings { get; private set; } = null!;
        public static HomeAssistantClient HomeAssistant { get; private set; } = null!;
        public static VoiceService Voice { get; private set; } = null!;
        public static WakeWordService WakeWord { get; private set; } = null!;
        public static WeatherClient Weather { get; private set; } = null!;
        public static WebSearchClient WebSearch { get; private set; } = null!;
        public static SpotifyClient Spotify { get; private set; } = null!;
        public static CameraService Camera { get; private set; } = null!;
        public static McpService Mcp { get; private set; } = null!;
        public static WindowsAutomationService WindowsAutomation { get; private set; } = null!;
        public static SystemMetricsService Metrics { get; private set; } = null!;

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            ShutdownMode = ShutdownMode.OnMainWindowClose;
            Settings = AppSettings.LoadOrCreate();

            var bootstrap = new Views.StartupPermissionWindow();
            bootstrap.Show();
            bootstrap.Activate();
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Loaded);

            var permissions = new WindowsPermissionService();
            await bootstrap.AskPermissionAsync("Location", permissions.RequestLocationAsync);
            await bootstrap.AskPermissionAsync("Microphone", permissions.RequestMicrophoneAsync);
            await bootstrap.AskPermissionAsync("Camera", permissions.RequestCameraAsync);

            System.Diagnostics.Debug.WriteLine("[Startup] Permission choices complete. Creating dashboard.");

            Memory = new MemoryStore(Settings.DatabasePath);
            Memory.Initialize();
            Weather = new WeatherClient();

            // DashboardViewModel subscribes to Metrics during construction, so the metrics
            // service must exist before MainWindow is created. Otherwise all four dashboard
            // resource readings remain at their initial placeholder values forever.
            try { Metrics = new SystemMetricsService(); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[Startup] Metrics initialization failed: {ex}"); }

            var mainWindow = new Views.MainWindow();
            MainWindow = mainWindow;
            mainWindow.Show();
            mainWindow.Activate();
            bootstrap.Close();

            try
            {
                AI = BuildChatEngine();
                ImageGen = BuildImageGenClient();
                SmartThings = new SmartThingsClient(Settings.SmartThingsToken);
                HomeAssistant = new HomeAssistantClient(Settings.HomeAssistantUrl, Settings.HomeAssistantToken);
                Voice = new VoiceService(Settings.VoiceName);
                WakeWord = new WakeWordService();
                var wakeWordWasEnabledBeforeSpeech = false;
                Voice.SpeakingChanged += speaking =>
                {
                    if (speaking)
                    {
                        wakeWordWasEnabledBeforeSpeech = WakeWord.IsEnabled;
                        if (wakeWordWasEnabledBeforeSpeech) WakeWord.Stop();
                    }
                    else if (wakeWordWasEnabledBeforeSpeech)
                    {
                        WakeWord.Start();
                        wakeWordWasEnabledBeforeSpeech = false;
                    }
                };
                WebSearch = new WebSearchClient();
                Spotify = BuildSpotifyClient();
                Camera = new CameraService();
                Mcp = new McpService();
                WindowsAutomation = CreateWindowsService();

                try { WakeWord.Start(); }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[Startup] Wake word start failed: {ex}"); }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Startup] Optional service initialization failed: {ex}");
            }
        }

        private static WindowsAutomationService CreateWindowsService()
        {
            return new WindowsAutomationService
            {
                FilesEnabled = Settings.WindowsFilesEnabled,
                ScreenEnabled = Settings.WindowsScreenEnabled,
                ClipboardEnabled = Settings.WindowsClipboardEnabled,
                ApplicationsEnabled = Settings.WindowsApplicationsEnabled,
                TerminalEnabled = Settings.WindowsTerminalEnabled,
                UiAutomationEnabled = Settings.WindowsUiAutomationEnabled,
                NetworkEnabled = Settings.WindowsNetworkEnabled,
                PowerEnabled = Settings.WindowsPowerEnabled
            };
        }
        public static void RefreshWindowsPermissions() { WindowsAutomation = CreateWindowsService(); }
        private static SpotifyClient BuildSpotifyClient() { var client = new SpotifyClient(Settings.SpotifyClientId, Settings.SpotifyRefreshToken); client.RefreshTokenRotated += newToken => { Settings.SpotifyRefreshToken = newToken; Settings.Save(); }; return client; }
        private static bool ActiveProviderIsConfigured() => Settings.ChatProvider switch { "groq" => !string.IsNullOrWhiteSpace(Settings.GroqApiKey), "openai" => !string.IsNullOrWhiteSpace(Settings.OpenAIApiKey), "claude" => !string.IsNullOrWhiteSpace(Settings.ClaudeApiKey), _ => !string.IsNullOrWhiteSpace(Settings.GeminiApiKey) };
        private static IChatEngine BuildChatEngine() => Settings.ChatProvider switch { "groq" => new GroqClient(Settings.GroqApiKey, Settings.GroqModel), "openai" => new OpenAIClient(Settings.OpenAIApiKey, Settings.OpenAIModel), "claude" => new ClaudeClient(Settings.ClaudeApiKey, Settings.ClaudeModel), _ => new GeminiClient(Settings.GeminiApiKey, Settings.GeminiModel) };
        private static ImageGenClient BuildImageGenClient() { var key = Settings.ImageProvider == "openai" ? Settings.ImageProviderApiKey : Settings.GeminiApiKey; return new ImageGenClient(key, Settings.ImageProvider); }
        public static void RefreshIntegrationClients() { SmartThings = new SmartThingsClient(Settings.SmartThingsToken); HomeAssistant = new HomeAssistantClient(Settings.HomeAssistantUrl, Settings.HomeAssistantToken); ImageGen = BuildImageGenClient(); Spotify = BuildSpotifyClient(); AI = BuildChatEngine(); RefreshWindowsPermissions(); }
        public static void ResetEverythingAndRestart() { Memory?.Dispose(); Voice?.Dispose(); WakeWord?.Dispose(); Camera?.DisposeAsync().AsTask().GetAwaiter().GetResult(); Mcp?.DisposeAsync().AsTask().GetAwaiter().GetResult(); Metrics?.Dispose(); AppSettings.ResetAll(); var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName; if (!string.IsNullOrEmpty(exePath)) { try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = exePath, UseShellExecute = true }); } catch { } } Environment.Exit(0); }
        protected override void OnExit(ExitEventArgs e) { Memory?.Dispose(); Voice?.Dispose(); WakeWord?.Dispose(); Camera?.DisposeAsync().AsTask().GetAwaiter().GetResult(); Mcp?.DisposeAsync().AsTask().GetAwaiter().GetResult(); Metrics?.Dispose(); base.OnExit(e); }
    }
}
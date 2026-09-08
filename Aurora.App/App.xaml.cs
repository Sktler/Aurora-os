using System;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using Aurora.App.Services;

namespace Aurora.App
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
        public static WindowsUpdaterService Updater { get; private set; } = null!;

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            ShutdownMode = ShutdownMode.OnMainWindowClose;
            var firstRun = !AppSettings.HasSavedConfiguration;
            Settings = AppSettings.LoadOrCreate();
            Updater = new WindowsUpdaterService();

            if (firstRun)
            {
                var welcome = new Views.FirstRunWindow();
                if (welcome.ShowDialog() != true)
                {
                    Shutdown();
                    return;
                }

                await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                var setup = new Views.SetupWindow(restartOnSave: false)
                {
                    Topmost = true,
                    ShowInTaskbar = true,
                    WindowState = WindowState.Normal
                };
                setup.Loaded += (_, _) =>
                {
                    setup.Activate();
                    setup.Focus();
                };
                setup.ShowDialog();
                setup.Topmost = false;
            }

            var bootstrap = new Views.StartupPermissionWindow();
            bootstrap.Show();
            bootstrap.Activate();
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Loaded);

            var permissions = new WindowsPermissionService();
            await bootstrap.AskPermissionAsync("Location", permissions.RequestLocationAsync);
            await bootstrap.AskPermissionAsync("Microphone", permissions.RequestMicrophoneAsync);
            await bootstrap.AskPermissionAsync("Camera", permissions.RequestCameraAsync);
            Settings.WindowsFilesEnabled = await bootstrap.AskCapabilityPermissionAsync("Files");
            Settings.WindowsScreenEnabled = await bootstrap.AskCapabilityPermissionAsync("Screen capture");
            Settings.WindowsClipboardEnabled = await bootstrap.AskCapabilityPermissionAsync("Clipboard");
            Settings.WindowsApplicationsEnabled = await bootstrap.AskCapabilityPermissionAsync("Applications");
            Settings.WindowsTerminalEnabled = await bootstrap.AskCapabilityPermissionAsync("Terminal");
            Settings.WindowsUiAutomationEnabled = await bootstrap.AskCapabilityPermissionAsync("UI automation");
            Settings.WindowsNetworkEnabled = await bootstrap.AskCapabilityPermissionAsync("Network");
            Settings.WindowsPowerEnabled = await bootstrap.AskCapabilityPermissionAsync("Power controls");
            Settings.Save();

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
            return WindowsAutomationService.FromSettings(Settings);
        }
        public static void RefreshWindowsPermissions() { WindowsAutomation = CreateWindowsService(); }
        private static SpotifyClient BuildSpotifyClient() { var client = new SpotifyClient(Settings.SpotifyClientId, Settings.SpotifyRefreshToken); client.RefreshTokenRotated += newToken => { Settings.SpotifyRefreshToken = newToken; Settings.Save(); }; return client; }
        private static bool ActiveProviderIsConfigured() => Settings.ChatProvider switch { "groq" => !string.IsNullOrWhiteSpace(Settings.GroqApiKey), "openai" => !string.IsNullOrWhiteSpace(Settings.OpenAIApiKey), "claude" => !string.IsNullOrWhiteSpace(Settings.ClaudeApiKey), _ => !string.IsNullOrWhiteSpace(Settings.GeminiApiKey) };
        private static IChatEngine BuildChatEngine() => Settings.ChatProvider switch { "groq" => new GroqClient(Settings.GroqApiKey, Settings.GroqModel), "openai" => new OpenAIClient(Settings.OpenAIApiKey, Settings.OpenAIModel), "claude" => new ClaudeClient(Settings.ClaudeApiKey, Settings.ClaudeModel), _ => new GeminiClient(Settings.GeminiApiKey, Settings.GeminiModel) };
        private static ImageGenClient BuildImageGenClient() { var key = Settings.ImageProvider == "openai" ? Settings.ImageProviderApiKey : Settings.GeminiApiKey; return new ImageGenClient(key, Settings.ImageProvider); }
        public static void RefreshIntegrationClients() { SmartThings = new SmartThingsClient(Settings.SmartThingsToken); HomeAssistant = new HomeAssistantClient(Settings.HomeAssistantUrl, Settings.HomeAssistantToken); ImageGen = BuildImageGenClient(); Spotify = BuildSpotifyClient(); AI = BuildChatEngine(); RefreshWindowsPermissions(); }
        public static void ResetEverythingAndRestart()
        {
            var databasePath = Settings?.DatabasePath;
            Memory?.Dispose();
            Voice?.Dispose();
            WakeWord?.Dispose();
            Camera?.DisposeAsync().AsTask().GetAwaiter().GetResult();
            Mcp?.DisposeAsync().AsTask().GetAwaiter().GetResult();
            Metrics?.Dispose();
            AppSettings.ResetAll(databasePath);

            var processPath = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(processPath))
            {
                var arguments = Environment.GetCommandLineArgs()
                    .Skip(1)
                    .Select(QuoteProcessArgument);
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = processPath,
                    Arguments = string.Join(" ", arguments),
                    UseShellExecute = true
                });
            }

            Environment.Exit(0);
        }
        private static string QuoteProcessArgument(string argument) =>
            argument.Contains(' ') || argument.Contains('"')
                ? $"\"{argument.Replace("\"", "\\\"")}\""
                : argument;
        protected override void OnExit(ExitEventArgs e) { Memory?.Dispose(); Voice?.Dispose(); WakeWord?.Dispose(); Camera?.DisposeAsync().AsTask().GetAwaiter().GetResult(); Mcp?.DisposeAsync().AsTask().GetAwaiter().GetResult(); Metrics?.Dispose(); base.OnExit(e); }
    }
}
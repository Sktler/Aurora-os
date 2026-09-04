using System;
using System.Threading.Tasks;
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

            Settings = AppSettings.LoadOrCreate();

            // Keep a visible foreground window in place while Windows owns the
            // native permission prompts. MainWindow is deliberately not created yet.
            var bootstrap = new Views.StartupPermissionWindow();
            bootstrap.Show();
            bootstrap.Activate();
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Loaded);

            var permissions = new WindowsPermissionService();

            // Each request is awaited separately. That means Aurora never starts
            // the next permission request until Windows has finished resolving the
            // current Allow/Don't allow decision. A denied/unavailable permission
            // is recorded and startup continues normally.
            var location = await RequestPermissionAsync(
                bootstrap,
                "Location",
                permissions.RequestLocationAsync);

            var microphone = await RequestPermissionAsync(
                bootstrap,
                "Microphone",
                permissions.RequestMicrophoneAsync);

            var camera = await RequestPermissionAsync(
                bootstrap,
                "Camera",
                permissions.RequestCameraAsync);

            System.Diagnostics.Debug.WriteLine(
                $"[Startup] Permissions - Location: {location}, Microphone: {microphone}, Camera: {camera}");

            // Keep the bootstrap open after the permission flow has completed.
            // The delay is persistent and configurable in AppSettings (0-600 sec).
            var delaySeconds = Math.Clamp(Settings.StartupBootstrapDelaySeconds, 0, 600);
            var endAt = DateTime.UtcNow.AddSeconds(delaySeconds);

            while (true)
            {
                var remaining = endAt - DateTime.UtcNow;
                if (remaining <= TimeSpan.Zero)
                    break;

                bootstrap.ShowCountdown(remaining);
                await Task.Delay(TimeSpan.FromSeconds(Math.Min(1, Math.Max(0.05, remaining.TotalSeconds))));
            }

            bootstrap.Close();

            if (!ActiveProviderIsConfigured()) new Views.SetupWindow().ShowDialog();
            Memory = new MemoryStore(Settings.DatabasePath); Memory.Initialize();
            AI = BuildChatEngine(); ImageGen = BuildImageGenClient();
            SmartThings = new SmartThingsClient(Settings.SmartThingsToken);
            HomeAssistant = new HomeAssistantClient(Settings.HomeAssistantUrl, Settings.HomeAssistantToken);
            Voice = new VoiceService(Settings.VoiceName); WakeWord = new WakeWordService();
            Weather = new WeatherClient(); WebSearch = new WebSearchClient();
            Spotify = BuildSpotifyClient();
            Camera = new CameraService(); Mcp = new McpService(); WindowsAutomation = CreateWindowsService();
            Metrics = new SystemMetricsService();
            WakeWord.Start();

            var mainWindow = new Views.MainWindow();
            MainWindow = mainWindow;
            mainWindow.Show();
            mainWindow.Activate();
        }

        private static async Task<PermissionResult> RequestPermissionAsync(
            Views.StartupPermissionWindow bootstrap,
            string name,
            Func<Task<PermissionResult>> request)
        {
            bootstrap.SetStatus($"Requesting {name.ToLowerInvariant()} access...\nFinish the Windows prompt to continue.");

            try
            {
                var result = await request();
                bootstrap.SetStatus(result switch
                {
                    PermissionResult.Allowed => $"{name} access allowed. Continuing...",
                    PermissionResult.Denied => $"{name} access denied. Continuing...",
                    _ => $"{name} access unavailable. Continuing..."
                });
                return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Startup] {name} permission request failed: {ex}");
                bootstrap.SetStatus($"{name} access could not be checked. Continuing...");
                return PermissionResult.Unavailable;
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
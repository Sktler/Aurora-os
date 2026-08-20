using System;
using System.Windows;
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
        public static WeatherClient Weather { get; private set; } = null!;
        public static WebSearchClient WebSearch { get; private set; } = null!;
        public static SpotifyClient Spotify { get; private set; } = null!;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Load or create settings.json (API keys, preferences) in %AppData%\Aurora
            Settings = AppSettings.LoadOrCreate();

            // First run (or the active provider's key was never set): collect it now
            // instead of making the user hand-edit settings.json.
            if (!ActiveProviderIsConfigured())
            {
                var setup = new Views.SetupWindow();
                setup.ShowDialog();
            }

            // Local SQLite store: one conversation history per companion, persists across launches
            Memory = new MemoryStore(Settings.DatabasePath);
            Memory.Initialize();

            AI = BuildChatEngine();
            ImageGen = BuildImageGenClient();
            SmartThings = new SmartThingsClient(Settings.SmartThingsToken);
            HomeAssistant = new HomeAssistantClient(Settings.HomeAssistantUrl, Settings.HomeAssistantToken);
            Voice = new VoiceService(Settings.VoiceName);
            Weather = new WeatherClient();
            WebSearch = new WebSearchClient();
            Spotify = BuildSpotifyClient();
        }

        private static SpotifyClient BuildSpotifyClient()
        {
            var client = new SpotifyClient(Settings.SpotifyClientId, Settings.SpotifyRefreshToken);
            // Spotify sometimes rotates the refresh token on use - keep settings.json in sync
            // so the next launch doesn't start from a stale/invalid one.
            client.RefreshTokenRotated += newToken =>
            {
                Settings.SpotifyRefreshToken = newToken;
                Settings.Save();
            };
            return client;
        }

        private static bool ActiveProviderIsConfigured() => Settings.ChatProvider switch
        {
            "groq" => !string.IsNullOrWhiteSpace(Settings.GroqApiKey),
            "openai" => !string.IsNullOrWhiteSpace(Settings.OpenAIApiKey),
            "claude" => !string.IsNullOrWhiteSpace(Settings.ClaudeApiKey),
            _ => !string.IsNullOrWhiteSpace(Settings.GeminiApiKey)
        };

        private static IChatEngine BuildChatEngine() => Settings.ChatProvider switch
        {
            "groq" => new GroqClient(Settings.GroqApiKey, Settings.GroqModel),
            "openai" => new OpenAIClient(Settings.OpenAIApiKey, Settings.OpenAIModel),
            "claude" => new ClaudeClient(Settings.ClaudeApiKey, Settings.ClaudeModel),
            _ => new GeminiClient(Settings.GeminiApiKey, Settings.GeminiModel)
        };

        private static ImageGenClient BuildImageGenClient()
        {
            var key = Settings.ImageProvider == "openai" ? Settings.ImageProviderApiKey : Settings.GeminiApiKey;
            return new ImageGenClient(key, Settings.ImageProvider);
        }

        /// <summary>Rebuilds the integration clients after settings.json changes (e.g. from the Integrations window).</summary>
        public static void RefreshIntegrationClients()
        {
            SmartThings = new SmartThingsClient(Settings.SmartThingsToken);
            HomeAssistant = new HomeAssistantClient(Settings.HomeAssistantUrl, Settings.HomeAssistantToken);
            ImageGen = BuildImageGenClient();
            Spotify = BuildSpotifyClient();
            // Rebuilding here (rather than requiring a full app restart, like the provider/key
            // change does) lets a model-name change - e.g. picking a newer Gemini or Groq
            // model as Google/Groq release them - take effect on the very next message.
            AI = BuildChatEngine();
        }

        /// <summary>
        /// Wipes all local Aurora data (keys, tokens, companion history) and restarts
        /// as a fresh process, landing back on first-run setup. Closes the database
        /// connection first so the folder can actually be deleted.
        /// </summary>
        public static void ResetEverythingAndRestart()
        {
            Memory?.Dispose();
            Voice?.Dispose();
            AppSettings.ResetAll();

            var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
            if (!string.IsNullOrEmpty(exePath))
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = exePath,
                        UseShellExecute = true
                    });
                }
                catch
                {
                    // Falling through to Exit still lets the user start Aurora again manually.
                }
            }
            Environment.Exit(0);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            Memory?.Dispose();
            Voice?.Dispose();
            base.OnExit(e);
        }
    }
}

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
        public static JamendoClient Jamendo { get; private set; } = null!;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            Settings = AppSettings.LoadOrCreate();

            if (!ActiveProviderIsConfigured())
            {
                var setup = new Views.SetupWindow();
                setup.ShowDialog();
            }

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
            Jamendo = BuildJamendoClient();
        }

        private static SpotifyClient BuildSpotifyClient()
        {
            var client = new SpotifyClient(Settings.SpotifyClientId, Settings.SpotifyRefreshToken);
            client.RefreshTokenRotated += newToken =>
            {
                Settings.SpotifyRefreshToken = newToken;
                Settings.Save();
            };
            return client;
        }

        private static JamendoClient BuildJamendoClient() => new(Settings.JamendoClientId);

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

        public static void RefreshIntegrationClients()
        {
            SmartThings = new SmartThingsClient(Settings.SmartThingsToken);
            HomeAssistant = new HomeAssistantClient(Settings.HomeAssistantUrl, Settings.HomeAssistantToken);
            ImageGen = BuildImageGenClient();
            Spotify = BuildSpotifyClient();
            Jamendo = BuildJamendoClient();
            AI = BuildChatEngine();
        }

        public static void ResetEverythingAndRestart()
        {
            Memory?.Dispose();
            Voice?.Dispose();
            Jamendo?.Dispose();
            AppSettings.ResetAll();
            var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
            if (!string.IsNullOrEmpty(exePath))
            {
                try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = exePath, UseShellExecute = true }); }
                catch { }
            }
            Environment.Exit(0);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            Memory?.Dispose();
            Voice?.Dispose();
            Jamendo?.Dispose();
            base.OnExit(e);
        }
    }
}

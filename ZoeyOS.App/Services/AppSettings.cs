using System;
using System.IO;
using System.Text.Json;

namespace ZoeyOS.App.Services
{
    /// <summary>
    /// Persisted locally at %AppData%\Aurora\settings.json.
    /// Holds your own API keys - never sent anywhere except directly to each provider.
    /// </summary>
    public class AppSettings
    {
        public string ChatProvider { get; set; } = "gemini";
        public string GeminiApiKey { get; set; } = "";
        public string GeminiModel { get; set; } = "gemini-3.6-flash";
        public string GroqApiKey { get; set; } = "";
        public string GroqModel { get; set; } = "llama-3.3-70b-versatile";
        public string OpenAIApiKey { get; set; } = "";
        public string OpenAIModel { get; set; } = "gpt-4o-mini";
        public string ClaudeApiKey { get; set; } = "";
        public string ClaudeModel { get; set; } = "claude-sonnet-5";
        public string ImageProvider { get; set; } = "gemini";
        public string ImageProviderApiKey { get; set; } = "";
        public string SmartThingsToken { get; set; } = "";
        public string HomeAssistantUrl { get; set; } = "";
        public string HomeAssistantToken { get; set; } = "";
        public bool AlexaConnected { get; set; } = false;
        public string GoogleClientId { get; set; } = "";
        public string GoogleClientSecret { get; set; } = "";
        public string GoogleRefreshToken { get; set; } = "";
        public bool GoogleConnected { get; set; } = false;
        public string GoogleAccountEmail { get; set; } = "";

        // Legacy Spotify settings are retained so existing Aurora installs deserialize cleanly.
        // Jamendo is now the primary in-app music provider.
        public string SpotifyClientId { get; set; } = "";
        public string SpotifyRefreshToken { get; set; } = "";
        public bool SpotifyConnected { get; set; } = false;
        public string SpotifyAccountName { get; set; } = "";

        public string JamendoClientId { get; set; } = "";
        public bool JamendoConnected { get; set; } = false;

        public string DatabasePath { get; set; } = "";
        public string VoiceName { get; set; } = "";
        public string TtsProvider { get; set; } = "openai";
        public string OpenAiTtsApiKey { get; set; } = "";
        public string OpenAiTtsVoice { get; set; } = "alloy";
        public string ElevenLabsApiKey { get; set; } = "";
        public string ElevenLabsVoiceId { get; set; } = "";
        public string ElevenLabsVoiceName { get; set; } = "";
        public string AzureSpeechKey { get; set; } = "";
        public string AzureSpeechRegion { get; set; } = "";
        public string AzureVoiceName { get; set; } = "en-US-JennyNeural";
        public bool SpeakRepliesByDefault { get; set; } = true;
        public bool DevModeEnabled { get; set; } = false;
        public string DeveloperOverrideCode { get; set; } = "";
        public string TrustedFolderPath { get; set; } = "";

        public static string ConfigDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Aurora");
        private static string ConfigPath => Path.Combine(ConfigDir, "settings.json");

        public static AppSettings LoadOrCreate()
        {
            Directory.CreateDirectory(ConfigDir);
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                if (string.IsNullOrWhiteSpace(loaded.DatabasePath))
                    loaded.DatabasePath = Path.Combine(ConfigDir, "aurora.db");

                var originalModel = loaded.GeminiModel;
                if (loaded.GeminiModel == "gemini-2.5-flash") loaded.GeminiModel = "gemini-3.6-flash";
                loaded.GeminiModel = StripModelsPrefix(loaded.GeminiModel);
                if (loaded.GeminiModel != originalModel) loaded.Save();
                return loaded;
            }

            var fresh = new AppSettings { DatabasePath = Path.Combine(ConfigDir, "aurora.db") };
            fresh.Save();
            return fresh;
        }

        private static string StripModelsPrefix(string? model)
        {
            var trimmed = model?.Trim() ?? "";
            return trimmed.StartsWith("models/", StringComparison.OrdinalIgnoreCase)
                ? trimmed.Substring("models/".Length)
                : trimmed;
        }

        public void Save()
        {
            Directory.CreateDirectory(ConfigDir);
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigPath, json);
        }

        public static void ResetAll()
        {
            if (Directory.Exists(ConfigDir)) Directory.Delete(ConfigDir, recursive: true);
        }
    }
}

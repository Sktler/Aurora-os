using System;
using System.IO;
using System.Text.Json;

namespace Aurora.App.Services
{
    public class AppSettings
    {
        public string UserName { get; set; } = "Adam";
        public string ChatProvider { get; set; } = "gemini";
        public string GeminiApiKey { get; set; } = "";
        public string GeminiModel { get; set; } = "gemini-3.6-flash";
        public string GroqApiKey { get; set; } = "";
        public string GroqModel { get; set; } = "llama-3.3-70b-versatile";
        public string OpenAIApiKey { get; set; } = "";
        public string OpenAIModel { get; set; } = "gpt-4o-mini";
        public string ClaudeApiKey { get; set; } = "";
        public string ClaudeModel { get; set; } = "claude-sonnet-5";
        public string GitHubCopilotApiKey { get; set; } = "";
        public string GitHubCopilotModel { get; set; } = "gpt-4o";
        public string ImageProvider { get; set; } = "gemini";
        public string ImageProviderApiKey { get; set; } = "";
        public string SmartThingsToken { get; set; } = "";
        public string HomeAssistantUrl { get; set; } = "";
        public string HomeAssistantToken { get; set; } = "";
        public string HubitatUrl { get; set; } = "";
        public string HubitatToken { get; set; } = "";
        public bool SmartHomeCatalogIncludeRawMetadata { get; set; } = false;
        public bool AlexaConnected { get; set; } = false;
        public string GoogleClientId { get; set; } = "";
        public string GoogleClientSecret { get; set; } = "";
        public string GoogleRefreshToken { get; set; } = "";
        public bool GoogleConnected { get; set; } = false;
        public string GoogleAccountEmail { get; set; } = "";
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
        /// <summary>Minimum SAPI recognition confidence (0.0-1.0) an utterance must clear to
        /// be forwarded to the AI at all; anything below this is treated as noise/mumbling
        /// rather than a real (if imperfectly transcribed) request. Starting point requested
        /// for Aurora's voice pipeline; tune up for a noisy room or down for a quiet one.</summary>
        public double VoiceConfidenceThreshold { get; set; } = Aurora.App.Services.VoiceTranscriptFilter.DefaultConfidenceThreshold;
        /// <summary>Target speaking pace in words per minute for replies, applied to whichever
        /// TTS provider is active (see <see cref="Aurora.App.Services.SpeechFormatter"/>).
        /// Natural conversational pace is roughly 150-170 wpm; adjustable per user preference
        /// without changing the wording spoken.</summary>
        public double SpeechPaceWpm { get; set; } = Aurora.App.Services.SpeechFormatter.TargetWordsPerMinute;
        public bool SpeakRepliesByDefault { get; set; } = true;
        public bool DevModeEnabled { get; set; } = false;
        public string DeveloperOverrideCode { get; set; } = "";
        public string TrustedFolderPath { get; set; } = "";
        public bool WindowsFilesEnabled { get; set; } = false;
        public bool WindowsScreenEnabled { get; set; } = false;
        public bool WindowsClipboardEnabled { get; set; } = false;
        public bool WindowsApplicationsEnabled { get; set; } = false;
        public bool WindowsTerminalEnabled { get; set; } = false;
        public bool WindowsUiAutomationEnabled { get; set; } = false;
        public bool WindowsNetworkEnabled { get; set; } = false;
        public bool WindowsPowerEnabled { get; set; } = false;
        public bool WindowsCameraEnabled { get; set; } = false;
        public bool WindowsMicrophoneEnabled { get; set; } = false;
        public bool WindowsMcpEnabled { get; set; } = false;

        public static string ConfigDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Aurora");
        private static string ConfigPath => Path.Combine(ConfigDir, "settings.json");
        public static bool HasSavedConfiguration
        {
            get
            {
                if (!File.Exists(ConfigPath))
                    return false;

                try
                {
                    var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(ConfigPath));
                    return settings != null &&
                           (settings.ChatProvider switch
                           {
                               "groq" => !string.IsNullOrWhiteSpace(settings.GroqApiKey),
                               "openai" => !string.IsNullOrWhiteSpace(settings.OpenAIApiKey),
                               "claude" => !string.IsNullOrWhiteSpace(settings.ClaudeApiKey),
                               "copilot" => !string.IsNullOrWhiteSpace(settings.GitHubCopilotApiKey),
                               _ => !string.IsNullOrWhiteSpace(settings.GeminiApiKey)
                           });
                }
                catch (JsonException)
                {
                    return false;
                }
            }
        }

        public static AppSettings LoadOrCreate()
        {
            Directory.CreateDirectory(ConfigDir);
            AppSettings loaded;
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                loaded = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
            else loaded = new AppSettings();
            if (string.IsNullOrWhiteSpace(loaded.UserName)) loaded.UserName = "Adam";
            if (string.IsNullOrWhiteSpace(loaded.DatabasePath)) loaded.DatabasePath = Path.Combine(ConfigDir, "aurora.db");

            var originalModel = loaded.GeminiModel;
            if (loaded.GeminiModel == "gemini-2.5-flash") loaded.GeminiModel = "gemini-3.6-flash";
            loaded.GeminiModel = StripModelsPrefix(loaded.GeminiModel);
            if (loaded.GeminiModel != originalModel || !File.Exists(ConfigPath)) loaded.Save();
            return loaded;
        }
        private static string StripModelsPrefix(string? model)
        {
            var trimmed = model?.Trim() ?? "";
            return trimmed.StartsWith("models/", StringComparison.OrdinalIgnoreCase) ? trimmed.Substring("models/".Length) : trimmed;
        }
        public void Save()
        {
            Directory.CreateDirectory(ConfigDir);
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigPath, json);
        }
        public static void ResetAll(string? databasePath = null, string? configDirectory = null)
        {
            var resetDirectory = configDirectory ?? ConfigDir;
            if (Directory.Exists(resetDirectory))
                Directory.Delete(resetDirectory, recursive: true);

            if (string.IsNullOrWhiteSpace(databasePath))
                return;

            var fullDatabasePath = Path.GetFullPath(databasePath);
            var fullConfigDirectory = Path.GetFullPath(resetDirectory)
                .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (fullDatabasePath.StartsWith(fullConfigDirectory, StringComparison.OrdinalIgnoreCase))
                return;

            foreach (var path in new[] { fullDatabasePath, $"{fullDatabasePath}-wal", $"{fullDatabasePath}-shm" })
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
        }
    }
}
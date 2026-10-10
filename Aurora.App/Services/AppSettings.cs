using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Windows.Security.Credentials;

namespace Aurora.App.Services
{
    public static class WindowsCredentialVault
    {
        private const string ResourcePrefix = "Aurora.AppSettings";

        public static string? GetSecret(string key, string? fallback = null)
        {
            if (string.IsNullOrWhiteSpace(key))
                return fallback;

            try
            {
                var vault = new PasswordVault();
                var resource = GetResourceName(key);
                var credential = vault.Retrieve(resource, "Aurora");
                return credential?.Password ?? fallback;
            }
            catch
            {
                return fallback;
            }
        }

        public static void SetSecret(string key, string? value)
        {
            if (string.IsNullOrWhiteSpace(key))
                return;

            if (string.IsNullOrWhiteSpace(value))
            {
                RemoveSecret(key);
                return;
            }

            try
            {
                var vault = new PasswordVault();
                var resource = GetResourceName(key);
                try
                {
                    foreach (var credential in vault.FindAllByResource(resource))
                        vault.Remove(credential);
                }
                catch
                {
                }

                vault.Add(new PasswordCredential(resource, "Aurora", value));
            }
            catch
            {
            }
        }

        public static void RemoveSecret(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return;

            try
            {
                var vault = new PasswordVault();
                var resource = GetResourceName(key);
                foreach (var credential in vault.FindAllByResource(resource))
                    vault.Remove(credential);
            }
            catch
            {
            }
        }

        public static void ClearAllSecrets()
        {
            try
            {
                var vault = new PasswordVault();
                foreach (var credential in vault.RetrieveAll())
                {
                    if (credential.Resource.StartsWith(ResourcePrefix, StringComparison.OrdinalIgnoreCase))
                        vault.Remove(credential);
                }
            }
            catch
            {
            }
        }

        private static string GetResourceName(string key) => $"{ResourcePrefix}.{key}";
    }

    public class AppSettings
    {
        private string _geminiApiKey = "";
        private string _groqApiKey = "";
        private string _openAIApiKey = "";
        private string _claudeApiKey = "";
        private string _gitHubCopilotApiKey = "";
        private string _imageProviderApiKey = "";
        private string _smartThingsToken = "";
        private string _homeAssistantToken = "";
        private string _hubitatToken = "";
        private string _googleClientSecret = "";
        private string _googleRefreshToken = "";
        private string _spotifyRefreshToken = "";
        private string _openAiTtsApiKey = "";
        private string _elevenLabsApiKey = "";
        private string _azureSpeechKey = "";

        private static readonly string[] SecretPropertyNames =
        {
            nameof(GeminiApiKey),
            nameof(GroqApiKey),
            nameof(OpenAIApiKey),
            nameof(ClaudeApiKey),
            nameof(GitHubCopilotApiKey),
            nameof(ImageProviderApiKey),
            nameof(SmartThingsToken),
            nameof(HomeAssistantToken),
            nameof(HubitatToken),
            nameof(GoogleClientSecret),
            nameof(GoogleRefreshToken),
            nameof(SpotifyRefreshToken),
            nameof(OpenAiTtsApiKey),
            nameof(ElevenLabsApiKey),
            nameof(AzureSpeechKey)
        };

        public string UserName { get; set; } = "Adam";
        public string ProfileId { get; set; } = "primary";
        public bool LocalOnlyMode { get; set; } = false;
        public bool SpeakerRecognitionEnabled { get; set; } = false;
        public string ChatProvider { get; set; } = "gemini";

        [JsonIgnore]
        public string GeminiApiKey
        {
            get => GetSecretValue(nameof(GeminiApiKey), _geminiApiKey);
            set => SetSecretValue(nameof(GeminiApiKey), ref _geminiApiKey, value);
        }

        public string GeminiModel { get; set; } = "gemini-3.6-flash";

        [JsonIgnore]
        public string GroqApiKey
        {
            get => GetSecretValue(nameof(GroqApiKey), _groqApiKey);
            set => SetSecretValue(nameof(GroqApiKey), ref _groqApiKey, value);
        }

        public string GroqModel { get; set; } = "llama-3.3-70b-versatile";

        [JsonIgnore]
        public string OpenAIApiKey
        {
            get => GetSecretValue(nameof(OpenAIApiKey), _openAIApiKey);
            set => SetSecretValue(nameof(OpenAIApiKey), ref _openAIApiKey, value);
        }

        public string OpenAIModel { get; set; } = "gpt-4o-mini";

        [JsonIgnore]
        public string ClaudeApiKey
        {
            get => GetSecretValue(nameof(ClaudeApiKey), _claudeApiKey);
            set => SetSecretValue(nameof(ClaudeApiKey), ref _claudeApiKey, value);
        }

        public string ClaudeModel { get; set; } = "claude-sonnet-5";

        [JsonIgnore]
        public string GitHubCopilotApiKey
        {
            get => GetSecretValue(nameof(GitHubCopilotApiKey), _gitHubCopilotApiKey);
            set => SetSecretValue(nameof(GitHubCopilotApiKey), ref _gitHubCopilotApiKey, value);
        }

        public string GitHubCopilotModel { get; set; } = "gpt-4o";
        public string ImageProvider { get; set; } = "gemini";

        [JsonIgnore]
        public string ImageProviderApiKey
        {
            get => GetSecretValue(nameof(ImageProviderApiKey), _imageProviderApiKey);
            set => SetSecretValue(nameof(ImageProviderApiKey), ref _imageProviderApiKey, value);
        }

        [JsonIgnore]
        public string SmartThingsToken
        {
            get => GetSecretValue(nameof(SmartThingsToken), _smartThingsToken);
            set => SetSecretValue(nameof(SmartThingsToken), ref _smartThingsToken, value);
        }

        public string HomeAssistantUrl { get; set; } = "";

        [JsonIgnore]
        public string HomeAssistantToken
        {
            get => GetSecretValue(nameof(HomeAssistantToken), _homeAssistantToken);
            set => SetSecretValue(nameof(HomeAssistantToken), ref _homeAssistantToken, value);
        }

        public string HubitatUrl { get; set; } = "";

        [JsonIgnore]
        public string HubitatToken
        {
            get => GetSecretValue(nameof(HubitatToken), _hubitatToken);
            set => SetSecretValue(nameof(HubitatToken), ref _hubitatToken, value);
        }

        public bool SmartHomeCatalogIncludeRawMetadata { get; set; } = false;
        public bool AlexaConnected { get; set; } = false;
        public string GoogleClientId { get; set; } = "";

        [JsonIgnore]
        public string GoogleClientSecret
        {
            get => GetSecretValue(nameof(GoogleClientSecret), _googleClientSecret);
            set => SetSecretValue(nameof(GoogleClientSecret), ref _googleClientSecret, value);
        }

        [JsonIgnore]
        public string GoogleRefreshToken
        {
            get => GetSecretValue(nameof(GoogleRefreshToken), _googleRefreshToken);
            set => SetSecretValue(nameof(GoogleRefreshToken), ref _googleRefreshToken, value);
        }

        public bool GoogleConnected { get; set; } = false;
        public string GoogleAccountEmail { get; set; } = "";
        public string SpotifyClientId { get; set; } = "";

        [JsonIgnore]
        public string SpotifyRefreshToken
        {
            get => GetSecretValue(nameof(SpotifyRefreshToken), _spotifyRefreshToken);
            set => SetSecretValue(nameof(SpotifyRefreshToken), ref _spotifyRefreshToken, value);
        }

        public bool SpotifyConnected { get; set; } = false;
        public string SpotifyAccountName { get; set; } = "";
        public string JamendoClientId { get; set; } = "";
        public bool JamendoConnected { get; set; } = false;
        public string DatabasePath { get; set; } = "";
        public string VoiceName { get; set; } = "";
        /// <summary>Name of the microphone to capture from; empty means "use whatever
        /// Windows currently has set as the system default recording device".</summary>
        public string MicrophoneDeviceName { get; set; } = AudioDeviceCatalog.SystemDefaultDeviceName;
        /// <summary>Name of the speaker/output device to play cloud TTS audio through; empty
        /// means "use whatever Windows currently has set as the system default playback
        /// device". Only applies to the OpenAI/ElevenLabs/Azure voices - the offline Windows
        /// voice always uses the system default output, since System.Speech has no API to
        /// target a specific playback device.</summary>
        public string SpeakerDeviceName { get; set; } = AudioDeviceCatalog.SystemDefaultDeviceName;
        public string TtsProvider { get; set; } = "openai";

        [JsonIgnore]
        public string OpenAiTtsApiKey
        {
            get => GetSecretValue(nameof(OpenAiTtsApiKey), _openAiTtsApiKey);
            set => SetSecretValue(nameof(OpenAiTtsApiKey), ref _openAiTtsApiKey, value);
        }

        public string OpenAiTtsVoice { get; set; } = "alloy";

        [JsonIgnore]
        public string ElevenLabsApiKey
        {
            get => GetSecretValue(nameof(ElevenLabsApiKey), _elevenLabsApiKey);
            set => SetSecretValue(nameof(ElevenLabsApiKey), ref _elevenLabsApiKey, value);
        }

        public string ElevenLabsVoiceId { get; set; } = "";
        public string ElevenLabsVoiceName { get; set; } = "";

        [JsonIgnore]
        public string AzureSpeechKey
        {
            get => GetSecretValue(nameof(AzureSpeechKey), _azureSpeechKey);
            set => SetSecretValue(nameof(AzureSpeechKey), ref _azureSpeechKey, value);
        }

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
        public string ActionApprovalMode { get; set; } = "AskForRisky";
        public List<string> TrustedTools { get; set; } = new();
        public List<string> PersistentGrantedTools { get; set; } = new();

        private string GetSecretValue(string propertyName, string fallback)
        {
            var secret = WindowsCredentialVault.GetSecret(propertyName, fallback);
            return string.IsNullOrWhiteSpace(secret) ? fallback : secret;
        }

        private void SetSecretValue(string propertyName, ref string backingField, string value)
        {
            backingField = value ?? string.Empty;
            if (string.IsNullOrWhiteSpace(backingField))
                WindowsCredentialVault.RemoveSecret(propertyName);
            else
                WindowsCredentialVault.SetSecret(propertyName, backingField);
        }

        private static bool IsLoadedSecretProperty(string propertyName, JsonElement value)
        {
            return value.ValueKind == JsonValueKind.String &&
                   !string.IsNullOrWhiteSpace(value.GetString()) &&
                   Array.Exists(SecretPropertyNames, existing => string.Equals(existing, propertyName, StringComparison.OrdinalIgnoreCase));
        }

        private void PersistLegacySecretValue(string propertyName, string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return;

            switch (propertyName)
            {
                case nameof(GeminiApiKey):
                    GeminiApiKey = value;
                    break;
                case nameof(GroqApiKey):
                    GroqApiKey = value;
                    break;
                case nameof(OpenAIApiKey):
                    OpenAIApiKey = value;
                    break;
                case nameof(ClaudeApiKey):
                    ClaudeApiKey = value;
                    break;
                case nameof(GitHubCopilotApiKey):
                    GitHubCopilotApiKey = value;
                    break;
                case nameof(ImageProviderApiKey):
                    ImageProviderApiKey = value;
                    break;
                case nameof(SmartThingsToken):
                    SmartThingsToken = value;
                    break;
                case nameof(HomeAssistantToken):
                    HomeAssistantToken = value;
                    break;
                case nameof(HubitatToken):
                    HubitatToken = value;
                    break;
                case nameof(GoogleClientSecret):
                    GoogleClientSecret = value;
                    break;
                case nameof(GoogleRefreshToken):
                    GoogleRefreshToken = value;
                    break;
                case nameof(SpotifyRefreshToken):
                    SpotifyRefreshToken = value;
                    break;
                case nameof(OpenAiTtsApiKey):
                    OpenAiTtsApiKey = value;
                    break;
                case nameof(ElevenLabsApiKey):
                    ElevenLabsApiKey = value;
                    break;
                case nameof(AzureSpeechKey):
                    AzureSpeechKey = value;
                    break;
            }
        }

        private void MigrateLegacySecretsFromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return;

            try
            {
                using var document = JsonDocument.Parse(json);
                if (document.RootElement.ValueKind != JsonValueKind.Object)
                    return;

                foreach (var propertyName in SecretPropertyNames)
                {
                    if (document.RootElement.TryGetProperty(propertyName, out var value) && IsLoadedSecretProperty(propertyName, value))
                        PersistLegacySecretValue(propertyName, value.GetString());
                }
            }
            catch (JsonException)
            {
            }
        }

        public void ClearActiveChatProviderCredential()
        {
            switch (ChatProvider)
            {
                case "groq":
                    GroqApiKey = "";
                    break;
                case "openai":
                    OpenAIApiKey = "";
                    break;
                case "claude":
                    ClaudeApiKey = "";
                    break;
                case "copilot":
                    GitHubCopilotApiKey = "";
                    break;
                default:
                    GeminiApiKey = "";
                    break;
            }
        }

        public static string ConfigDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Aurora");
        private static string ConfigPath => Path.Combine(ConfigDir, "settings.json");
        public static bool HasSavedConfiguration
        {
            get
            {
                if (!File.Exists(ConfigPath))
                {
                    return HasConfiguredActiveProviderSecret();
                }

                try
                {
                    var json = File.ReadAllText(ConfigPath);
                    var settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                    settings.MigrateLegacySecretsFromJson(json);
                    return settings.IsConfiguredForProvider(settings.ChatProvider);
                }
                catch (JsonException)
                {
                    return HasConfiguredActiveProviderSecret();
                }
            }
        }

        private static bool HasConfiguredActiveProviderSecret()
        {
            return IsConfiguredForProviderSecret("gemini") ||
                   IsConfiguredForProviderSecret("groq") ||
                   IsConfiguredForProviderSecret("openai") ||
                   IsConfiguredForProviderSecret("claude") ||
                   IsConfiguredForProviderSecret("copilot");
        }

        private static bool IsConfiguredForProviderSecret(string? provider)
        {
            var key = provider switch
            {
                "groq" => nameof(GroqApiKey),
                "openai" => nameof(OpenAIApiKey),
                "claude" => nameof(ClaudeApiKey),
                "copilot" => nameof(GitHubCopilotApiKey),
                _ => nameof(GeminiApiKey)
            };

            return !string.IsNullOrWhiteSpace(WindowsCredentialVault.GetSecret(key, string.Empty));
        }

        private bool IsConfiguredForProvider(string? provider)
        {
            var key = provider switch
            {
                "groq" => nameof(GroqApiKey),
                "openai" => nameof(OpenAIApiKey),
                "claude" => nameof(ClaudeApiKey),
                "copilot" => nameof(GitHubCopilotApiKey),
                _ => nameof(GeminiApiKey)
            };

            return !string.IsNullOrWhiteSpace(GetSecretValue(key, string.Empty));
        }

        public static AppSettings LoadOrCreate()
        {
            Directory.CreateDirectory(ConfigDir);
            AppSettings loaded;
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                loaded = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                loaded.MigrateLegacySecretsFromJson(json);
            }
            else loaded = new AppSettings();
            if (string.IsNullOrWhiteSpace(loaded.UserName)) loaded.UserName = "Adam";
            if (string.IsNullOrWhiteSpace(loaded.DatabasePath)) loaded.DatabasePath = Path.Combine(ConfigDir, "aurora.db");
            if (string.IsNullOrWhiteSpace(loaded.ActionApprovalMode)) loaded.ActionApprovalMode = "AskForRisky";
            if (loaded.TrustedTools == null) loaded.TrustedTools = new List<string>();
            if (loaded.PersistentGrantedTools == null) loaded.PersistentGrantedTools = new List<string>();

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
            if (App.Profiles != null)
                App.Profiles.SaveActiveSettings();
        }

        public void SaveGlobalOnly()
        {
            Directory.CreateDirectory(ConfigDir);
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigPath, json);
        }

        public static void ResetAll(string? databasePath = null, string? configDirectory = null)
        {
            foreach (var propertyName in SecretPropertyNames)
                WindowsCredentialVault.RemoveSecret(propertyName);

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
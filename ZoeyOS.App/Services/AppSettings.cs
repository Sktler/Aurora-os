using System;
using System.IO;
using System.Text.Json;

namespace ZoeyOS.App.Services
{
    /// <summary>
    /// Persisted locally at %AppData%\ZoeyOS\settings.json.
    /// Holds your own API keys - never sent anywhere except directly to each provider.
    /// </summary>
    public class AppSettings
    {
        // Which chat engine is active: "gemini" | "groq" | "openai" | "claude"
        public string ChatProvider { get; set; } = "gemini";

        public string GeminiApiKey { get; set; } = "";
        public string GeminiModel { get; set; } = "gemini-3.6-flash";

        public string GroqApiKey { get; set; } = "";
        public string GroqModel { get; set; } = "llama-3.3-70b-versatile";

        // Paid, metered APIs - no permanent free tier the way Gemini/Groq have. See
        // AIProviderCatalog for the honest cost note shown in Settings/Setup.
        public string OpenAIApiKey { get; set; } = "";
        public string OpenAIModel { get; set; } = "gpt-4o-mini";

        public string ClaudeApiKey { get; set; } = "";
        public string ClaudeModel { get; set; } = "claude-sonnet-5";

        // Defaults to "gemini" so image generation uses the same free Gemini key -
        // no separate provider key needed. Set to "openai" (with ImageProviderApiKey)
        // to use a different provider instead.
        public string ImageProvider { get; set; } = "gemini";
        public string ImageProviderApiKey { get; set; } = "";

        public string SmartThingsToken { get; set; } = "";

        public string HomeAssistantUrl { get; set; } = "";
        public string HomeAssistantToken { get; set; } = "";

        // Alexa Smart Home Skill setup is heavier (requires a published skill + account linking),
        // so for now this just tracks whether the user has completed that setup elsewhere.
        public bool AlexaConnected { get; set; } = false;

        // Gmail / Google Drive & Docs, via real OAuth 2.0 (see GoogleAuthClient).
        // ClientId/ClientSecret come from a Google Cloud OAuth client you create yourself -
        // Aurora can't ship its own on your behalf. RefreshToken is what actually proves
        // a working connection; GoogleConnected/GoogleAccountEmail just mirror that state
        // for display.
        public string GoogleClientId { get; set; } = "";
        public string GoogleClientSecret { get; set; } = "";
        public string GoogleRefreshToken { get; set; } = "";
        public bool GoogleConnected { get; set; } = false;
        public string GoogleAccountEmail { get; set; } = "";

        // Spotify, via real OAuth (Authorization Code + PKCE - no client secret needed,
        // just a free Client ID from developer.spotify.com/dashboard). Playback control
        // needs Spotify Premium; reading what's playing and searching works on any account.
        public string SpotifyClientId { get; set; } = "";
        public string SpotifyRefreshToken { get; set; } = "";
        public bool SpotifyConnected { get; set; } = false;
        public string SpotifyAccountName { get; set; } = "";

        public string DatabasePath { get; set; } = "";

        // Which installed Windows voice to speak replies with (exact name from
        // GetInstalledVoices, e.g. "Microsoft David Desktop"). Empty = system default.
        // Only used when TtsProvider is "windows".
        public string VoiceName { get; set; } = "";

        // Which text-to-speech backend actually speaks replies: "openai" (default - the
        // biggest, most natural-sounding pool without needing a second account, and falls
        // back to the chat OpenAI key below if no separate TTS key is set), "elevenlabs" or
        // "azure" (optional, bring-your-own-key, both with much larger voice catalogs than
        // Windows ships with), or "windows" (fully offline, free, no key, smaller selection).
        // Whichever is picked, VoiceService falls back to "windows" automatically if the
        // configured provider has no key or a call fails - a reply is never silently unspoken.
        public string TtsProvider { get; set; } = "openai";

        // Optional - if left empty, TTS reuses OpenAIApiKey (the chat key) so anyone already
        // using OpenAI for chat doesn't have to paste the same key twice.
        public string OpenAiTtsApiKey { get; set; } = "";
        public string OpenAiTtsVoice { get; set; } = "alloy";

        public string ElevenLabsApiKey { get; set; } = "";
        public string ElevenLabsVoiceId { get; set; } = "";
        public string ElevenLabsVoiceName { get; set; } = ""; // display-only, for Settings

        public string AzureSpeechKey { get; set; } = "";
        public string AzureSpeechRegion { get; set; } = ""; // e.g. "eastus" - part of the endpoint URL
        public string AzureVoiceName { get; set; } = "en-US-JennyNeural";

        // Whether companions speak their replies out loud without you having to tap the
        // speaker toggle every time. On by default now that voice mode is a core feature.
        public bool SpeakRepliesByDefault { get; set; } = true;

        // When on: chat errors show the real exception instead of a generic message, and
        // Settings shows each companion's enforced tool-access role for verification.
        public bool DevModeEnabled { get; set; } = false;

        // Set the first time you unlock developer mode; required to unlock it again after
        // that. Empty means no code has been set yet - the next code entered becomes it.
        public string DeveloperOverrideCode { get; set; } = "";

        // One folder Sift is allowed to read from - chosen explicitly by the user via a
        // folder picker, never browsed elsewhere on the system. Empty = no folder access.
        public string TrustedFolderPath { get; set; } = "";

        public static string ConfigDir =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Aurora");

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

                // One-time upgrade: earlier builds saved "gemini-2.5-flash" as the model
                // name into settings.json. That's a genuinely older model now, so nudge
                // existing installs forward to the current default. If you've deliberately
                // picked a different model since, this leaves it alone.
                if (loaded.GeminiModel == "gemini-2.5-flash")
                    loaded.GeminiModel = "gemini-3.6-flash";

                // Google's own docs sometimes refer to models as "models/gemini-3.6-flash"
                // rather than the bare name - if that ever ends up in settings.json (hand
                // edit, pasted from an API response, etc.), strip it so the raw name is
                // what actually gets sent to the API.
                loaded.GeminiModel = StripModelsPrefix(loaded.GeminiModel);

                if (loaded.GeminiModel != originalModel)
                    loaded.Save();

                return loaded;
            }

            var fresh = new AppSettings
            {
                DatabasePath = Path.Combine(ConfigDir, "aurora.db")
            };
            fresh.Save();
            return fresh;
        }

        /// <summary>Strips a leading "models/" from a Gemini model name, e.g. turns
        /// "models/gemini-3.6-flash" into "gemini-3.6-flash". Google's own docs and some
        /// API responses use the prefixed form; the REST endpoint we call wants the bare
        /// name in the URL, so anything that slips in with the prefix gets normalized here
        /// rather than causing a confusing 404 at request time.</summary>
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

        /// <summary>
        /// Wipes everything Aurora has stored locally - settings.json (keys, tokens,
        /// preferences) and aurora.db (every companion's renamed name and chat history).
        /// The app should restart right after this so it comes back up completely fresh,
        /// as if freshly installed.
        /// </summary>
        public static void ResetAll()
        {
            if (Directory.Exists(ConfigDir))
                Directory.Delete(ConfigDir, recursive: true);
        }
    }
}

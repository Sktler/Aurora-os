using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ZoeyOS.App.Models;
using ZoeyOS.App.Services;

namespace ZoeyOS.App.ViewModels
{
    public partial class IntegrationsViewModel : ObservableObject
    {
        /// <summary>The live Companion instances already bound to the main window's sidebar -
        /// not a fresh copy loaded from the database, so an accent color change here shows up
        /// on the sidebar globe immediately, without needing to close and reopen Settings.</summary>
        public ObservableCollection<Companion> Companions { get; }

        public IntegrationsViewModel(IEnumerable<Companion> companions)
        {
            Companions = new ObservableCollection<Companion>(companions);
        }

        // --- Navigation (hub + dedicated pages) ---
        [ObservableProperty] private SettingsSection _currentSection = SettingsSection.Hub;

        [RelayCommand]
        private void GoToSection(SettingsSection section)
        {
            CurrentSection = section;
            if (section == SettingsSection.Models && ModelSuggestions.Count == 0 && !IsLoadingModels)
                _ = RefreshModelsAsync();
        }

        [RelayCommand]
        private void GoToHub() => CurrentSection = SettingsSection.Hub;

        // --- SmartThings ---
        [ObservableProperty] private string _smartThingsToken = App.Settings.SmartThingsToken;
        [ObservableProperty] private bool _smartThingsVerified = App.SmartThings.IsConfigured;
        [ObservableProperty] private string _smartThingsStatus = "";
        [ObservableProperty] private bool _isTestingSmartThings;

        // --- Home Assistant ---
        [ObservableProperty] private string _homeAssistantUrl = App.Settings.HomeAssistantUrl;
        [ObservableProperty] private string _homeAssistantToken = App.Settings.HomeAssistantToken;
        [ObservableProperty] private bool _homeAssistantVerified = App.HomeAssistant.IsConfigured;
        [ObservableProperty] private string _homeAssistantStatus = "";
        [ObservableProperty] private bool _isTestingHomeAssistant;

        // --- Alexa (Smart Home Skill setup happens outside the app - see note in UI) ---
        [ObservableProperty] private bool _alexaConnected = App.Settings.AlexaConnected;

        // --- Google (Gmail / Drive & Docs) - real OAuth ---
        [ObservableProperty] private string _googleClientId = App.Settings.GoogleClientId;
        [ObservableProperty] private string _googleClientSecret = App.Settings.GoogleClientSecret;
        [ObservableProperty] private bool _googleConnected = App.Settings.GoogleConnected;
        [ObservableProperty] private string _googleAccountEmail = App.Settings.GoogleAccountEmail;
        [ObservableProperty] private bool _isConnectingGoogle;

        // --- Spotify - real OAuth (PKCE, no client secret needed) ---
        [ObservableProperty] private string _spotifyClientId = App.Settings.SpotifyClientId;
        [ObservableProperty] private bool _spotifyConnected = App.Settings.SpotifyConnected;
        [ObservableProperty] private string _spotifyAccountName = App.Settings.SpotifyAccountName;
        [ObservableProperty] private bool _isConnectingSpotify;

        [ObservableProperty] private bool _isDiscovering;
        [ObservableProperty] private string _statusMessage = "";

        // --- Voice ---
        // Windows SAPI voices - only relevant when TtsProvider == "windows".
        public ObservableCollection<VoiceOption> Voices { get; } = new(App.Voice.GetAvailableVoices());
        [ObservableProperty] private VoiceOption? _selectedVoice;
        [ObservableProperty] private bool _speakRepliesByDefault = App.Settings.SpeakRepliesByDefault;
        [ObservableProperty] private string _voiceStatus = "";

        [ObservableProperty] private string _ttsProvider = App.Settings.TtsProvider;

        [ObservableProperty] private string _openAiTtsApiKey = App.Settings.OpenAiTtsApiKey;
        [ObservableProperty] private string _openAiTtsVoice = App.Settings.OpenAiTtsVoice;

        [ObservableProperty] private string _elevenLabsApiKey = App.Settings.ElevenLabsApiKey;
        [ObservableProperty] private ObservableCollection<ElevenLabsVoiceOption> _elevenLabsVoices = new();
        [ObservableProperty] private ElevenLabsVoiceOption? _selectedElevenLabsVoice;

        [ObservableProperty] private string _azureSpeechKey = App.Settings.AzureSpeechKey;
        [ObservableProperty] private string _azureSpeechRegion = App.Settings.AzureSpeechRegion;
        [ObservableProperty] private ObservableCollection<AzureVoiceOption> _azureVoices = new();
        [ObservableProperty] private AzureVoiceOption? _selectedAzureVoice;

        [ObservableProperty] private bool _isLoadingVoices;

        [RelayCommand]
        private async Task LoadElevenLabsVoicesAsync()
        {
            if (string.IsNullOrWhiteSpace(ElevenLabsApiKey))
            {
                VoiceStatus = "Enter an ElevenLabs API key first.";
                return;
            }

            IsLoadingVoices = true;
            VoiceStatus = "Loading voices from ElevenLabs...";
            try
            {
                var voices = await App.Voice.ListElevenLabsVoicesAsync(ElevenLabsApiKey);
                ElevenLabsVoices = new ObservableCollection<ElevenLabsVoiceOption>(voices);
                VoiceStatus = voices.Count > 0
                    ? $"{voices.Count} voices loaded from ElevenLabs."
                    : "ElevenLabs didn't return any voices for this key.";
            }
            catch (Exception ex)
            {
                VoiceStatus = $"Couldn't load ElevenLabs voices: {ex.Message}";
            }
            finally
            {
                IsLoadingVoices = false;
            }
        }

        [RelayCommand]
        private async Task LoadAzureVoicesAsync()
        {
            if (string.IsNullOrWhiteSpace(AzureSpeechKey) || string.IsNullOrWhiteSpace(AzureSpeechRegion))
            {
                VoiceStatus = "Enter an Azure Speech key and region first.";
                return;
            }

            IsLoadingVoices = true;
            VoiceStatus = "Loading voices from Azure...";
            try
            {
                var voices = await App.Voice.ListAzureVoicesAsync(AzureSpeechKey, AzureSpeechRegion);
                AzureVoices = new ObservableCollection<AzureVoiceOption>(voices);
                VoiceStatus = voices.Count > 0
                    ? $"{voices.Count} voices loaded from Azure."
                    : "Azure didn't return any voices - check the region is correct.";
            }
            catch (Exception ex)
            {
                VoiceStatus = $"Couldn't load Azure voices: {ex.Message}";
            }
            finally
            {
                IsLoadingVoices = false;
            }
        }

        public string CurrentEngineDescription
        {
            get
            {
                var p = AIProviderCatalog.Get(App.Settings.ChatProvider);
                if (!App.AI.IsConfigured)
                    return $"Chat runs on {p.DisplayName} (no key set).";

                return p.Key switch
                {
                    "gemini" => "Chat runs on Gemini - image generation is included with the same key.",
                    "openai" or "claude" => $"Chat runs on {p.DisplayName}. This is a paid, metered API - keep an eye on usage in their dashboard.",
                    _ => $"Chat runs on {p.DisplayName}."
                };
            }
        }

        // --- Model name - kept as free text rather than a fixed dropdown, so Aurora
        // isn't pinned to whichever model happened to be current when this was built.
        // Works with any model name the active provider actually serves - point it at
        // whatever's current or preferred, no app update required. Which settings field
        // this reads/writes is resolved through AIProviderCatalog by the active
        // ChatProvider ("gemini" | "groq" | "openai" | "claude").
        private static AIProviderInfo ActiveProvider => AIProviderCatalog.Get(App.Settings.ChatProvider);

        public string ModelFieldLabel => $"{ActiveProvider.DisplayName} model";

        // Live model catalog, fetched from the active provider's own API - never a
        // hard-coded list, since providers add and retire models on their own schedule
        // and this app has no way to know that in advance. The ComboBox stays editable
        // (IsEditable="True") regardless, so typing any model name still always works
        // even if the fetch fails or the provider adds something between refreshes.
        [ObservableProperty] private ObservableCollection<string> _modelSuggestions = new();
        [ObservableProperty] private bool _isLoadingModels;

        partial void OnIsLoadingModelsChanged(bool value) => OnPropertyChanged(nameof(CanEditModel));

        /// <summary>False while a live model fetch is in flight, so the picker can be
        /// disabled during that brief wait instead of letting an edit race the fetch.</summary>
        public bool CanEditModel => !IsLoadingModels;
        [ObservableProperty] private string _modelListStatus = "";

        [RelayCommand]
        private async Task RefreshModelsAsync()
        {
            if (!App.AI.IsConfigured)
            {
                ModelListStatus = $"Save a {ActiveProvider.DisplayName} API key first.";
                return;
            }

            IsLoadingModels = true;
            ModelListStatus = $"Loading models from {ActiveProvider.DisplayName}...";
            try
            {
                var models = await App.AI.ListModelsAsync();
                ModelSuggestions = new ObservableCollection<string>(models);
                ModelListStatus = models.Count > 0
                    ? $"{models.Count} models loaded live from {ActiveProvider.DisplayName}."
                    : $"{ActiveProvider.DisplayName} didn't return any models.";
            }
            catch (Exception ex)
            {
                ModelListStatus = $"Couldn't load models: {ex.Message}";
            }
            finally
            {
                IsLoadingModels = false;
            }
        }

        private static string GetActiveModel() => ActiveProvider.Key switch
        {
            "groq" => App.Settings.GroqModel,
            "openai" => App.Settings.OpenAIModel,
            "claude" => App.Settings.ClaudeModel,
            _ => App.Settings.GeminiModel
        };

        [ObservableProperty]
        private string _modelName = GetActiveModel();

        [ObservableProperty] private string _modelStatus = "";

        [RelayCommand]
        private void SaveModel()
        {
            var trimmed = (ModelName ?? "").Trim();
            if (trimmed.StartsWith("models/", StringComparison.OrdinalIgnoreCase))
                trimmed = trimmed.Substring("models/".Length);

            if (string.IsNullOrWhiteSpace(trimmed))
            {
                ModelStatus = "Enter a model name first.";
                return;
            }

            switch (ActiveProvider.Key)
            {
                case "groq": App.Settings.GroqModel = trimmed; break;
                case "openai": App.Settings.OpenAIModel = trimmed; break;
                case "claude": App.Settings.ClaudeModel = trimmed; break;
                default: App.Settings.GeminiModel = trimmed; break;
            }

            App.Settings.Save();
            App.RefreshIntegrationClients(); // rebuilds App.AI - takes effect on the next message, no restart
            ModelName = trimmed;
            ModelStatus = $"Saved. Companions now use \"{trimmed}\".";
        }

        public string SettingsFilePath => System.IO.Path.Combine(AppSettings.ConfigDir, "settings.json");
        public string DatabaseFilePath => App.Settings.DatabasePath;

        // --- Sift's documents folder ---
        [ObservableProperty] private string _trustedFolderPath = App.Settings.TrustedFolderPath;

        [RelayCommand]
        private void ClearTrustedFolder()
        {
            TrustedFolderPath = "";
            App.Settings.TrustedFolderPath = "";
            App.Settings.Save();
            StatusMessage = "Sift's documents folder cleared.";
        }

        /// <summary>Called from code-behind after the folder picker dialog returns a path.</summary>
        public void SetTrustedFolder(string path)
        {
            TrustedFolderPath = path;
            App.Settings.TrustedFolderPath = path;
            App.Settings.Save();
            StatusMessage = $"Sift can now read files in {path}.";
        }

        /// <summary>Changes one companion's orb color. Companion.AccentHex is itself an
        /// ObservableProperty, so this updates the sidebar globe live - the same Companion
        /// instance is bound there since Companions was constructed from the live list, not
        /// a fresh database load.</summary>
        public void SetCompanionColor(Companion companion, string hex)
        {
            companion.AccentHex = hex;
            App.Memory.SaveCompanion(companion);
        }

        // --- Developer ---
        // DevModeEnabled is the actual gate (checked by CompanionViewModel for real error
        // detail, and by the UI below to reveal the override controls). It's no longer a
        // plain toggle - turning it on requires the override code below, since once on it
        // lets you rewrite any companion's role, system prompt, and tool access directly.
        [ObservableProperty] private bool _devModeEnabled = App.Settings.DevModeEnabled;
        [ObservableProperty] private string _overrideCodeInput = "";
        [ObservableProperty] private string _devModeStatus = "";

        public static Array ToolAccessValues => Enum.GetValues(typeof(CompanionToolAccess));

        [RelayCommand]
        private void UnlockDevMode()
        {
            var entered = OverrideCodeInput.Trim();
            if (string.IsNullOrWhiteSpace(entered))
            {
                DevModeStatus = "Enter a code first.";
                return;
            }

            if (string.IsNullOrEmpty(App.Settings.DeveloperOverrideCode))
            {
                // First use: whatever you enter becomes your override code from now on.
                App.Settings.DeveloperOverrideCode = entered;
                DevModeEnabled = true;
                App.Settings.DevModeEnabled = true;
                App.Settings.Save();
                DevModeStatus = "Override code set. Developer mode unlocked - use this same code next time.";
            }
            else if (entered == App.Settings.DeveloperOverrideCode)
            {
                DevModeEnabled = true;
                App.Settings.DevModeEnabled = true;
                App.Settings.Save();
                DevModeStatus = "Developer mode unlocked.";
            }
            else
            {
                DevModeStatus = "Incorrect override code.";
            }

            OverrideCodeInput = "";
        }

        [RelayCommand]
        private void LockDevMode()
        {
            DevModeEnabled = false;
            App.Settings.DevModeEnabled = false;
            App.Settings.Save();
            DevModeStatus = "Developer mode locked.";
        }

        /// <summary>Persists whatever edits were made directly to a companion's Role,
        /// SystemPrompt, or ToolAccess in the developer panel - those TextBoxes/ComboBox are
        /// two-way bound straight to the live Companion object, so this just writes the
        /// current in-memory state to disk.</summary>
        public void SaveCompanionOverrides(Companion companion)
        {
            App.Memory.SaveCompanion(companion);
            DevModeStatus = $"Saved overrides for {companion.Name}.";
        }

        public ObservableCollection<DiscoveredDevice> Devices { get; } = new();

        [RelayCommand]
        private async Task SaveAndTestSmartThingsAsync()
        {
            if (IsTestingSmartThings) return;

            App.Settings.SmartThingsToken = SmartThingsToken.Trim();
            App.Settings.Save();
            App.RefreshIntegrationClients();

            IsTestingSmartThings = true;
            SmartThingsStatus = "Testing connection...";
            try
            {
                var (success, message) = await App.SmartThings.TestConnectionAsync();
                SmartThingsVerified = success;
                SmartThingsStatus = message;
            }
            finally
            {
                IsTestingSmartThings = false;
            }
        }

        [RelayCommand]
        private async Task SaveAndTestHomeAssistantAsync()
        {
            if (IsTestingHomeAssistant) return;

            App.Settings.HomeAssistantUrl = HomeAssistantUrl.Trim();
            App.Settings.HomeAssistantToken = HomeAssistantToken.Trim();
            App.Settings.Save();
            App.RefreshIntegrationClients();

            IsTestingHomeAssistant = true;
            HomeAssistantStatus = "Testing connection...";
            try
            {
                var (success, message) = await App.HomeAssistant.TestConnectionAsync();
                HomeAssistantVerified = success;
                HomeAssistantStatus = message;
            }
            finally
            {
                IsTestingHomeAssistant = false;
            }
        }

        [RelayCommand]
        private void ToggleAlexaConnected()
        {
            AlexaConnected = !AlexaConnected;
            App.Settings.AlexaConnected = AlexaConnected;
            App.Settings.Save();
            StatusMessage = AlexaConnected
                ? "Marked Alexa as connected."
                : "Marked Alexa as not connected.";
        }

        [RelayCommand]
        private async Task ConnectGoogleAsync()
        {
            if (IsConnectingGoogle) return;

            var clientId = GoogleClientId.Trim();
            var clientSecret = GoogleClientSecret.Trim();

            if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
            {
                StatusMessage = "Enter your Google OAuth Client ID and Client Secret first (see the note below).";
                return;
            }

            IsConnectingGoogle = true;
            StatusMessage = "Opening your browser to sign in with Google...";
            try
            {
                var result = await GoogleAuthClient.ConnectAsync(clientId, clientSecret);

                if (result.Success)
                {
                    App.Settings.GoogleClientId = clientId;
                    App.Settings.GoogleClientSecret = clientSecret;
                    App.Settings.GoogleRefreshToken = result.RefreshToken ?? App.Settings.GoogleRefreshToken;
                    App.Settings.GoogleAccountEmail = result.Email ?? "";
                    App.Settings.GoogleConnected = true;
                    App.Settings.Save();

                    GoogleAccountEmail = result.Email ?? "";
                    GoogleConnected = true;
                    StatusMessage = string.IsNullOrEmpty(result.Email)
                        ? "Connected to Google."
                        : $"Connected as {result.Email}.";
                }
                else
                {
                    StatusMessage = $"Google connection failed: {result.Error}";
                }
            }
            finally
            {
                IsConnectingGoogle = false;
            }
        }

        [RelayCommand]
        private void DisconnectGoogle()
        {
            App.Settings.GoogleRefreshToken = "";
            App.Settings.GoogleAccountEmail = "";
            App.Settings.GoogleConnected = false;
            App.Settings.Save();

            GoogleAccountEmail = "";
            GoogleConnected = false;
            StatusMessage = "Disconnected from Google.";
        }

        [RelayCommand]
        private async Task ConnectSpotifyAsync()
        {
            if (IsConnectingSpotify) return;

            var clientId = SpotifyClientId.Trim();
            if (string.IsNullOrWhiteSpace(clientId))
            {
                StatusMessage = "Enter your Spotify Client ID first (see the note below).";
                return;
            }

            IsConnectingSpotify = true;
            StatusMessage = "Opening your browser to sign in with Spotify...";
            try
            {
                var result = await SpotifyAuthClient.ConnectAsync(clientId);

                if (result.Success)
                {
                    App.Settings.SpotifyClientId = clientId;
                    App.Settings.SpotifyRefreshToken = result.RefreshToken ?? App.Settings.SpotifyRefreshToken;
                    App.Settings.SpotifyAccountName = result.DisplayName ?? "";
                    App.Settings.SpotifyConnected = true;
                    App.Settings.Save();
                    App.RefreshIntegrationClients();

                    SpotifyAccountName = result.DisplayName ?? "";
                    SpotifyConnected = true;
                    StatusMessage = string.IsNullOrEmpty(result.DisplayName)
                        ? "Connected to Spotify."
                        : $"Connected as {result.DisplayName}.";
                }
                else
                {
                    StatusMessage = $"Spotify connection failed: {result.Error}";
                }
            }
            finally
            {
                IsConnectingSpotify = false;
            }
        }

        [RelayCommand]
        private void DisconnectSpotify()
        {
            App.Settings.SpotifyRefreshToken = "";
            App.Settings.SpotifyAccountName = "";
            App.Settings.SpotifyConnected = false;
            App.Settings.Save();
            App.RefreshIntegrationClients();

            SpotifyAccountName = "";
            SpotifyConnected = false;
            StatusMessage = "Disconnected from Spotify.";
        }

        // --- System volume ---
        [ObservableProperty] private double _systemVolume = SystemVolumeControl.GetVolume() * 100;
        [ObservableProperty] private bool _isSystemMuted = SystemVolumeControl.GetMute();

        partial void OnSystemVolumeChanged(double value) => SystemVolumeControl.SetVolume((float)(value / 100.0));

        [RelayCommand]
        private void ToggleSystemMute()
        {
            IsSystemMuted = !IsSystemMuted;
            SystemVolumeControl.SetMute(IsSystemMuted);
        }

        [RelayCommand]
        private void SaveVoice()
        {
            App.Settings.TtsProvider = TtsProvider;

            App.Settings.OpenAiTtsApiKey = (OpenAiTtsApiKey ?? "").Trim();
            App.Settings.OpenAiTtsVoice = string.IsNullOrWhiteSpace(OpenAiTtsVoice) ? "alloy" : OpenAiTtsVoice.Trim();

            App.Settings.ElevenLabsApiKey = (ElevenLabsApiKey ?? "").Trim();
            if (SelectedElevenLabsVoice != null)
            {
                App.Settings.ElevenLabsVoiceId = SelectedElevenLabsVoice.VoiceId;
                App.Settings.ElevenLabsVoiceName = SelectedElevenLabsVoice.Name;
            }

            App.Settings.AzureSpeechKey = (AzureSpeechKey ?? "").Trim();
            App.Settings.AzureSpeechRegion = (AzureSpeechRegion ?? "").Trim();
            if (SelectedAzureVoice != null)
                App.Settings.AzureVoiceName = SelectedAzureVoice.ShortName;

            if (SelectedVoice != null)
            {
                App.Voice.SelectVoice(SelectedVoice.Name);
                App.Settings.VoiceName = SelectedVoice.Name;
            }

            App.Settings.SpeakRepliesByDefault = SpeakRepliesByDefault;
            App.Settings.Save();
            VoiceStatus = "Saved. New replies will use this voice.";
        }

        [RelayCommand]
        private async Task TestVoiceAsync()
        {
            // Save first so App.Voice - which reads settings fresh on every call, not
            // whatever's in these unsaved form fields - actually tests what's shown here.
            SaveVoice();

            if (TtsProvider == "windows" && !App.Voice.CanSpeak)
            {
                VoiceStatus = "No speech voices are available on this Windows install.";
                return;
            }

            VoiceStatus = "Speaking a sample now...";
            await App.Voice.SpeakAsync("Hi, this is Aurora. This is what I sound like.");
        }

        /// <summary>Pulls the current device/entity list from every verified integration - no per-device setup needed.</summary>
        [RelayCommand]
        private async Task DiscoverDevicesAsync()
        {
            if (IsDiscovering) return;
            IsDiscovering = true;
            StatusMessage = "Discovering devices...";
            Devices.Clear();

            try
            {
                if (App.SmartThings.IsConfigured)
                {
                    var stDevices = await App.SmartThings.ListDevicesAsync();
                    foreach (var d in stDevices)
                        Devices.Add(new DiscoveredDevice { Source = "SmartThings", Name = d.Label, Detail = d.Type });
                }

                if (App.HomeAssistant.IsConfigured)
                {
                    var haDevices = await App.HomeAssistant.ListDevicesAsync();
                    foreach (var d in haDevices)
                        Devices.Add(new DiscoveredDevice { Source = "Home Assistant", Name = d.FriendlyName, Detail = d.State });
                }

                StatusMessage = Devices.Count > 0
                    ? $"Found {Devices.Count} device(s)."
                    : "No devices found - connect and test SmartThings or Home Assistant above first.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Discovery failed: {ex.Message}";
            }
            finally
            {
                IsDiscovering = false;
            }
        }
    }
}

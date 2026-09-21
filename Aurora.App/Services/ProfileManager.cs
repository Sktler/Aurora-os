using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Aurora.App.Models;

namespace Aurora.App.Services
{
    /// <summary>Owns local user profiles and their per-profile settings snapshots.</summary>
    public sealed class ProfileManager
    {
        private sealed class ProfileCatalog
        {
            public string ActiveProfileId { get; set; } = "";
            public List<UserProfile> Profiles { get; set; } = new();
        }

        private static string CatalogPath => Path.Combine(AppSettings.ConfigDir, "profiles.json");
        private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };
        private ProfileCatalog _catalog = new();

        public IReadOnlyList<UserProfile> Profiles => _catalog.Profiles;
        public UserProfile ActiveProfile => _catalog.Profiles.First(p => p.Id == _catalog.ActiveProfileId);

        public void Initialize(AppSettings currentSettings)
        {
            Directory.CreateDirectory(AppSettings.ConfigDir);
            try
            {
                if (File.Exists(CatalogPath))
                    _catalog = JsonSerializer.Deserialize<ProfileCatalog>(File.ReadAllText(CatalogPath)) ?? new ProfileCatalog();
            }
            catch { _catalog = new ProfileCatalog(); }

            if (_catalog.Profiles.Count == 0)
            {
                var id = string.IsNullOrWhiteSpace(currentSettings.ProfileId) ? "primary" : currentSettings.ProfileId;
                currentSettings.ProfileId = id;
                _catalog.Profiles.Add(new UserProfile { Id = id, DisplayName = currentSettings.UserName, SettingsJson = SerializeSettings(currentSettings) });
                _catalog.ActiveProfileId = id;
                SaveCatalog();
            }
            else if (!_catalog.Profiles.Any(p => p.Id == _catalog.ActiveProfileId))
            {
                _catalog.ActiveProfileId = _catalog.Profiles[0].Id;
            }

            var active = ActiveProfile;
            if (!string.IsNullOrWhiteSpace(active.SettingsJson))
            {
                try
                {
                    var loaded = JsonSerializer.Deserialize<AppSettings>(active.SettingsJson);
                    if (loaded != null) CopyInto(loaded, currentSettings);
                }
                catch { }
            }
            currentSettings.ProfileId = active.Id;
            currentSettings.SaveGlobalOnly();
            SaveCatalog();
        }

        public UserProfile CreateProfile(string displayName)
        {
            var source = CloneSettings(App.Settings);
            var profile = new UserProfile
            {
                DisplayName = string.IsNullOrWhiteSpace(displayName) ? "New User" : displayName.Trim(),
                SettingsJson = ""
            };
            source.ProfileId = profile.Id;
            source.UserName = profile.DisplayName;
            profile.SettingsJson = SerializeSettings(source);
            _catalog.Profiles.Add(profile);
            SaveCatalog();
            return profile;
        }

        public bool SwitchProfile(string profileId)
        {
            var target = _catalog.Profiles.FirstOrDefault(p => p.Id == profileId);
            if (target == null || target.Id == _catalog.ActiveProfileId) return false;
            SaveActiveSettings();
            App.Settings = JsonSerializer.Deserialize<AppSettings>(target.SettingsJson) ?? new AppSettings();
            App.Settings.ProfileId = target.Id;
            App.Settings.SaveGlobalOnly();
            _catalog.ActiveProfileId = target.Id;
            SaveCatalog();
            App.Memory.SetActiveProfile(target.Id);
            App.RefreshIntegrationClients();
            return true;
        }

        public void SaveActiveSettings()
        {
            var active = ActiveProfile;
            active.DisplayName = App.Settings.UserName;
            App.Settings.ProfileId = active.Id;
            active.SettingsJson = SerializeSettings(App.Settings);
            SaveCatalog();
            App.Settings.SaveGlobalOnly();
        }

        public void SaveSpeakerEmbedding(string profileId, string embedding)
        {
            var profile = _catalog.Profiles.FirstOrDefault(p => p.Id == profileId);
            if (profile == null) return;
            profile.SpeakerEmbedding = embedding;
            profile.SpeakerEnrolled = !string.IsNullOrWhiteSpace(embedding);
            SaveCatalog();
        }

        public UserProfile? GetProfile(string id) => _catalog.Profiles.FirstOrDefault(p => p.Id == id);

        private void SaveCatalog() => File.WriteAllText(CatalogPath, JsonSerializer.Serialize(_catalog, _jsonOptions));
        private static string SerializeSettings(AppSettings settings) => JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = false });
        private static AppSettings CloneSettings(AppSettings settings) => JsonSerializer.Deserialize<AppSettings>(SerializeSettings(settings)) ?? new AppSettings();

        private static void CopyInto(AppSettings source, AppSettings target)
        {
            var json = SerializeSettings(source);
            var loaded = JsonSerializer.Deserialize<AppSettings>(json);
            if (loaded == null) return;
            foreach (var p in typeof(AppSettings).GetProperties().Where(p => p.CanRead && p.CanWrite)) p.SetValue(target, p.GetValue(loaded));
        }
    }
}

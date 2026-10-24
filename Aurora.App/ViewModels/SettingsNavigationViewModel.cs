using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Aurora.App.Models;
using Aurora.App.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Aurora.App.ViewModels
{
    public sealed class SettingsNavigationEntry : ObservableObject
    {
        private readonly string _title;
        private readonly string _description;
        private bool _isPinned;

        public SettingsNavigationEntry(SettingsSection section, string icon, string title, string description)
        {
            Section = section;
            Icon = icon;
            _title = title;
            _description = description;
        }

        public SettingsSection Section { get; }
        public string Icon { get; }
        public string Title => Localizer.GetUiText(_title);
        public string Description => Localizer.GetUiText(_description);
        public bool IsPinned => _isPinned;
        public string PinGlyph => IsPinned ? "★" : "☆";
        public string PinAction => Localizer.FormatUiText(IsPinned ? "Unpin {0} from Home" : "Pin {0} to Home", Title);

        internal void SetPinned(bool value)
        {
            if (SetProperty(ref _isPinned, value, nameof(IsPinned)))
            {
                OnPropertyChanged(nameof(PinGlyph));
                OnPropertyChanged(nameof(PinAction));
            }
        }

        internal void RefreshLabels()
        {
            OnPropertyChanged(nameof(Title));
            OnPropertyChanged(nameof(Description));
            OnPropertyChanged(nameof(PinAction));
        }
    }

    public partial class SettingsNavigationViewModel : ObservableObject
    {
        private readonly AppSettings _settings;
        private readonly Action _save;
        private readonly ObservableCollection<SettingsNavigationEntry> _pinnedEntries = new();

        public SettingsNavigationViewModel(AppSettings settings, Action? save = null)
        {
            _settings = settings;
            _save = save ?? settings.Save;
            Entries = Array.AsReadOnly(new[]
            {
                new SettingsNavigationEntry(SettingsSection.Engine, "🧠", "AI engine", "Provider, API key, reset"),
                new SettingsNavigationEntry(SettingsSection.Models, "🧩", "Models", "Choose your AI model"),
                new SettingsNavigationEntry(SettingsSection.Companions, "🎨", "Companion colors", "Orb color per companion"),
                new SettingsNavigationEntry(SettingsSection.Voice, "🔊", "Voice", "OpenAI, ElevenLabs, Azure, or offline"),
                new SettingsNavigationEntry(SettingsSection.Appearance, "🎨", "Appearance", "Themes & orb skins"),
                new SettingsNavigationEntry(SettingsSection.Documents, "📄", "Documents", "Sift's trusted folder"),
                new SettingsNavigationEntry(SettingsSection.System, "⚙", "System", "Volume, weather & web search"),
                new SettingsNavigationEntry(SettingsSection.Music, "♫", "Music", "Spotify and music settings"),
                new SettingsNavigationEntry(SettingsSection.SmartHome, "⌂", "Smart home", "SmartThings, Home Assistant, Hubitat, Alexa"),
                new SettingsNavigationEntry(SettingsSection.Google, "📧", "Google", "Gmail, Drive & Docs"),
                new SettingsNavigationEntry(SettingsSection.Developer, "🛠", "Developer", "Override-code-gated"),
                new SettingsNavigationEntry(SettingsSection.Memory, "💾", "Memory & data", "Conversations and local storage"),
                new SettingsNavigationEntry(SettingsSection.Tools, "⚒", "Tools", "Permissions, camera, images & MCP")
            });
            PinnedEntries = new ReadOnlyObservableCollection<SettingsNavigationEntry>(_pinnedEntries);
            var savedPins = new HashSet<string>(settings.PinnedSettingsSections ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
            foreach (var entry in Entries)
            {
                entry.SetPinned(savedPins.Contains(entry.Section.ToString()));
                if (entry.IsPinned) _pinnedEntries.Add(entry);
            }
        }

        public IReadOnlyList<SettingsNavigationEntry> Entries { get; }
        public ReadOnlyObservableCollection<SettingsNavigationEntry> PinnedEntries { get; }
        [ObservableProperty] private string _status = "";

        [RelayCommand]
        private void TogglePin(SettingsNavigationEntry? entry)
        {
            if (entry == null || !Entries.Contains(entry)) return;
            var previous = _settings.PinnedSettingsSections;
            _settings.PinnedSettingsSections = Entries
                .Where(item => item == entry ? !item.IsPinned : item.IsPinned)
                .Select(item => item.Section.ToString()).ToList();
            try
            {
                _save();
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                _settings.PinnedSettingsSections = previous;
                Status = Localizer.FormatUiText("Could not save sidebar shortcuts: {0}", ex.Message);
                return;
            }

            entry.SetPinned(!entry.IsPinned);
            // Keep the Home shortcuts in the same order as the Settings hub.
            if (entry.IsPinned)
                _pinnedEntries.Insert(Entries.TakeWhile(item => item != entry).Count(item => item.IsPinned), entry);
            else
                _pinnedEntries.Remove(entry);
            Status = "";
        }

        public void RefreshLabels()
        {
            foreach (var entry in Entries) entry.RefreshLabels();
        }
    }
}

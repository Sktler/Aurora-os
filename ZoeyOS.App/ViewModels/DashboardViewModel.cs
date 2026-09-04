using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ZoeyOS.App.Models;
using ZoeyOS.App.Services;

namespace ZoeyOS.App.ViewModels
{
    public partial class DashboardViewModel : ObservableObject
    {
        public ObservableCollection<CompanionViewModel> Companions { get; } = new();
        public MusicPlayerViewModel MusicPlayer { get; } = new();

        [ObservableProperty] private CompanionViewModel? _selectedCompanion;
        [ObservableProperty] private string _wakeWordStatus = "Listening for “Hey Aurora”";
        [ObservableProperty] private bool _wakeWordFlash;
        [ObservableProperty] private string _userName = "Adam";
        [ObservableProperty] private string _greetingText = "Good evening, Adam";

        [ObservableProperty] private string _weatherSummary = "Waiting for location permission…";
        [ObservableProperty] private string _weatherLocation = "Finding your location…";
        [ObservableProperty] private string _weatherTemperature = "—";
        [ObservableProperty] private string _weatherCondition = "Weather";

        [ObservableProperty] private string _cpuUsage = "—";
        [ObservableProperty] private string _ramUsage = "—";
        [ObservableProperty] private string _diskUsage = "—";
        [ObservableProperty] private string _gpuUsage = "—";
        [ObservableProperty] private bool _gpuAvailable;

        public bool WakeWordAvailable => App.WakeWord?.IsAvailable == true;

        public DashboardViewModel()
        {
            UserName = NormalizeUserName(App.Settings.UserName);
            GreetingText = BuildGreeting();

            var existing = App.Memory.LoadCompanions();
            if (existing.Count == 0)
            {
                existing = SeedDefaults();
                foreach (var c in existing) App.Memory.SaveCompanion(c);
            }
            foreach (var c in existing) Companions.Add(new CompanionViewModel(c));
            SelectedCompanion = Companions.FirstOrDefault();

            if (App.WakeWord != null)
            {
                App.WakeWord.WakeWordDetected += OnWakeWordDetected;
                App.WakeWord.CommandRecognized += OnWakeCommandRecognized;
                App.WakeWord.StatusChanged += OnWakeStatusChanged;
            }

            if (App.Metrics != null)
                App.Metrics.Updated += OnMetricsUpdated;
        }

        public void RefreshUserName()
        {
            UserName = NormalizeUserName(App.Settings.UserName);
            GreetingText = BuildGreeting();
        }

        private static string NormalizeUserName(string? name)
        {
            var trimmed = name?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(trimmed)) return "Adam";
            return trimmed.Length > 40 ? trimmed.Substring(0, 40) : trimmed;
        }

        private string BuildGreeting()
        {
            var hour = DateTime.Now.Hour;
            var part = hour < 12 ? "morning" : hour < 17 ? "afternoon" : "evening";
            return $"Good {part}, {UserName}";
        }

        public async System.Threading.Tasks.Task RefreshWeatherAsync()
        {
            try
            {
                var location = await new LocationService().GetCurrentLocationAsync();
                if (location == null)
                {
                    WeatherLocation = "Location unavailable";
                    WeatherTemperature = "—";
                    WeatherCondition = "Location permission required";
                    WeatherSummary = "Allow Aurora to use Windows location services to show local NWS weather.";
                    return;
                }

                var weather = await App.Weather.GetCurrentWeatherAsync(location.Latitude, location.Longitude);
                WeatherLocation = weather.Location;
                WeatherTemperature = $"{weather.TemperatureF:0}°F";
                WeatherCondition = weather.Condition;
                WeatherSummary = $"{weather.Condition}, {weather.TemperatureF:0}°F • wind {weather.Wind}" +
                                 (weather.PrecipitationChance.HasValue ? $" • {weather.PrecipitationChance.Value:0}% precipitation" : "") +
                                 $" • {(weather.IsObserved ? "NWS observation" : "NWS forecast")}";
            }
            catch (Exception ex)
            {
                WeatherLocation = "Location unavailable";
                WeatherTemperature = "—";
                WeatherCondition = "Weather unavailable";
                WeatherSummary = $"Unable to load National Weather Service data: {ex.Message}";
            }
        }

        private void OnMetricsUpdated(SystemMetrics metrics)
        {
            System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                CpuUsage = FormatPercent(metrics.CpuPercent);
                RamUsage = FormatPercent(metrics.RamPercent);
                DiskUsage = FormatPercent(metrics.DiskPercent);
                GpuAvailable = metrics.GpuPercent >= 0;
                GpuUsage = GpuAvailable ? FormatPercent(metrics.GpuPercent) : "N/A";
            });
        }

        private static string FormatPercent(double value) => value < 0 ? "N/A" : $"{value:0}%";

        private async void OnWakeWordDetected()
        {
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                WakeWordStatus = "What can I do for you?";
                WakeWordFlash = true;
                OnPropertyChanged(nameof(WakeWordFlash));
                await System.Threading.Tasks.Task.Delay(450);
                WakeWordFlash = false;
                OnPropertyChanged(nameof(WakeWordFlash));
            });
        }

        private void OnWakeCommandRecognized(string command)
        {
            System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                var target = SelectedCompanion;
                if (target != null) target.SubmitVoiceUtterance(command);
            });
        }

        private void OnWakeStatusChanged(string status)
        {
            System.Windows.Application.Current?.Dispatcher.BeginInvoke(() => WakeWordStatus = status);
        }

        [RelayCommand] private void Select(CompanionViewModel vm) => SelectedCompanion = vm;

        private static System.Collections.Generic.List<Companion> SeedDefaults() => new()
        {
            new Companion { Name = "Aurora", Role = "Orchestrator", ToolAccess = CompanionToolAccess.General, AccentHex = "#4FD8E8", SystemPrompt = "You are Aurora, the main orchestrator inside the user's personal desktop AI app. You are the default point of contact for anything that doesn't clearly belong to a specialist companion.", LastActivitySummary = "Ready to help with anything." },
            new Companion { Name = "Scout", Role = "Research", ToolAccess = CompanionToolAccess.General, AccentHex = "#5BC0F8", SystemPrompt = "You are Scout, a research and web-information specialist inside the user's personal desktop AI app. You dig into topics deeply and stay skeptical of weak sources.", LastActivitySummary = "Ready to dig into a topic." },
            new Companion { Name = "Nova", Role = "Planner", ToolAccess = CompanionToolAccess.General, AccentHex = "#8C6FF0", SystemPrompt = "You are Nova, a planning, organization, and task-management specialist inside the user's personal desktop AI app.", LastActivitySummary = "Ready to plan something." },
            new Companion { Name = "Sift", Role = "Inbox & Documents", ToolAccess = CompanionToolAccess.InboxDocuments, AccentHex = "#F0B65C", SystemPrompt = "You are Sift, an inbox, document, and information-management specialist inside the user's personal desktop AI app.", LastActivitySummary = "Ready to handle messages and documents." },
            new Companion { Name = "Home", Role = "Home Automation", ToolAccess = CompanionToolAccess.HomeAutomation, AccentHex = "#5BE0A0", SystemPrompt = "You are Home, the home automation specialist inside the user's personal desktop AI app. You control and reason about smart home devices via connected integrations.", LastActivitySummary = "Ready to control the smart home." }
        };
    }
}

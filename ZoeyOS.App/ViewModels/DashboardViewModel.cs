using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ZoeyOS.App.Models;

namespace ZoeyOS.App.ViewModels
{
    public partial class DashboardViewModel : ObservableObject
    {
        public ObservableCollection<CompanionViewModel> Companions { get; } = new();

        public MusicPlayerViewModel MusicPlayer { get; } = new();

        [ObservableProperty]
        private CompanionViewModel? _selectedCompanion;

        public DashboardViewModel()
        {
            var existing = App.Memory.LoadCompanions();

            if (existing.Count == 0)
            {
                existing = SeedDefaults();
                foreach (var c in existing)
                    App.Memory.SaveCompanion(c);
            }

            foreach (var c in existing)
                Companions.Add(new CompanionViewModel(c));

            SelectedCompanion = Companions.FirstOrDefault();
        }

        [RelayCommand]
        private void Select(CompanionViewModel vm) => SelectedCompanion = vm;

        private static System.Collections.Generic.List<Companion> SeedDefaults() => new()
        {
            new Companion
            {
                Name = "Aurora",
                Role = "Orchestrator",
                ToolAccess = CompanionToolAccess.General,
                AccentHex = "#4FD8E8",
                SystemPrompt = "You are Aurora, the main orchestrator inside the user's personal desktop AI app. " +
                                "You are the default point of contact for anything that doesn't clearly belong to a " +
                                "specialist companion (Scout for research, Nova for planning, Sift for inbox/documents, " +
                                "Home for smart-home control) - and you can also just be talked to directly for anything at all. " +
                                "You share memory and context with the other companions and can point the user toward the " +
                                "right one when a task is clearly in their lane.",
                LastActivitySummary = "Ready to help with anything."
            },
            new Companion
            {
                Name = "Scout",
                Role = "Research",
                ToolAccess = CompanionToolAccess.General,
                AccentHex = "#5BC0F8",
                SystemPrompt = "You are Scout, a research and web-information specialist inside the user's personal desktop AI app. " +
                                "You dig into topics deeply, cite reasoning clearly, and stay skeptical of weak sources. " +
                                "Keep answers focused and well-organized.",
                LastActivitySummary = "Ready to dig into a topic."
            },
            new Companion
            {
                Name = "Nova",
                Role = "Planner",
                ToolAccess = CompanionToolAccess.General,
                AccentHex = "#8C6FF0",
                SystemPrompt = "You are Nova, a planning, organization, and task-management specialist inside the user's " +
                                "personal desktop AI app. You help break down goals into concrete steps, timelines, and priorities.",
                LastActivitySummary = "Ready to plan something."
            },
            new Companion
            {
                Name = "Sift",
                Role = "Inbox & Documents",
                ToolAccess = CompanionToolAccess.InboxDocuments,
                AccentHex = "#F0B65C",
                SystemPrompt = "You are Sift, an inbox, document, and information-management specialist inside the user's " +
                                "personal desktop AI app. You help draft, summarize, triage, and organize messages and documents, " +
                                "including content from connected sources like Gmail and Google Docs/Drive.",
                LastActivitySummary = "Ready to handle messages and documents."
            },
            new Companion
            {
                Name = "Home",
                Role = "Home Automation",
                ToolAccess = CompanionToolAccess.HomeAutomation,
                AccentHex = "#5BE0A0",
                SystemPrompt = "You are Home, the home automation specialist inside the user's personal desktop AI app. " +
                                "You control and reason about smart home devices via SmartThings, Alexa, and Home Assistant. " +
                                "When the user asks to control a device, describe clearly what you're doing; " +
                                "actual device commands are executed through the connected integration.",
                LastActivitySummary = "Ready to control the smart home."
            }
        };
    }
}

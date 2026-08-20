using CommunityToolkit.Mvvm.ComponentModel;

namespace ZoeyOS.App.Models
{
    public enum CompanionStatus
    {
        Idle,
        Thinking,
        Working,
        Error
    }

    /// <summary>What tool set a companion is allowed to use. Deliberately separate from the
    /// free-text Role field (which is just display text like "Home Automation" and can be
    /// renamed) - ToolAccess is what actually gates capability, so renaming or retyping a
    /// companion's Role label can never silently strip its tools.</summary>
    public enum CompanionToolAccess
    {
        General,          // weather, web search, Spotify - Aurora, Scout, Nova
        InboxDocuments,    // read/summarize files - Sift
        HomeAutomation     // smart home control - Home
    }

    /// <summary>
    /// One companion = one persona = one system prompt + one conversation thread.
    /// All companions share the same underlying Claude engine but are steered
    /// by their own role definition, giving the "team of specialists" feel.
    /// </summary>
    public partial class Companion : ObservableObject
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        [ObservableProperty]
        private string _name = "New Companion";

        [ObservableProperty]
        private string _role = "Generalist";

        /// <summary>Which tool set this companion is enforced to use. Set explicitly per
        /// companion (see DashboardViewModel.SeedDefaults) rather than inferred from the
        /// Role text, so it can't drift out of sync with a renamed Role label.</summary>
        [ObservableProperty]
        private CompanionToolAccess _toolAccess = CompanionToolAccess.General;

        [ObservableProperty]
        private string _systemPrompt = "You are a helpful, capable assistant.";

        /// <summary>Hex color driving this companion's orb glow.</summary>
        [ObservableProperty]
        private string _accentHex = "#4FD8E8";

        [ObservableProperty]
        private CompanionStatus _status = CompanionStatus.Idle;

        [ObservableProperty]
        private string _lastActivitySummary = "Waiting for a task.";

        /// <summary>True if this companion can be dispatched to run in the background unattended.</summary>
        [ObservableProperty]
        private bool _canRunInBackground = true;
    }
}

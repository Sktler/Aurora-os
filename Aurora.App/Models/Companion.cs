using Aurora.App.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Aurora.App.Models
{
    public enum CompanionStatus
    {
        Idle,
        Thinking,
        Working,
        Error
    }

    /// <summary>What tool families a companion is allowed to use. Deliberately separate from the
    /// free-text Role field (which is just display text like "Home Automation" and can be
    /// renamed) - ToolAccess is what actually gates capability, so renaming or retyping a
    /// companion's Role label can never silently strip its tools.</summary>
    [Flags]
    public enum CompanionToolAccess
    {
        None = 0,
        General = 1 << 0,          // weather, web search, media, image generation, system tools, camera
        InboxDocuments = 1 << 1,    // file and document access - Sift
        HomeAutomation = 1 << 2     // smart home control - Home
    }

    public static class CompanionToolAccessExtensions
    {
        public static bool IsToolAllowed(string toolName, CompanionToolAccess access)
        {
            if (string.IsNullOrWhiteSpace(toolName)) return false;

            var normalized = toolName.Trim();
            if (IsHomeAutomationTool(normalized) && (access & CompanionToolAccess.HomeAutomation) != 0) return true;
            if (IsInboxDocumentsTool(normalized) && (access & CompanionToolAccess.InboxDocuments) != 0) return true;
            if (IsGeneralTool(normalized) && (access & CompanionToolAccess.General) != 0) return true;
            return false;
        }

        public static List<object> GetAllowedToolDefinitions(CompanionToolAccess access)
        {
            var result = new List<object>();
            foreach (var definition in SystemTools.Definitions.Concat(CameraTools.Definitions).Concat(HomeTools.Definitions))
            {
                var name = definition.GetType().GetProperty("name")?.GetValue(definition)?.ToString();
                if (string.IsNullOrWhiteSpace(name)) continue;
                if (IsToolAllowed(name, access)) result.Add(definition);
            }
            return result;
        }

        private static bool IsGeneralTool(string toolName)
        {
            if (string.IsNullOrEmpty(toolName)) return false;
            return toolName.StartsWith("get_weather", StringComparison.OrdinalIgnoreCase)
                || toolName.StartsWith("web_search", StringComparison.OrdinalIgnoreCase)
                || toolName.StartsWith("generate_image", StringComparison.OrdinalIgnoreCase)
                || toolName.StartsWith("get_now_playing", StringComparison.OrdinalIgnoreCase)
                || toolName.StartsWith("list_media_sessions", StringComparison.OrdinalIgnoreCase)
                || toolName.StartsWith("media_", StringComparison.OrdinalIgnoreCase)
                || toolName.StartsWith("set_system_volume", StringComparison.OrdinalIgnoreCase)
                || toolName.StartsWith("toggle_system_mute", StringComparison.OrdinalIgnoreCase)
                || toolName.StartsWith("windows_list_applications", StringComparison.OrdinalIgnoreCase)
                || toolName.StartsWith("windows_launch_application", StringComparison.OrdinalIgnoreCase)
                || toolName.StartsWith("windows_run_command", StringComparison.OrdinalIgnoreCase)
                || toolName.StartsWith("windows_run_powershell", StringComparison.OrdinalIgnoreCase)
                || toolName.StartsWith("windows_run_cmd", StringComparison.OrdinalIgnoreCase)
                || toolName.StartsWith("windows_power_control", StringComparison.OrdinalIgnoreCase)
                || toolName.StartsWith("windows_network_status", StringComparison.OrdinalIgnoreCase)
                || toolName.StartsWith("windows_wifi_status", StringComparison.OrdinalIgnoreCase)
                || toolName.StartsWith("windows_wifi_toggle", StringComparison.OrdinalIgnoreCase)
                || toolName.StartsWith("windows_bluetooth_status", StringComparison.OrdinalIgnoreCase)
                || toolName.StartsWith("windows_capture_screen", StringComparison.OrdinalIgnoreCase)
                || toolName.StartsWith("camera", StringComparison.OrdinalIgnoreCase)
                || toolName.StartsWith("mcp_", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsInboxDocumentsTool(string toolName)
        {
            if (string.IsNullOrEmpty(toolName)) return false;
            return toolName.StartsWith("windows_list_app_adapters", StringComparison.OrdinalIgnoreCase)
                || toolName.StartsWith("windows_app_action", StringComparison.OrdinalIgnoreCase)
                || toolName.StartsWith("windows_open_path", StringComparison.OrdinalIgnoreCase)
                || toolName.StartsWith("windows_read_file", StringComparison.OrdinalIgnoreCase)
                || toolName.StartsWith("windows_write_file", StringComparison.OrdinalIgnoreCase)
                || toolName.StartsWith("windows_get_clipboard", StringComparison.OrdinalIgnoreCase)
                || toolName.StartsWith("windows_set_clipboard", StringComparison.OrdinalIgnoreCase)
                || toolName.StartsWith("windows_undo_last_reversible_action", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsHomeAutomationTool(string toolName)
        {
            if (string.IsNullOrEmpty(toolName)) return false;
            return toolName.StartsWith("list_smart_home_devices", StringComparison.OrdinalIgnoreCase)
                || toolName.StartsWith("control_smart_home_device", StringComparison.OrdinalIgnoreCase);
        }
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

        /// <summary>Provider key (see AIProviderCatalog) this companion uses; empty = follow the global provider.</summary>
        [ObservableProperty]
        private string _providerOverride = "";

        /// <summary>Model for this companion; empty = that provider's configured default model.</summary>
        [ObservableProperty]
        private string _modelOverride = "";

        /// <summary>True if this companion can be dispatched to run in the background unattended.</summary>
        [ObservableProperty]
        private bool _canRunInBackground = true;
    }
}

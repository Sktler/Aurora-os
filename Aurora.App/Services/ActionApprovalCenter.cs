using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace Aurora.App.Services
{
    public enum ToolRiskLevel
    {
        None = 0,
        Low = 1,
        Medium = 2,
        High = 3
    }

    public static class ActionApprovalCenter
    {
        private static readonly Dictionary<string, ToolRiskLevel> RiskLevels = new(StringComparer.OrdinalIgnoreCase)
        {
            ["windows_app_action"] = ToolRiskLevel.High,
            ["windows_launch_application"] = ToolRiskLevel.Medium,
            ["windows_open_path"] = ToolRiskLevel.Low,
            ["windows_write_file"] = ToolRiskLevel.Medium,
            ["windows_set_clipboard"] = ToolRiskLevel.Medium,
            ["windows_run_command"] = ToolRiskLevel.High,
            ["windows_run_powershell"] = ToolRiskLevel.High,
            ["windows_run_cmd"] = ToolRiskLevel.High,
            ["windows_power_control"] = ToolRiskLevel.High,
            ["windows_wifi_toggle"] = ToolRiskLevel.High,
            ["windows_bluetooth_toggle"] = ToolRiskLevel.High,
            ["windows_network_toggle"] = ToolRiskLevel.High,
            ["camera_capture_photo"] = ToolRiskLevel.Medium,
            ["camera_start_preview"] = ToolRiskLevel.Low,
            ["mcp_call_tool"] = ToolRiskLevel.High
        };

        public static ToolRiskLevel GetRiskLevel(string toolName)
        {
            if (string.IsNullOrWhiteSpace(toolName)) return ToolRiskLevel.None;
            return RiskLevels.TryGetValue(toolName, out var riskLevel) ? riskLevel : ToolRiskLevel.None;
        }

        public static string NormalizeApprovalMode(string? mode)
        {
            var normalized = (mode ?? "AskForRisky").Trim();

            if (string.Equals(normalized, "AlwaysAsk", StringComparison.OrdinalIgnoreCase)) return "AlwaysAsk";
            if (string.Equals(normalized, "AllowTrusted", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalized, "TrustedOnly", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalized, "TrustTools", StringComparison.OrdinalIgnoreCase)) return "AllowTrusted";
            return "AskForRisky";
        }

        public static bool IsTrustedTool(string toolName, AppSettings? settings = null)
        {
            var currentSettings = settings ?? App.Settings ?? new AppSettings();
            if (string.IsNullOrWhiteSpace(toolName)) return false;
            return currentSettings.TrustedTools.Any(t => string.Equals(t, toolName, StringComparison.OrdinalIgnoreCase));
        }

        public static bool RequiresConfirmation(string toolName, AppSettings? settings = null, ToolRiskLevel? explicitRiskLevel = null)
        {
            var risk = explicitRiskLevel ?? GetRiskLevel(toolName);
            if (risk == ToolRiskLevel.None) return false;

            var currentSettings = settings ?? App.Settings ?? new AppSettings();
            var approvalMode = NormalizeApprovalMode(currentSettings.ActionApprovalMode);
            if (approvalMode == "AllowTrusted" && IsTrustedTool(toolName, currentSettings))
                return false;

            return approvalMode switch
            {
                "AlwaysAsk" => true,
                "AskForRisky" => risk >= ToolRiskLevel.Medium,
                "AllowTrusted" => risk >= ToolRiskLevel.Low,
                _ => risk >= ToolRiskLevel.Medium
            };
        }

        public static bool ConfirmAction(string toolName, string actionDescription, AppSettings? settings = null, ToolRiskLevel? explicitRiskLevel = null)
        {
            var risk = explicitRiskLevel ?? GetRiskLevel(toolName);
            if (!RequiresConfirmation(toolName, settings, risk)) return true;

            var currentSettings = settings ?? App.Settings ?? new AppSettings();
            var mode = NormalizeApprovalMode(currentSettings.ActionApprovalMode);
            var message =
                $"Aurora needs approval before this side-effecting action runs.{Environment.NewLine}{Environment.NewLine}" +
                $"Tool: {toolName}{Environment.NewLine}" +
                $"Risk: {risk}{Environment.NewLine}" +
                $"Policy: {mode}{Environment.NewLine}{Environment.NewLine}" +
                $"Description: {actionDescription}{Environment.NewLine}{Environment.NewLine}" +
                "Do you want to allow it?";

            if (Application.Current == null) return false;

            var result = Application.Current.Dispatcher.Invoke(() =>
                MessageBox.Show(
                    message,
                    "Aurora approval required",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning,
                    MessageBoxResult.No));

            return result == MessageBoxResult.Yes;
        }
    }
}

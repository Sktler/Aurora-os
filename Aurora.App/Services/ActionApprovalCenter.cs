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

    public enum ActionGrantScope
    {
        None = 0,
        OneTime = 1,
        Session = 2,
        Persistent = 3
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

        private static readonly HashSet<string> SessionGrantedTools = new(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> OneTimeGrantedTools = new(StringComparer.OrdinalIgnoreCase);
        private static readonly object GrantSync = new();

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

        public static bool HasGrant(string toolName, AppSettings? settings = null)
        {
            return IsToolGranted(toolName, settings);
        }

        public static bool IsToolGranted(string toolName, AppSettings? settings = null)
        {
            if (string.IsNullOrWhiteSpace(toolName)) return false;

            var currentSettings = settings ?? App.Settings ?? new AppSettings();
            if (currentSettings.PersistentGrantedTools?.Any(t => string.Equals(t, toolName, StringComparison.OrdinalIgnoreCase)) == true)
                return true;

            lock (GrantSync)
            {
                if (SessionGrantedTools.Contains(toolName)) return true;
                if (OneTimeGrantedTools.Contains(toolName)) return true;
            }

            return false;
        }

        public static ActionGrantScope GetGrantScope(string toolName, AppSettings? settings = null)
        {
            if (string.IsNullOrWhiteSpace(toolName)) return ActionGrantScope.None;

            var currentSettings = settings ?? App.Settings ?? new AppSettings();
            if (currentSettings.PersistentGrantedTools?.Any(t => string.Equals(t, toolName, StringComparison.OrdinalIgnoreCase)) == true)
                return ActionGrantScope.Persistent;

            lock (GrantSync)
            {
                if (SessionGrantedTools.Contains(toolName)) return ActionGrantScope.Session;
                if (OneTimeGrantedTools.Contains(toolName)) return ActionGrantScope.OneTime;
            }

            return ActionGrantScope.None;
        }

        public static bool GrantAction(string toolName, ActionGrantScope scope, AppSettings? settings = null)
        {
            if (string.IsNullOrWhiteSpace(toolName)) return false;
            if (scope == ActionGrantScope.None) return false;

            var currentSettings = settings ?? App.Settings ?? new AppSettings();
            currentSettings.PersistentGrantedTools ??= new List<string>();

            switch (scope)
            {
                case ActionGrantScope.Persistent:
                    if (!currentSettings.PersistentGrantedTools.Any(t => string.Equals(t, toolName, StringComparison.OrdinalIgnoreCase)))
                        currentSettings.PersistentGrantedTools.Add(toolName);
                    lock (GrantSync)
                    {
                        SessionGrantedTools.Remove(toolName);
                        OneTimeGrantedTools.Remove(toolName);
                    }
                    return true;
                case ActionGrantScope.Session:
                    lock (GrantSync)
                    {
                        SessionGrantedTools.Add(toolName);
                        OneTimeGrantedTools.Remove(toolName);
                    }
                    return true;
                case ActionGrantScope.OneTime:
                    lock (GrantSync)
                    {
                        OneTimeGrantedTools.Add(toolName);
                        SessionGrantedTools.Remove(toolName);
                    }
                    return true;
                default:
                    return false;
            }
        }

        public static bool RevokeAction(string toolName, AppSettings? settings = null)
        {
            if (string.IsNullOrWhiteSpace(toolName)) return false;

            var currentSettings = settings ?? App.Settings ?? new AppSettings();
            if (currentSettings.PersistentGrantedTools != null)
                currentSettings.PersistentGrantedTools.RemoveAll(t => string.Equals(t, toolName, StringComparison.OrdinalIgnoreCase));

            lock (GrantSync)
            {
                SessionGrantedTools.Remove(toolName);
                OneTimeGrantedTools.Remove(toolName);
            }

            return true;
        }

        public static bool ConsumeOneTimeGrant(string toolName)
        {
            if (string.IsNullOrWhiteSpace(toolName)) return false;

            lock (GrantSync)
            {
                if (!OneTimeGrantedTools.Contains(toolName)) return false;
                OneTimeGrantedTools.Remove(toolName);
                return true;
            }
        }

        public static void ClearSessionGrants()
        {
            lock (GrantSync)
            {
                SessionGrantedTools.Clear();
                OneTimeGrantedTools.Clear();
            }
        }

        public static bool RequiresConfirmation(string toolName, AppSettings? settings = null, ToolRiskLevel? explicitRiskLevel = null)
        {
            var risk = explicitRiskLevel ?? GetRiskLevel(toolName);
            if (risk == ToolRiskLevel.None) return false;

            var currentSettings = settings ?? App.Settings ?? new AppSettings();
            var scope = GetGrantScope(toolName, currentSettings);
            if (scope != ActionGrantScope.None)
                return false;

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
            var currentSettings = settings ?? App.Settings ?? new AppSettings();
            var risk = explicitRiskLevel ?? GetRiskLevel(toolName);
            var existingScope = GetGrantScope(toolName, currentSettings);
            if (existingScope == ActionGrantScope.OneTime)
            {
                ConsumeOneTimeGrant(toolName);
                return true;
            }

            if (!RequiresConfirmation(toolName, currentSettings, risk)) return true;

            var mode = NormalizeApprovalMode(currentSettings.ActionApprovalMode);
            var message =
                $"Aurora needs approval before this side-effecting action runs.{Environment.NewLine}{Environment.NewLine}" +
                $"Tool: {toolName}{Environment.NewLine}" +
                $"Current grant: {existingScope}{Environment.NewLine}" +
                $"Risk: {risk}{Environment.NewLine}" +
                $"Policy: {mode}{Environment.NewLine}{Environment.NewLine}" +
                $"Description: {actionDescription}{Environment.NewLine}{Environment.NewLine}" +
                "Choose how Aurora should handle this action:";

            if (Application.Current == null) return false;

            var grantScope = Application.Current.Dispatcher.Invoke(() => ShowApprovalWindow(message, toolName));
            switch (grantScope)
            {
                case ActionGrantScope.Persistent:
                    GrantAction(toolName, ActionGrantScope.Persistent, currentSettings);
                    currentSettings.Save();
                    return true;
                case ActionGrantScope.Session:
                    GrantAction(toolName, ActionGrantScope.Session, currentSettings);
                    return true;
                case ActionGrantScope.OneTime:
                    GrantAction(toolName, ActionGrantScope.OneTime, currentSettings);
                    ConsumeOneTimeGrant(toolName);
                    return true;
                default:
                    RevokeAction(toolName, currentSettings);
                    return false;
            }
        }

        private static ActionGrantScope ShowApprovalWindow(string message, string toolName)
        {
            var window = new Window
            {
                Width = 480,
                Height = 260,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ResizeMode = ResizeMode.NoResize,
                Title = "Aurora approval required",
                ShowInTaskbar = false,
                Owner = Application.Current?.MainWindow,
                Topmost = true,
                Content = new System.Windows.Controls.StackPanel
                {
                    Margin = new System.Windows.Thickness(16),
                    Children =
                    {
                        new System.Windows.Controls.TextBlock
                        {
                            Text = message,
                            TextWrapping = System.Windows.TextWrapping.Wrap,
                            Margin = new System.Windows.Thickness(0, 0, 0, 12)
                        },
                        new System.Windows.Controls.TextBlock
                        {
                            Text = $"Tool: {toolName}",
                            FontWeight = System.Windows.FontWeights.SemiBold,
                            Margin = new System.Windows.Thickness(0, 0, 0, 16)
                        },
                        new System.Windows.Controls.WrapPanel
                        {
                            HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                            ItemWidth = 170,
                            ItemHeight = 32,
                            Margin = new System.Windows.Thickness(0, 0, 0, 0),
                            Children =
                            {
                                CreateGrantButton("Allow once", ActionGrantScope.OneTime),
                                CreateGrantButton("Allow for this session", ActionGrantScope.Session),
                                CreateGrantButton("Always allow", ActionGrantScope.Persistent),
                                CreateGrantButton("Deny", ActionGrantScope.None)
                            }
                        }
                    }
                }
            };

            ActionGrantScope result = ActionGrantScope.None;
            window.Closed += (_, _) => { };
            foreach (var button in ((System.Windows.Controls.WrapPanel)((System.Windows.Controls.StackPanel)window.Content).Children[2]).Children)
            {
                if (button is System.Windows.Controls.Button b)
                {
                    b.Click += (_, _) =>
                    {
                        result = (ActionGrantScope)b.Tag;
                        window.DialogResult = true;
                        window.Close();
                    };
                }
            }

            window.ShowDialog();
            return result;
        }

        private static System.Windows.Controls.Button CreateGrantButton(string text, ActionGrantScope scope)
        {
            return new System.Windows.Controls.Button
            {
                Content = text,
                Tag = scope,
                Margin = new System.Windows.Thickness(8),
                MinWidth = 150,
                IsDefault = scope == ActionGrantScope.OneTime,
                IsCancel = scope == ActionGrantScope.None
            };
        }
    }
}

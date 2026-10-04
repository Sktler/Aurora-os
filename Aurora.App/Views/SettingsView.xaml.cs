using System;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Aurora.App.Services;

namespace Aurora.App.Views
{
    public partial class SettingsView : UserControl
    {
        public SettingsView()
        {
            InitializeComponent();
            Loaded += SettingsView_Loaded;
        }

        private void SettingsView_Loaded(object sender, RoutedEventArgs e)
        {
            JamendoClientIdBox.Password = App.Settings.JamendoClientId ?? "";
            var approvalMode = ActionApprovalCenter.NormalizeApprovalMode(App.Settings.ActionApprovalMode);
            foreach (ComboBoxItem item in ApprovalModeBox.Items)
            {
                item.IsSelected = string.Equals(item.Tag as string, approvalMode, StringComparison.OrdinalIgnoreCase);
            }
            TrustedToolsBox.Text = string.Join(", ", App.Settings.TrustedTools ?? new System.Collections.Generic.List<string>());
            UpdateJamendoStatus();
        }

        private void GetJamendoClientId_Click(object sender, RoutedEventArgs e)
        {
            OpenUrl("https://devportal.jamendo.com/");
        }

        private void SaveJamendo_Click(object sender, RoutedEventArgs e)
        {
            var clientId = JamendoClientIdBox.Password.Trim();
            if (string.IsNullOrWhiteSpace(clientId))
            {
                App.Settings.JamendoClientId = "";
                App.Settings.JamendoConnected = false;
                App.Settings.Save();
                App.RefreshIntegrationClients();
                UpdateJamendoStatus();
                return;
            }

            App.Settings.JamendoClientId = clientId;
            App.Settings.JamendoConnected = true;
            App.Settings.Save();
            App.RefreshIntegrationClients();
            UpdateJamendoStatus();
        }

        private void SaveApprovalPolicy_Click(object sender, RoutedEventArgs e)
        {
            var selectedMode = ApprovalModeBox.SelectedValue as string ?? ActionApprovalCenter.NormalizeApprovalMode(App.Settings.ActionApprovalMode);
            App.Settings.ActionApprovalMode = ActionApprovalCenter.NormalizeApprovalMode(selectedMode);
            App.Settings.TrustedTools = TrustedToolsBox.Text
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            App.Settings.Save();
        }

        private void UpdateJamendoStatus()
        {
            JamendoStatusText.Text = App.Settings.JamendoConnected && !string.IsNullOrWhiteSpace(App.Settings.JamendoClientId)
                ? "Connected"
                : "Not connected";
        }

        private static void OpenUrl(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch
            {
                // Keep Settings usable even if the system cannot launch a browser.
            }
        }
    }
}

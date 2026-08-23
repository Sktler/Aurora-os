using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using ZoeyOS.App.Services;

namespace ZoeyOS.App.Views
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

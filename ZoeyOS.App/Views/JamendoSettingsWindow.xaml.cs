using System.Diagnostics;
using System.Windows;

namespace ZoeyOS.App.Views
{
    public partial class JamendoSettingsWindow : Window
    {
        public JamendoSettingsWindow()
        {
            InitializeComponent();
            ClientIdBox.Text = App.Settings.JamendoClientId ?? "";
            UpdateStatus();
        }

        private void GetClientId_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://devportal.jamendo.com/",
                    UseShellExecute = true
                });
            }
            catch
            {
                ErrorText.Text = "Couldn't open the browser. Open the Jamendo Developer Portal manually.";
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            var clientId = ClientIdBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(clientId))
            {
                ErrorText.Text = "Enter a Jamendo Client ID first.";
                return;
            }

            App.Settings.JamendoClientId = clientId;
            App.Settings.JamendoConnected = true;
            App.Settings.Save();
            App.RefreshIntegrationClients();
            ErrorText.Text = "";
            UpdateStatus();
        }

        private void UpdateStatus()
        {
            StatusText.Text = App.Settings.JamendoConnected && !string.IsNullOrWhiteSpace(App.Settings.JamendoClientId)
                ? "Connected"
                : "Not connected";
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}

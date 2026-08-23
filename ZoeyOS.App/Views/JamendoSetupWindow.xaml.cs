using System.Diagnostics;
using System.Windows;

namespace ZoeyOS.App.Views
{
    public partial class JamendoSetupWindow : Window
    {
        public JamendoSetupWindow()
        {
            InitializeComponent();
            ClientIdBox.Text = App.Settings.JamendoClientId;
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
                StatusText.Text = "Couldn't open the browser. Open devportal.jamendo.com manually.";
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            var clientId = ClientIdBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(clientId))
            {
                StatusText.Text = "Enter a Jamendo Client ID first.";
                return;
            }

            App.Settings.JamendoClientId = clientId;
            App.Settings.JamendoConnected = true;
            App.Settings.Save();
            App.RefreshIntegrationClients();
            DialogResult = true;
            Close();
        }

        private void Skip_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}

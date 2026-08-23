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

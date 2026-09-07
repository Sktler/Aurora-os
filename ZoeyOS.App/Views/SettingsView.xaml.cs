using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using ZoeyOS.App.Services;

namespace ZoeyOS.App.Views
{
    public partial class SettingsView : UserControl
    {
        private readonly WindowsUpdateService _updateService = new();
        private AuroraUpdateInfo? _availableUpdate;

        public SettingsView()
        {
            InitializeComponent();
            Loaded += SettingsView_Loaded;
        }

        private void SettingsView_Loaded(object sender, RoutedEventArgs e)
        {
            JamendoClientIdBox.Password = App.Settings.JamendoClientId ?? "";
            UpdateJamendoStatus();
            VersionText.Text = $"Installed version: {_updateService.CurrentVersion}";
        }

        private async void CheckUpdates_Click(object sender, RoutedEventArgs e)
        {
            CheckUpdatesButton.IsEnabled = false;
            InstallUpdateButton.IsEnabled = false;
            UpdateStatusText.Text = "Checking GitHub for updates…";
            _availableUpdate = null;

            try
            {
                _availableUpdate = await _updateService.CheckAsync();
                if (_availableUpdate is null)
                {
                    UpdateStatusText.Text = $"You're up to date ({_updateService.CurrentVersion}).";
                    return;
                }

                UpdateStatusText.Text = $"Update {_availableUpdate.Version} is available.";
                InstallUpdateButton.IsEnabled = true;
            }
            catch (Exception ex)
            {
                UpdateStatusText.Text = $"Update check failed: {ex.Message}";
            }
            finally
            {
                CheckUpdatesButton.IsEnabled = true;
            }
        }

        private async void InstallUpdate_Click(object sender, RoutedEventArgs e)
        {
            if (_availableUpdate is null) return;

            var update = _availableUpdate;
            var answer = MessageBox.Show(
                $"Aurora {update.Version} is ready. Pull and install it now? Aurora will close and restart when the update is applied.",
                "Aurora Update",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);

            if (answer != MessageBoxResult.Yes) return;

            CheckUpdatesButton.IsEnabled = false;
            InstallUpdateButton.IsEnabled = false;
            UpdateProgress.Visibility = Visibility.Visible;
            UpdateProgress.Value = 0;
            UpdateStatusText.Text = $"Downloading Aurora {update.Version}…";

            try
            {
                await _updateService.InstallAsync(update, new Progress<int>(value => UpdateProgress.Value = value));
                UpdateStatusText.Text = "Update downloaded. Restarting Aurora…";
                await Task.Delay(500);
                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                UpdateProgress.Visibility = Visibility.Collapsed;
                UpdateStatusText.Text = $"Update failed: {ex.Message}";
                CheckUpdatesButton.IsEnabled = true;
            }
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

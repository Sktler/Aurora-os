using System;
using System.Linq;
using System.Windows;
using Aurora.App.Models;
using Aurora.App.Services;

namespace Aurora.App.Views
{
    public partial class CapabilitiesView : System.Windows.Controls.UserControl
    {
        private McpServerConnection? _connectedServer;

        public void ShowPhonePanel() => PhonePanel.BringIntoView();

        public CapabilitiesView()
        {
            InitializeComponent();
            LocationPermission.IsChecked = App.Settings.WindowsLocationEnabled;
            WeatherLocationBox.Text = App.Settings.WeatherLocation ?? "";
            WeatherSourceBox.SelectedIndex = App.Settings.WeatherSource == "openmeteo" ? 1 : 0;
            WeatherPageUrlBox.Text = App.Settings.WeatherPageUrl ?? "";
            FilesPermission.IsChecked = App.Settings.WindowsFilesEnabled;
            ScreenPermission.IsChecked = App.Settings.WindowsScreenEnabled;
            ClipboardPermission.IsChecked = App.Settings.WindowsClipboardEnabled;
            ApplicationsPermission.IsChecked = App.Settings.WindowsApplicationsEnabled;
            TerminalPermission.IsChecked = App.Settings.WindowsTerminalEnabled;
            UiAutomationPermission.IsChecked = App.Settings.WindowsUiAutomationEnabled;
            NetworkPermission.IsChecked = App.Settings.WindowsNetworkEnabled;
            PowerPermission.IsChecked = App.Settings.WindowsPowerEnabled;
            CameraPermission.IsChecked = App.Settings.WindowsCameraEnabled;
            MicrophonePermission.IsChecked = App.Settings.WindowsMicrophoneEnabled;
            McpPermission.IsChecked = App.Settings.WindowsMcpEnabled;
            RefreshPhoneLinkUi();
            ImageProviderText.Text = Localizer.FormatUiText("Provider: {0}. Image generation uses Aurora's configured provider and API key.", App.Settings.ImageProvider);
            Loaded += async (_, _) =>
            {
                if (App.Settings.WindowsCameraEnabled)
                    await RefreshCamerasAsync();
            };
        }

        private void SavePermissions_Click(object sender, RoutedEventArgs e)
        {
            App.Settings.WindowsLocationEnabled = LocationPermission.IsChecked == true;
            App.Settings.WindowsFilesEnabled = FilesPermission.IsChecked == true;
            App.Settings.WindowsScreenEnabled = ScreenPermission.IsChecked == true;
            App.Settings.WindowsClipboardEnabled = ClipboardPermission.IsChecked == true;
            App.Settings.WindowsApplicationsEnabled = ApplicationsPermission.IsChecked == true;
            App.Settings.WindowsTerminalEnabled = TerminalPermission.IsChecked == true;
            App.Settings.WindowsUiAutomationEnabled = UiAutomationPermission.IsChecked == true;
            App.Settings.WindowsNetworkEnabled = NetworkPermission.IsChecked == true;
            App.Settings.WindowsPowerEnabled = PowerPermission.IsChecked == true;
            App.Settings.WindowsCameraEnabled = CameraPermission.IsChecked == true;
            App.Settings.WindowsMicrophoneEnabled = MicrophonePermission.IsChecked == true;
            App.Settings.WindowsMcpEnabled = McpPermission.IsChecked == true;
            App.Settings.Save();
            App.RefreshWindowsPermissions();
            if (!App.Settings.WindowsCameraEnabled)
                CameraPicker.ItemsSource = null;
            StatusText.Text = Localizer.GetUiText("Permissions saved. Aurora services will respect these settings.");
        }

        private void RefreshPhoneLinkUi()
        {
            var link = App.PhoneLink;
            PhoneLinkToggle.Content = Localizer.GetUiText(link.IsRunning ? "Stop phone link" : "Start phone link");
            PhoneLinkDetails.Visibility = link.IsRunning ? Visibility.Visible : Visibility.Collapsed;
            if (!link.IsRunning) return;

            var addresses = PhoneLinkService.GetLanAddresses();
            if (addresses.Count == 0)
            {
                PhoneAddressText.Text = Localizer.GetUiText("No Wi-Fi or network connection found. Connect this PC to the same network as your phone.");
                PhonePinText.Text = link.CurrentPin;
                PhoneQrImage.Source = null;
                return;
            }

            var url = link.PairingUrl(addresses[0])!;
            PhoneAddressText.Text = Localizer.FormatUiText("Open on your phone: http://{0}:{1}", addresses[0], link.Port.ToString());
            PhonePinText.Text = Localizer.FormatUiText("PIN: {0}", link.CurrentPin);
            PhoneQrImage.Source = BuildQr(url);
            PhoneStatusText.Text = Localizer.FormatUiText("Connected phones: {0}. The PIN changes every 5 minutes and after each pairing.", link.PairedDeviceCount.ToString());
        }

        private static System.Windows.Media.Imaging.BitmapSource BuildQr(string text)
        {
            using var generator = new QRCoder.QRCodeGenerator();
            using var data = generator.CreateQrCode(text, QRCoder.QRCodeGenerator.ECCLevel.M);
            var matrix = data.ModuleMatrix;
            const int quiet = 4, scale = 8;
            var modules = matrix.Count + quiet * 2;
            var size = modules * scale;
            var pixels = new byte[size * size];
            Array.Fill(pixels, (byte)255);
            for (var y = 0; y < matrix.Count; y++)
                for (var x = 0; x < matrix.Count; x++)
                    if (matrix[y][x])
                        for (var dy = 0; dy < scale; dy++)
                            for (var dx = 0; dx < scale; dx++)
                                pixels[((y + quiet) * scale + dy) * size + (x + quiet) * scale + dx] = 0;
            var bitmap = System.Windows.Media.Imaging.BitmapSource.Create(size, size, 96, 96, System.Windows.Media.PixelFormats.Gray8, null, pixels, size);
            bitmap.Freeze();
            return bitmap;
        }

        private void PhoneLinkToggle_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (App.PhoneLink.IsRunning) App.PhoneLink.Stop();
                else
                {
                    App.PhoneLink.Start();
                    App.PhoneLink.PairedDevicesChanged -= PhoneLink_PairedChanged;
                    App.PhoneLink.PairedDevicesChanged += PhoneLink_PairedChanged;
                }
                RefreshPhoneLinkUi();
            }
            catch (Exception ex)
            {
                StatusText.Text = Localizer.FormatUiText("Couldn't start the phone link: {0}", ex.Message);
            }
        }

        private void PhoneLink_PairedChanged(object? sender, EventArgs e) => Dispatcher.BeginInvoke(RefreshPhoneLinkUi);

        private void NewPin_Click(object sender, RoutedEventArgs e) => RefreshPhoneLinkUi();

        private void RevokePhones_Click(object sender, RoutedEventArgs e)
        {
            App.PhoneLink.RevokeAll();
            StatusText.Text = Localizer.GetUiText("All paired phones were disconnected.");
            if (App.PhoneLink.IsRunning) RefreshPhoneLinkUi();
        }

        private void PairBluetooth_Click(object sender, RoutedEventArgs e) => OpenSettingsUri("ms-settings:bluetooth");
        private void OpenWifi_Click(object sender, RoutedEventArgs e) => OpenSettingsUri("ms-settings:network-wifi");

        private void OpenSettingsUri(string uri)
        {
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(uri) { UseShellExecute = true }); }
            catch (Exception ex) { StatusText.Text = ex.Message; }
        }

        private void SaveWeatherLocation_Click(object sender, RoutedEventArgs e)
        {
            App.Settings.WeatherLocation = WeatherLocationBox.Text.Trim();
            App.Settings.WeatherSource = (WeatherSourceBox.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Tag as string ?? "nws";
            App.Settings.WeatherPageUrl = WeatherPageUrlBox.Text.Trim();
            if (App.Weather != null) App.Weather.WebPageUrl = App.Settings.WeatherPageUrl;
            if (App.Weather != null) App.Weather.UseOpenMeteo = App.Settings.WeatherSource == "openmeteo";
            App.Settings.Save();
            App.RefreshWindowsPermissions();
            StatusText.Text = Localizer.GetUiText(App.Settings.WeatherLocation.Length == 0
                ? "Weather location cleared. Aurora will use Windows location if it is enabled."
                : "Weather location saved.");
        }

        private bool CanUseCamera()
        {
            if (App.Settings.WindowsCameraEnabled)
                return true;

            StatusText.Text = Localizer.GetUiText("Camera permission is disabled in Aurora Settings.");
            return false;
        }

        private async System.Threading.Tasks.Task RefreshCamerasAsync()
        {
            try
            {
                if (!CanUseCamera()) return;
                var selectedId = (CameraPicker.SelectedItem as CameraDeviceInfo)?.Id ?? App.Camera.SelectedDeviceId;
                var devices = await App.Camera.RefreshDevicesAsync();
                if (!App.Settings.WindowsCameraEnabled) return;
                CameraPicker.ItemsSource = devices;
                CameraPicker.DisplayMemberPath = nameof(CameraDeviceInfo.Name);
                CameraPicker.SelectedItem = devices.FirstOrDefault(device => device.Id == selectedId) ?? devices.FirstOrDefault();
                StatusText.Text = devices.Count == 0 ? Localizer.GetUiText("No Windows camera devices were found.") : Localizer.FormatUiText("Found {0} camera device(s).", devices.Count);
            }
            catch (Exception ex) { StatusText.Text = Localizer.FormatUiText("Camera discovery failed: {0}", ex.Message); }
        }

        private async void RefreshCameras_Click(object sender, RoutedEventArgs e) => await RefreshCamerasAsync();
        private async void CheckCamera_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!CanUseCamera()) return;
                var result = await App.Camera.CheckPermissionAsync((CameraPicker.SelectedItem as CameraDeviceInfo)?.Id);
                if (!CanUseCamera()) return;
                StatusText.Text = Localizer.GetUiText(result switch
                {
                    CameraPermissionResult.Allowed => "Windows camera permission is allowed.",
                    CameraPermissionResult.Denied => "Camera permission is blocked. Enable it in Windows Settings > Privacy & security > Camera.",
                    CameraPermissionResult.NoCamera => "No Windows camera devices were found.",
                    CameraPermissionResult.Busy => "Initialize the selected camera before checking its permission.",
                    _ => "Camera permission check failed."
                });
            }
            catch (Exception ex) { StatusText.Text = Localizer.FormatUiText("Permission check failed: {0}", ex.Message); }
        }
        private async void InitializeCamera_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!CanUseCamera()) return;
                var device = CameraPicker.SelectedItem as CameraDeviceInfo;
                await App.Camera.InitializeAsync(device?.Id);
                StatusText.Text = device == null ? Localizer.GetUiText("Camera initialized.") : Localizer.FormatUiText("Camera initialized: {0}", device.Name);
            }
            catch (Exception ex) { StatusText.Text = Localizer.FormatUiText("Camera initialization failed: {0}", ex.Message); }
        }
        private async void CapturePhoto_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!CanUseCamera()) return;
                var file = await App.Camera.CapturePhotoAsync(deviceId: (CameraPicker.SelectedItem as CameraDeviceInfo)?.Id);
                StatusText.Text = Localizer.FormatUiText("Photo captured: {0}", file.Path);
            }
            catch (Exception ex) { StatusText.Text = Localizer.FormatUiText("Capture failed: {0}", ex.Message); }
        }
        private async void ConnectMcp_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!McpPermission.IsChecked.GetValueOrDefault()) { StatusText.Text = Localizer.GetUiText("Enable MCP access above first."); return; }
                var name = ServerName.Text.Trim();
                var command = ServerCommand.Text.Trim();
                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(command)) { StatusText.Text = Localizer.GetUiText("Enter an MCP server name and command."); return; }
                var args = ServerArgs.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                _connectedServer = await App.Mcp.ConnectStdioAsync(name, command, args);
                var tools = await App.Mcp.DiscoverToolsAsync(_connectedServer);
                McpTools.ItemsSource = tools.Select(x => $"{x.Name} — {x.Description}").ToList();
                StatusText.Text = Localizer.FormatUiText("Connected to {0}; discovered {1} tool(s).", name, tools.Count);
            }
            catch (Exception ex) { StatusText.Text = Localizer.FormatUiText("MCP connection failed: {0}", ex.Message); }
        }
        private async void DisconnectMcp_Click(object sender, RoutedEventArgs e)
        {
            if (_connectedServer == null) { StatusText.Text = Localizer.GetUiText("No MCP server is connected in this window."); return; }
            await App.Mcp.DisconnectAsync(_connectedServer);
            _connectedServer = null;
            McpTools.ItemsSource = null;
            StatusText.Text = Localizer.GetUiText("MCP server disconnected.");
        }
        private void RefreshIntegrations_Click(object sender, RoutedEventArgs e)
        {
            App.RefreshIntegrationClients();
            ImageProviderText.Text = Localizer.FormatUiText("Provider: {0}. Integration clients refreshed.", App.Settings.ImageProvider);
            StatusText.Text = Localizer.GetUiText("Integration clients refreshed.");
        }
    }
}

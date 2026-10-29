using System;
using System.Linq;
using System.Windows;
using Aurora.App.Models;
using Aurora.App.Services;

namespace Aurora.App.Views
{
    public partial class CapabilitiesWindow : Window
    {
        private McpServerConnection? _connectedServer;

        public CapabilitiesWindow()
        {
            InitializeComponent();
            DialogLayout.ConstrainToWorkArea(this);
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
            ImageProviderText.Text = Localizer.FormatUiText("Provider: {0}. Image generation uses Aurora's configured provider and API key.", App.Settings.ImageProvider);
            Loaded += async (_, _) => await RefreshCamerasAsync();
        }

        private void SavePermissions_Click(object sender, RoutedEventArgs e)
        {
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
            StatusText.Text = Localizer.GetUiText("Permissions saved. Aurora services will respect these settings.");
        }

        private async System.Threading.Tasks.Task RefreshCamerasAsync()
        {
            try
            {
                var devices = await App.Camera.RefreshDevicesAsync();
                CameraPicker.ItemsSource = devices;
                CameraPicker.DisplayMemberPath = "Name";
                if (CameraPicker.Items.Count > 0) CameraPicker.SelectedIndex = 0;
                StatusText.Text = devices.Count == 0 ? Localizer.GetUiText("No Windows camera devices were found.") : Localizer.FormatUiText("Found {0} camera device(s).", devices.Count);
            }
            catch (Exception ex) { StatusText.Text = Localizer.FormatUiText("Camera discovery failed: {0}", ex.Message); }
        }

        private async void RefreshCameras_Click(object sender, RoutedEventArgs e) => await RefreshCamerasAsync();
        private async void CheckCamera_Click(object sender, RoutedEventArgs e)
        {
            try { StatusText.Text = await App.Camera.CheckPermissionAsync() == CameraPermissionResult.Allowed ? Localizer.GetUiText("Windows camera permission is allowed.") : Localizer.GetUiText("Camera permission is blocked. Enable it in Windows Settings > Privacy & security > Camera."); }
            catch (Exception ex) { StatusText.Text = Localizer.FormatUiText("Permission check failed: {0}", ex.Message); }
        }
        private async void InitializeCamera_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (CameraPermission.IsChecked != true) { StatusText.Text = Localizer.GetUiText("Enable Camera access above first."); return; }
                var device = CameraPicker.SelectedItem as DiscoveredDevice;
                await App.Camera.InitializeAsync(device?.Id);
                StatusText.Text = device == null ? Localizer.GetUiText("Camera initialized.") : Localizer.FormatUiText("Camera initialized: {0}", device.Name);
            }
            catch (Exception ex) { StatusText.Text = Localizer.FormatUiText("Camera initialization failed: {0}", ex.Message); }
        }
        private async void CapturePhoto_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (CameraPermission.IsChecked != true) { StatusText.Text = Localizer.GetUiText("Enable Camera access above first."); return; }
                if (!App.Camera.IsInitialized) await App.Camera.InitializeAsync((CameraPicker.SelectedItem as DiscoveredDevice)?.Id);
                var file = await App.Camera.CapturePhotoAsync();
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

using System;
using System.Linq;
using System.Windows;
using ZoeyOS.App.Models;
using ZoeyOS.App.Services;

namespace ZoeyOS.App.Views
{
    public partial class CapabilitiesWindow : Window
    {
        private McpServerConnection? _connectedServer;

        public CapabilitiesWindow()
        {
            InitializeComponent();
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
            ImageProviderText.Text = $"Provider: {App.Settings.ImageProvider}. Image generation uses Aurora's configured provider and API key.";
        }

        private async void SavePermissions_Click(object sender, RoutedEventArgs e)
        {
            var microphoneWasEnabled = App.Settings.WindowsMicrophoneEnabled;
            var cameraWasEnabled = App.Settings.WindowsCameraEnabled;
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

            var permissions = new WindowsPermissionService();
            if (!microphoneWasEnabled && App.Settings.WindowsMicrophoneEnabled &&
                await permissions.RequestMicrophoneAsync() != PermissionResult.Allowed)
            {
                App.Settings.WindowsMicrophoneEnabled = false;
                MicrophonePermission.IsChecked = false;
            }

            if (!cameraWasEnabled && App.Settings.WindowsCameraEnabled &&
                await permissions.RequestCameraAsync() != PermissionResult.Allowed)
            {
                App.Settings.WindowsCameraEnabled = false;
                CameraPermission.IsChecked = false;
            }

            App.Settings.Save();
            App.RefreshMicrophonePermission();
            StatusText.Text = "Permissions saved. Aurora services will respect these settings.";
        }

        private async System.Threading.Tasks.Task RefreshCamerasAsync()
        {
            try
            {
                if (!App.Settings.WindowsCameraEnabled)
                {
                    StatusText.Text = "Enable Camera access and save before checking for devices.";
                    return;
                }
                var devices = await App.Camera.RefreshDevicesAsync();
                CameraPicker.ItemsSource = devices;
                CameraPicker.DisplayMemberPath = "Name";
                if (CameraPicker.Items.Count > 0) CameraPicker.SelectedIndex = 0;
                StatusText.Text = devices.Count == 0 ? "No Windows camera devices were found." : $"Found {devices.Count} camera device(s).";
            }
            catch (Exception ex) { StatusText.Text = $"Camera discovery failed: {ex.Message}"; }
        }

        private async void RefreshCameras_Click(object sender, RoutedEventArgs e) => await RefreshCamerasAsync();
        private async void CheckCamera_Click(object sender, RoutedEventArgs e)
        {
            try { StatusText.Text = await App.Camera.CheckPermissionAsync() == CameraPermissionResult.Allowed ? "Windows camera permission is allowed." : "Camera permission is blocked. Enable it in Windows Settings > Privacy & security > Camera."; }
            catch (Exception ex) { StatusText.Text = $"Permission check failed: {ex.Message}"; }
        }
        private async void InitializeCamera_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (CameraPermission.IsChecked != true) { StatusText.Text = "Enable Camera access above first."; return; }
                var device = CameraPicker.SelectedItem as DiscoveredDevice;
                await App.Camera.InitializeAsync(device?.Id);
                StatusText.Text = device == null ? "Camera initialized." : $"Camera initialized: {device.Name}";
            }
            catch (Exception ex) { StatusText.Text = $"Camera initialization failed: {ex.Message}"; }
        }
        private async void CapturePhoto_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (CameraPermission.IsChecked != true) { StatusText.Text = "Enable Camera access above first."; return; }
                if (!App.Camera.IsInitialized) await App.Camera.InitializeAsync((CameraPicker.SelectedItem as DiscoveredDevice)?.Id);
                var file = await App.Camera.CapturePhotoAsync();
                StatusText.Text = $"Photo captured: {file.Path}";
            }
            catch (Exception ex) { StatusText.Text = $"Capture failed: {ex.Message}"; }
        }
        private async void ConnectMcp_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!McpPermission.IsChecked.GetValueOrDefault()) { StatusText.Text = "Enable MCP access above first."; return; }
                var name = ServerName.Text.Trim();
                var command = ServerCommand.Text.Trim();
                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(command)) { StatusText.Text = "Enter an MCP server name and command."; return; }
                var args = ServerArgs.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                _connectedServer = await App.Mcp.ConnectStdioAsync(name, command, args);
                var tools = await App.Mcp.DiscoverToolsAsync(_connectedServer);
                McpTools.ItemsSource = tools.Select(x => $"{x.Name} — {x.Description}").ToList();
                StatusText.Text = $"Connected to {name}; discovered {tools.Count} tool(s).";
            }
            catch (Exception ex) { StatusText.Text = $"MCP connection failed: {ex.Message}"; }
        }
        private async void DisconnectMcp_Click(object sender, RoutedEventArgs e)
        {
            if (_connectedServer == null) { StatusText.Text = "No MCP server is connected in this window."; return; }
            await App.Mcp.DisconnectAsync(_connectedServer);
            _connectedServer = null;
            McpTools.ItemsSource = null;
            StatusText.Text = "MCP server disconnected.";
        }
        private void RefreshIntegrations_Click(object sender, RoutedEventArgs e)
        {
            App.RefreshIntegrationClients();
            ImageProviderText.Text = $"Provider: {App.Settings.ImageProvider}. Integration clients refreshed.";
            StatusText.Text = "Integration clients refreshed.";
        }
    }
}

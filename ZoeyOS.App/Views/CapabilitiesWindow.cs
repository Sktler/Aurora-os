using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ZoeyOS.App.Services;

namespace ZoeyOS.App.Views
{
    public sealed class CapabilitiesWindow : Window
    {
        private readonly TextBlock _status = new();
        private readonly ComboBox _camera = new();
        private readonly TextBox _serverName = new();
        private readonly TextBox _serverCommand = new();
        private readonly TextBox _serverArgs = new();
        private readonly ListBox _tools = new();
        private McpServerConnection? _connectedServer;

        public CapabilitiesWindow()
        {
            Title = "Aurora — Tools, Camera & Vision";
            Width = 720; Height = 680; MinWidth = 620; MinHeight = 560;
            Background = (Brush)Application.Current.FindResource("BgDeepTranslucentBrush");
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            var scroll = new ScrollViewer { Padding = new Thickness(24), VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var root = new StackPanel();
            scroll.Content = root;
            Content = scroll;

            root.Children.Add(Header("Tools, Camera & Vision", "These controls operate the real Aurora services. Nothing is reported as connected unless the service actually succeeds."));

            root.Children.Add(Card("Camera & Vision", new UIElement[] {
                new TextBlock { Text = "Windows camera", FontWeight = FontWeights.SemiBold },
                _camera,
                Row(new Button { Content = "Refresh cameras", Padding = new Thickness(14,7,14,7) }, new Button { Content = "Check permission", Padding = new Thickness(14,7,14,7) }),
                Row(new Button { Content = "Enable camera", Padding = new Thickness(14,7,14,7) }, new Button { Content = "Capture photo", Padding = new Thickness(14,7,14,7) }),
            }));

            var cameraCard = (StackPanel)((Border)root.Children[^1]).Child;
            var cameraButtons = cameraCard.Children.OfType<StackPanel>().ToArray();
            var refresh = (Button)cameraButtons[1].Children[0];
            var permission = (Button)cameraButtons[1].Children[1];
            var enable = (Button)cameraButtons[2].Children[0];
            var capture = (Button)cameraButtons[2].Children[1];
            refresh.Click += async (_, _) => await RefreshCamerasAsync();
            permission.Click += async (_, _) => _status.Text = await CheckCameraAsync();
            enable.Click += async (_, _) => await EnableCameraAsync();
            capture.Click += async (_, _) => await CaptureAsync();

            root.Children.Add(Card("MCP Servers", new UIElement[] {
                Labeled("Server name", _serverName, "filesystem"),
                Labeled("Command", _serverCommand, "npx"),
                Labeled("Arguments", _serverArgs, "-y @modelcontextprotocol/server-filesystem C:\\Projects"),
                Row(new Button { Content = "Connect & discover tools", Padding = new Thickness(14,7,14,7) }, new Button { Content = "Disconnect", Padding = new Thickness(14,7,14,7) }),
                new TextBlock { Text = "Discovered tools", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0,12,0,4) },
                _tools
            }));
            var mcpCard = (StackPanel)((Border)root.Children[^1]).Child;
            var mcpRows = mcpCard.Children.OfType<StackPanel>().ToArray();
            var connect = (Button)mcpRows[3].Children[0];
            var disconnect = (Button)mcpRows[3].Children[1];
            connect.Click += async (_, _) => await ConnectMcpAsync();
            disconnect.Click += async (_, _) => await DisconnectMcpAsync();

            root.Children.Add(Card("Image generation", new UIElement[] {
                new TextBlock { Text = "Provider", FontWeight = FontWeights.SemiBold },
                new TextBlock { Text = $"{App.Settings.ImageProvider} — configured through Aurora's existing image-generation client.", Foreground = Brushes.Gray, Margin = new Thickness(0,4,0,8) },
                new Button { Content = "Refresh integration clients", Padding = new Thickness(14,7,14,7) }
            }));
            var imageCard = (StackPanel)((Border)root.Children[^1]).Child;
            ((Button)imageCard.Children[2]).Click += (_, _) => { App.RefreshIntegrationClients(); _status.Text = "Integration clients refreshed."; };

            root.Children.Add(_status);
        }

        private static TextBlock Header(string title, string subtitle) => new TextBlock { Text = title + "\n" + subtitle, FontSize = 20, FontWeight = FontWeights.Bold, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,0,0,18) };
        private Border Card(string title, UIElement[] children)
        {
            var panel = new StackPanel();
            panel.Children.Add(new TextBlock { Text = title, FontSize = 15, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0,0,0,10) });
            foreach (var child in children) { if (child is FrameworkElement fe && fe.Margin == default) fe.Margin = new Thickness(0,0,0,8); panel.Children.Add(child); }
            return new Border { Background = (Brush)FindResource("BgPanelBrush"), CornerRadius = new CornerRadius(12), Padding = new Thickness(18), Margin = new Thickness(0,0,0,14), Child = panel };
        }
        private static StackPanel Row(params UIElement[] controls) { var p = new StackPanel { Orientation = Orientation.Horizontal }; foreach (var c in controls) { if (c is FrameworkElement fe) fe.Margin = new Thickness(0,0,8,0); p.Children.Add(c); } return p; }
        private static StackPanel Labeled(string label, TextBox box, string placeholder) { box.Text = placeholder; box.Padding = new Thickness(8); return new StackPanel { Children = { new TextBlock { Text = label, FontSize = 11 }, box } }; }

        private async System.Threading.Tasks.Task<string> CheckCameraAsync() => await App.Camera.CheckPermissionAsync() ? "Camera permission is allowed." : "Camera permission is blocked or unavailable. Check Windows Settings > Privacy & security > Camera.";
        private async System.Threading.Tasks.Task RefreshCamerasAsync() { var devices = await App.Camera.RefreshDevicesAsync(); _camera.ItemsSource = devices.Select(x => x.Name).ToArray(); if (_camera.Items.Count > 0) _camera.SelectedIndex = 0; _status.Text = $"Found {devices.Count} camera device(s)."; }
        private async System.Threading.Tasks.Task EnableCameraAsync() { try { await App.Camera.InitializeAsync(App.Camera.Devices.FirstOrDefault()?.Id); _status.Text = $"Camera initialized: {App.Camera.Devices.FirstOrDefault()?.Name}"; } catch (Exception ex) { _status.Text = $"Camera error: {ex.Message}"; } }
        private async System.Threading.Tasks.Task CaptureAsync() { try { var file = await App.Camera.CapturePhotoAsync(); _status.Text = $"Captured: {file.Path}"; } catch (Exception ex) { _status.Text = $"Capture error: {ex.Message}"; } }
        private async System.Threading.Tasks.Task ConnectMcpAsync() { try { var args = _serverArgs.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries); _connectedServer = await App.Mcp.ConnectStdioAsync(_serverName.Text.Trim(), _serverCommand.Text.Trim(), args); var tools = await App.Mcp.DiscoverToolsAsync(_connectedServer); _tools.ItemsSource = tools.Select(x => $"{x.Name} — {x.Description}"); _status.Text = $"Connected to {_connectedServer.Name}; discovered {tools.Count} tool(s)."; } catch (Exception ex) { _status.Text = $"MCP error: {ex.Message}"; } }
        private async System.Threading.Tasks.Task DisconnectMcpAsync() { if (_connectedServer != null) { await App.Mcp.DisconnectAsync(_connectedServer); _connectedServer = null; _tools.ItemsSource = null; _status.Text = "MCP server disconnected."; } }
    }
}

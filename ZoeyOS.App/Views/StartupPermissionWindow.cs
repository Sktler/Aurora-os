using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ZoeyOS.App.Views
{
    /// <summary>
    /// Foreground bootstrap window used while Aurora asks for privacy permissions.
    /// The permission cards below provide explicit Allow / Don't allow choices;
    /// Allow then invokes the real Windows permission API for the requested device.
    /// </summary>
    internal sealed class StartupPermissionWindow : Window
    {
        private readonly TextBlock _statusText;
        private readonly TextBlock _countdownText;

        public StartupPermissionWindow()
        {
            Title = "Aurora";
            Width = 560;
            Height = 360;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            Background = new SolidColorBrush(Color.FromRgb(11, 14, 20));
            Foreground = Brushes.White;

            _statusText = new TextBlock
            {
                Text = "Aurora will ask for permission to use location, microphone, and camera.",
                FontSize = 14,
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(Color.FromRgb(208, 215, 226)),
                Margin = new Thickness(0, 0, 0, 12)
            };

            _countdownText = new TextBlock
            {
                Visibility = Visibility.Collapsed,
                FontSize = 13,
                Foreground = new SolidColorBrush(Color.FromRgb(126, 232, 245)),
                Margin = new Thickness(0, 6, 0, 0)
            };

            Content = new Border
            {
                Padding = new Thickness(28),
                Background = new SolidColorBrush(Color.FromRgb(18, 22, 31)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(79, 216, 232)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Child = new StackPanel
                {
                    Children =
                    {
                        new TextBlock
                        {
                            Text = "Aurora needs your permission",
                            FontSize = 26,
                            FontWeight = FontWeights.SemiBold,
                            Margin = new Thickness(0, 0, 0, 8)
                        },
                        _statusText,
                        new TextBlock
                        {
                            Text = "Choose Allow or Don't allow for each capability. When you choose Allow, Aurora immediately asks Windows for the corresponding device permission.",
                            FontSize = 13,
                            TextWrapping = TextWrapping.Wrap,
                            Foreground = new SolidColorBrush(Color.FromRgb(160, 170, 186)),
                            Margin = new Thickness(0, 0, 0, 16)
                        },
                        _countdownText
                    }
                }
            };
        }

        public async Task<bool> AskPermissionAsync(string name, Func<Task<PermissionResult>> request)
        {
            _countdownText.Visibility = Visibility.Collapsed;
            _statusText.Text = $"Aurora would like to use your {name.ToLowerInvariant()}. Choose an option below.";

            var result = MessageBox.Show(
                $"Allow Aurora to access your {name.ToLowerInvariant()}?\n\nChoose Yes to allow access or No to continue without it.",
                $"Aurora — {name} permission",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question,
                MessageBoxResult.No);

            if (result != MessageBoxResult.Yes)
            {
                _statusText.Text = $"{name} access denied. Continuing...";
                await Task.Delay(250);
                return false;
            }

            _statusText.Text = $"Allow selected. Waiting for Windows to finish the {name.ToLowerInvariant()} permission request...";

            try
            {
                var permission = await request();
                _statusText.Text = permission switch
                {
                    PermissionResult.Allowed => $"{name} access allowed. Continuing...",
                    PermissionResult.Denied => $"Windows denied {name.ToLowerInvariant()} access. Continuing...",
                    _ => $"Windows could not grant {name.ToLowerInvariant()} access. Continuing..."
                };
                await Task.Delay(250);
                return permission == PermissionResult.Allowed;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[StartupPermissionWindow] {name} request failed: {ex}");
                _statusText.Text = $"{name} access could not be granted. Continuing...";
                await Task.Delay(250);
                return false;
            }
        }

        public void SetStatus(string text)
        {
            _statusText.Text = text;
            _countdownText.Visibility = Visibility.Collapsed;
        }

        public void ShowCountdown(TimeSpan remaining)
        {
            if (remaining <= TimeSpan.Zero)
            {
                _countdownText.Visibility = Visibility.Collapsed;
                return;
            }

            _statusText.Text = "Permissions complete — Aurora is starting soon.";
            _countdownText.Text = $"Startup delay remaining: {remaining.TotalSeconds:0} seconds";
            _countdownText.Visibility = Visibility.Visible;
        }
    }
}
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ZoeyOS.App.Views
{
    /// <summary>
    /// Foreground bootstrap window used while Aurora asks for privacy permissions.
    /// Each capability gets an explicit Allow / Don't allow dialog before Aurora
    /// invokes the real Windows permission API.
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
                            Text = "For each capability, choose Allow or Don't allow. If you allow it, Aurora then asks Windows for the actual device permission.",
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
            _statusText.Text = $"Aurora would like to use your {name.ToLowerInvariant()}.";

            var decision = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var dialog = new Window
            {
                Owner = this,
                Title = $"Aurora — {name} permission",
                Width = 460,
                Height = 230,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ResizeMode = ResizeMode.NoResize,
                ShowInTaskbar = false,
                Background = new SolidColorBrush(Color.FromRgb(18, 22, 31)),
                Foreground = Brushes.White,
                WindowStyle = WindowStyle.SingleBorderWindow
            };

            var allowButton = new Button
            {
                Content = "Allow",
                Width = 150,
                Height = 42,
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 10, 0),
                IsDefault = true
            };

            var denyButton = new Button
            {
                Content = "Don't allow",
                Width = 150,
                Height = 42,
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                IsCancel = true
            };

            allowButton.Click += (_, _) => decision.TrySetResult(true);
            denyButton.Click += (_, _) => decision.TrySetResult(false);
            dialog.Closed += (_, _) => decision.TrySetResult(false);

            dialog.Content = new Border
            {
                Padding = new Thickness(26),
                Child = new StackPanel
                {
                    Children =
                    {
                        new TextBlock
                        {
                            Text = $"Allow Aurora to access your {name.ToLowerInvariant()}?",
                            FontSize = 20,
                            FontWeight = FontWeights.SemiBold,
                            TextWrapping = TextWrapping.Wrap,
                            Margin = new Thickness(0, 0, 0, 10)
                        },
                        new TextBlock
                        {
                            Text = "Aurora uses this capability for features that depend on it. Your choice can also be controlled later in Windows privacy settings.",
                            FontSize = 13,
                            TextWrapping = TextWrapping.Wrap,
                            Foreground = new SolidColorBrush(Color.FromRgb(180, 190, 204)),
                            Margin = new Thickness(0, 0, 0, 20)
                        },
                        new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            HorizontalAlignment = HorizontalAlignment.Right,
                            Children = { allowButton, denyButton }
                        }
                    }
                }
            };

            dialog.Show();
            dialog.Activate();
            var allowed = await decision.Task;
            if (dialog.IsVisible)
                dialog.Close();

            if (!allowed)
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
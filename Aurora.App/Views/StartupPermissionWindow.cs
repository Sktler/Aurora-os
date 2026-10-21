using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Aurora.App.Services;

namespace Aurora.App.Views
{
    /// <summary>
    /// Foreground bootstrap window used while Aurora asks for privacy permissions.
    /// Each capability gets an explicit Allow / Deny dialog before Aurora
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
            Topmost = true;
            ShowInTaskbar = true;
            WindowState = WindowState.Normal;
            Background = new SolidColorBrush(Color.FromRgb(11, 14, 20));
            Foreground = Brushes.White;
            Loaded += (_, _) =>
            {
                WindowState = WindowState.Normal;
                Activate();
                Focus();
            };

            _statusText = new TextBlock
            {
                Text = "Aurora will ask for permission to use location, microphone, camera, and Windows capabilities.",
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
                            Text = "For each capability, choose Allow or Deny. Device permissions are also confirmed by Windows when required.",
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
            var localizedName = Localizer.GetUiText(name);
            _statusText.Text = Localizer.FormatUiText("Aurora would like to use your {0}.", localizedName.ToLower(System.Globalization.CultureInfo.CurrentUICulture));

            var allowed = await ShowChoiceDialogAsync(name, Localizer.FormatUiText("Allow Aurora to access your {0}?", localizedName.ToLower(System.Globalization.CultureInfo.CurrentUICulture)), true);
            if (!allowed)
            {
                _statusText.Text = Localizer.FormatUiText("{0} access denied. Continuing...", localizedName);
                await Task.Delay(250);
                return false;
            }

            _statusText.Text = Localizer.FormatUiText("Allow selected. Waiting for Windows to finish the {0} permission request...", localizedName.ToLower(System.Globalization.CultureInfo.CurrentUICulture));

            try
            {
                var permission = await request();
                _statusText.Text = permission switch
                {
                    PermissionResult.Allowed => Localizer.FormatUiText("{0} access allowed. Continuing...", localizedName),
                    PermissionResult.Denied => Localizer.FormatUiText("Windows denied {0} access. Continuing...", localizedName.ToLower(System.Globalization.CultureInfo.CurrentUICulture)),
                    _ => Localizer.FormatUiText("Windows could not grant {0} access. Continuing...", localizedName.ToLower(System.Globalization.CultureInfo.CurrentUICulture))
                };
                await Task.Delay(250);
                return permission == PermissionResult.Allowed;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[StartupPermissionWindow] {name} request failed: {ex}");
                _statusText.Text = Localizer.FormatUiText("{0} access could not be granted. Continuing...", localizedName.ToLower(System.Globalization.CultureInfo.CurrentUICulture));
                await Task.Delay(250);
                return false;
            }
        }

        public async Task<bool> AskCapabilityPermissionAsync(string name)
        {
            _countdownText.Visibility = Visibility.Collapsed;
            var localizedName = Localizer.GetUiText(name);
            _statusText.Text = Localizer.FormatUiText("Aurora would like to use {0}.", localizedName.ToLower(System.Globalization.CultureInfo.CurrentUICulture));

            var allowed = await ShowChoiceDialogAsync(name, Localizer.FormatUiText("Allow Aurora to access {0}?", localizedName.ToLower(System.Globalization.CultureInfo.CurrentUICulture)), false);
            _statusText.Text = allowed
                ? Localizer.FormatUiText("{0} access allowed. Continuing...", localizedName)
                : Localizer.FormatUiText("{0} access denied. Continuing...", localizedName);
            await Task.Delay(150);
            return allowed;
        }

        private async Task<bool> ShowChoiceDialogAsync(string name, string question, bool includeWindowsHint)
        {
            var decision = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var dialog = new Window
            {
                Owner = this,
                Title = Localizer.FormatUiText("Aurora — {0} permission", Localizer.GetUiText(name)),
                Width = 460,
                Height = 230,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ResizeMode = ResizeMode.NoResize,
                Topmost = true,
                ShowInTaskbar = true,
                WindowState = WindowState.Normal,
                Background = new SolidColorBrush(Color.FromRgb(18, 22, 31)),
                Foreground = Brushes.White,
                WindowStyle = WindowStyle.SingleBorderWindow
            };
            dialog.Loaded += (_, _) =>
            {
                dialog.WindowState = WindowState.Normal;
                dialog.Activate();
                dialog.Focus();
            };

            var allowButton = new Button
            {
                Content = Localizer.GetUiText("Allow"),
                Width = 150,
                Height = 42,
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 10, 0),
                IsDefault = true
            };
            var denyButton = new Button
            {
                Content = Localizer.GetUiText("Deny"),
                Width = 150,
                Height = 42,
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                IsCancel = true
            };

            allowButton.Click += (_, _) => decision.TrySetResult(true);
            denyButton.Click += (_, _) => decision.TrySetResult(false);
            dialog.Closed += (_, _) => decision.TrySetResult(false);

            var helperText = includeWindowsHint
                ? Localizer.GetUiText("Aurora uses this capability for features that depend on it. Your choice can also be controlled later in Windows privacy settings.")
                : Localizer.GetUiText("You can change this permission later in Windows capabilities settings.");

            dialog.Content = new Border
            {
                Padding = new Thickness(26),
                Child = new StackPanel
                {
                    Children =
                    {
                        new TextBlock
                        {
                            Text = question,
                            FontSize = 20,
                            FontWeight = FontWeights.SemiBold,
                            TextWrapping = TextWrapping.Wrap,
                            Margin = new Thickness(0, 0, 0, 10)
                        },
                        new TextBlock
                        {
                            Text = helperText,
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
            return allowed;
        }

        public void SetStatus(string text)
        {
            _statusText.Text = Localizer.GetUiText(text);
            _countdownText.Visibility = Visibility.Collapsed;
        }

        public void ShowCountdown(TimeSpan remaining)
        {
            if (remaining <= TimeSpan.Zero)
            {
                _countdownText.Visibility = Visibility.Collapsed;
                return;
            }

            _statusText.Text = Localizer.GetUiText("Permissions complete — Aurora is starting soon.");
            _countdownText.Text = Localizer.FormatUiText("Startup delay remaining: {0} seconds", remaining.TotalSeconds.ToString("0", System.Globalization.CultureInfo.CurrentUICulture));
            _countdownText.Visibility = Visibility.Visible;
        }
    }
}
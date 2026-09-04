using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ZoeyOS.App.Views
{
    /// <summary>
    /// Foreground bootstrap window shown while Windows handles privacy consent
    /// and for the configurable post-permission startup delay.
    /// </summary>
    internal sealed class StartupPermissionWindow : Window
    {
        private readonly TextBlock _statusText;
        private readonly TextBlock _countdownText;

        public StartupPermissionWindow()
        {
            Title = "Aurora";
            Width = 500;
            Height = 270;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            Background = new SolidColorBrush(Color.FromRgb(11, 14, 20));
            Foreground = Brushes.White;

            _statusText = new TextBlock
            {
                Text = "Starting Windows permission checks...",
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
                            Text = "Aurora is preparing",
                            FontSize = 26,
                            FontWeight = FontWeights.SemiBold,
                            Margin = new Thickness(0, 0, 0, 8)
                        },
                        new TextBlock
                        {
                            Text = "Windows may ask for location, microphone, and camera access. Please finish each Windows prompt before Aurora continues.",
                            FontSize = 14,
                            TextWrapping = TextWrapping.Wrap,
                            Foreground = new SolidColorBrush(Color.FromRgb(208, 215, 226)),
                            Margin = new Thickness(0, 0, 0, 12)
                        },
                        _statusText,
                        _countdownText
                    }
                }
            };
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
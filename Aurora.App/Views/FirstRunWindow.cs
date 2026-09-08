using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Threading.Tasks;
using System;

namespace Aurora.App.Views
{
    internal sealed class FirstRunWindow : Window
    {
        public FirstRunWindow()
        {
            Title = "Aurora";
            Width = 560;
            Height = 390;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ResizeMode = ResizeMode.NoResize;
            Background = new SolidColorBrush(Color.FromRgb(11, 14, 20));
            Foreground = Brushes.White;

            var progressText = new TextBlock
            {
                Text = "Preparing Aurora setup... 60 seconds",
                FontSize = 13,
                Foreground = new SolidColorBrush(Color.FromRgb(180, 190, 204)),
                Margin = new Thickness(0, 0, 0, 8),
                Visibility = Visibility.Collapsed
            };
            var progressBar = new ProgressBar
            {
                Height = 8,
                Minimum = 0,
                Maximum = 60,
                Margin = new Thickness(0, 0, 0, 20),
                Visibility = Visibility.Collapsed
            };
            var installButton = new Button
            {
                Content = "Install Aurora",
                Width = 180,
                Height = 44,
                FontSize = 15,
                FontWeight = FontWeights.SemiBold,
                IsDefault = true,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            installButton.Click += async (_, _) =>
            {
                installButton.IsEnabled = false;
                progressText.Visibility = Visibility.Visible;
                progressBar.Visibility = Visibility.Visible;
                for (var remaining = 60; remaining > 0; remaining--)
                {
                    progressBar.Value = 60 - remaining;
                    progressText.Text = $"Preparing Aurora setup... {remaining} seconds";
                    await Task.Delay(TimeSpan.FromSeconds(1));
                }
                progressBar.Value = progressBar.Maximum;
                progressText.Text = "Preparation complete. Opening Aurora setup...";
                await Task.Delay(TimeSpan.FromMilliseconds(700));
                DialogResult = true;
            };

            Content = new Border
            {
                Padding = new Thickness(30),
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
                            Text = "Welcome to Aurora",
                            FontSize = 30,
                            FontWeight = FontWeights.SemiBold,
                            Margin = new Thickness(0, 0, 0, 10)
                        },
                        new TextBlock
                        {
                            Text = "Let's get Aurora installed and ready to use.",
                            FontSize = 17,
                            Foreground = new SolidColorBrush(Color.FromRgb(208, 215, 226)),
                            Margin = new Thickness(0, 0, 0, 14)
                        },
                        new TextBlock
                        {
                            Text = "The setup wizard will help you choose an AI provider and model. After that, Aurora will ask which device permissions and Windows capabilities you want to enable.",
                            FontSize = 14,
                            TextWrapping = TextWrapping.Wrap,
                            Foreground = new SolidColorBrush(Color.FromRgb(180, 190, 204)),
                            Margin = new Thickness(0, 0, 0, 28)
                        },
                        progressText,
                        progressBar,
                        installButton
                    }
                }
            };
        }
    }
}

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ZoeyOS.App.Views
{
    /// <summary>
    /// Small foreground bootstrap window used only while Windows displays first-run
    /// privacy consent. The main Aurora window is not created until this completes.
    /// </summary>
    internal sealed class StartupPermissionWindow : Window
    {
        public StartupPermissionWindow()
        {
            Title = "Aurora";
            Width = 430;
            Height = 190;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            Background = new SolidColorBrush(Color.FromRgb(11, 14, 20));
            Foreground = Brushes.White;
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
                            FontSize = 24,
                            FontWeight = FontWeights.SemiBold,
                            Margin = new Thickness(0, 0, 0, 8)
                        },
                        new TextBlock
                        {
                            Text = "Windows may ask for location, microphone, and camera access before Aurora opens.",
                            FontSize = 14,
                            TextWrapping = TextWrapping.Wrap,
                            Foreground = new SolidColorBrush(Color.FromRgb(208, 215, 226))
                        }
                    }
                }
            };
        }
    }
}

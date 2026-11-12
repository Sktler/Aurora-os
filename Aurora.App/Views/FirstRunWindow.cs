using System.Windows;
using System.Windows.Controls;

namespace Aurora.App.Views
{
    internal sealed class FirstRunWindow : Window
    {
        public FirstRunWindow()
        {
            SetResourceReference(BackgroundProperty, "BgDeepBrush");
            SetResourceReference(ForegroundProperty, "TextPrimaryBrush");
            Title = "Aurora";
            Width = 560;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Topmost = true;
            ShowInTaskbar = true;
            WindowState = WindowState.Normal;

            Loaded += (_, _) =>
            {
                WindowState = WindowState.Normal;
                Activate();
                Focus();
            };

            var installButton = new Button
            {
                Content = "Continue",
                MinWidth = 180,
                MinHeight = 44,
                Padding = new Thickness(16, 8, 16, 8),
                FontSize = 15,
                FontWeight = FontWeights.SemiBold,
                IsDefault = true,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            installButton.Click += (_, _) => DialogResult = true;

            Content = new Border
            {
                Padding = new Thickness(30),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Child = new StackPanel
                {
                    Children =
                    {
                        new TextBlock
                        {
                            Text = "Welcome to Aurora",
                            TextWrapping = TextWrapping.Wrap,
                            FontSize = 30,
                            FontWeight = FontWeights.SemiBold,
                            Margin = new Thickness(0, 0, 0, 10)
                        },
                        new TextBlock
                        {
                            Text = "Let's get Aurora installed and ready to use.",
                            TextWrapping = TextWrapping.Wrap,
                            FontSize = 17,
                            Margin = new Thickness(0, 0, 0, 14)
                        },
                        new TextBlock
                        {
                            Text = "The setup wizard will help you choose an AI provider and model. After that, Aurora will ask which device permissions and Windows capabilities you want to enable.",
                            FontSize = 14,
                            TextWrapping = TextWrapping.Wrap,
                            Margin = new Thickness(0, 0, 0, 28)
                        },
                        new TextBlock { Text = "Interface language", Margin = new Thickness(0, 0, 0, 6) },
                        new LanguagePicker { Margin = new Thickness(0, 0, 0, 18) },
                        installButton
                    }
                }
            };
            DialogLayout.UseContentHeight(this);
        }
    }
}

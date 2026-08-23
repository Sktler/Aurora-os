using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ZoeyOS.App.Models;

namespace ZoeyOS.App.Views
{
    public partial class IntegrationsWindow
    {
        protected override void OnContentRendered(EventArgs e)
        {
            base.OnContentRendered(e);
            AddDedicatedJamendoSection();
        }

        private void AddDedicatedJamendoSection()
        {
            var hub = FindDescendant<WrapPanel>(this, panel =>
                panel.Children.OfType<Button>().Any(button =>
                    button.CommandParameter is SettingsSection section && section == SettingsSection.Music));

            if (hub == null || hub.Children.OfType<Button>().Any(button => button.Tag as string == "JamendoSettings"))
                return;

            var button = new Button
            {
                Style = (Style)FindResource("HubTileStyle"),
                Tag = "JamendoSettings",
                ToolTip = "Jamendo Music settings"
            };

            button.Content = new StackPanel
            {
                Children =
                {
                    new TextBlock { Text = "🎶 Jamendo Music", FontWeight = FontWeights.SemiBold, FontSize = 14 },
                    new TextBlock
                    {
                        Text = "Client ID, connection & music access",
                        Foreground = (Brush)FindResource("TextMutedBrush"),
                        Margin = new Thickness(0, 4, 0, 0),
                        TextWrapping = TextWrapping.Wrap
                    }
                }
            };

            button.Click += (_, _) =>
            {
                var window = new JamendoSettingsWindow { Owner = this };
                window.ShowDialog();
            };

            hub.Children.Add(button);
        }
    }
}

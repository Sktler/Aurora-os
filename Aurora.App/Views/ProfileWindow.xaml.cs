using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Aurora.App.Models;

namespace Aurora.App.Views
{
    public partial class ProfileWindow : Window
    {
        public ProfileWindow()
        {
            InitializeComponent();
            RefreshList();
            SpeakerSuggestionsBox.IsChecked = App.Settings.SpeakerRecognitionEnabled;
        }

        private void RefreshList() { ProfilesList.ItemsSource = App.Profiles.Profiles; ProfilesList.SelectedItem = App.Profiles.ActiveProfile; }

        private void AddProfile_Click(object sender, RoutedEventArgs e)
        {
            var input = new TextBox { Text = "New User", Margin = new Thickness(0, 8, 0, 8), Padding = new Thickness(8) };
            var dialog = new Window { Owner = this, Title = "New profile", Width = 340, Height = 170, WindowStartupLocation = WindowStartupLocation.CenterOwner, Content = new StackPanel { Margin = new Thickness(18) } };
            var panel = (StackPanel)dialog.Content; panel.Children.Add(new TextBlock { Text = "Profile name" }); panel.Children.Add(input);
            var save = new Button { Content = "Create", Padding = new Thickness(12), HorizontalAlignment = HorizontalAlignment.Right }; panel.Children.Add(save);
            save.Click += (_, _) => { var p = App.Profiles.CreateProfile(input.Text); RefreshList(); ProfilesList.SelectedItem = p; dialog.Close(); };
            dialog.ShowDialog();
        }

        private void UseSelected_Click(object sender, RoutedEventArgs e)
        {
            if (ProfilesList.SelectedItem is not UserProfile p) return;
            if (!App.Profiles.SwitchProfile(p.Id)) return;
            if (Owner is MainWindow main) main.ReloadAfterProfileSwitch();
            DialogResult = true;
            Close();
        }

        private void EnrollVoice_Click(object sender, RoutedEventArgs e)
        {
            if (ProfilesList.SelectedItem is not UserProfile p) return;
            if (!App.Settings.WindowsMicrophoneEnabled && !App.Settings.MicrophoneDeviceName.Any())
            {
                // Startup permission is the primary gate; this is a friendly reminder only.
                MessageBox.Show(this, "Enable microphone access for Aurora before enrolling a voice.", "Voice profile", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            MessageBox.Show(this, "Voice enrollment is available from the next voice capture. Speak a normal sentence after closing this window; Aurora will store only a local voice signature.", "Voice profile", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        protected override void OnClosed(EventArgs e)
        {
            App.Settings.SpeakerRecognitionEnabled = SpeakerSuggestionsBox.IsChecked == true;
            App.Profiles.SaveActiveSettings();
            base.OnClosed(e);
        }
    }
}

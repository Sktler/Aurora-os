using System;
using System.Windows;
using System.Windows.Controls;
using Aurora.App.Models;

namespace Aurora.App.Views
{
    public partial class ProfileWindow : Window
    {
        private bool _profileSwitched;
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
            _profileSwitched = true;
            if (Owner is MainWindow main) main.ReloadAfterProfileSwitch();
            DialogResult = true;
            Close();
        }

        private void EnrollVoice_Click(object sender, RoutedEventArgs e)
        {
            if (ProfilesList.SelectedItem is not UserProfile p) return;
            App.Voice.BeginSpeakerEnrollment(p.Id);
            MessageBox.Show(this, "Speak a normal sentence using Aurora's microphone after closing this window. Aurora will store only a local voice signature for this profile. Voice matching is not authentication.", "Voice profile", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        protected override void OnClosed(EventArgs e)
        {
            if (!_profileSwitched)
            {
                App.Settings.SpeakerRecognitionEnabled = SpeakerSuggestionsBox.IsChecked == true;
                App.Profiles.SaveActiveSettings();
            }
            base.OnClosed(e);
        }
    }
}

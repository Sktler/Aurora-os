using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using Aurora.App.Models;
using Aurora.App.Services;

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
            RecoveryStoragePathText.Text = RecoveryStorageSettings.DirectoryPath;
        }

        private void RefreshList() { ProfilesList.ItemsSource = App.Profiles.Profiles; ProfilesList.SelectedItem = App.Profiles.ActiveProfile; }

        private void AddProfile_Click(object sender, RoutedEventArgs e)
        {
            var input = new TextBox { Text = "New User", Margin = new Thickness(0, 8, 0, 8), Padding = new Thickness(8, 8, 8, 8) };
            var panel = new StackPanel { Margin = new Thickness(18, 18, 18, 18) };
            var dialog = new Window { Owner = this, Title = "New profile", Width = 340, Height = 170, WindowStartupLocation = WindowStartupLocation.CenterOwner, Content = panel };
            panel.Children.Add(new TextBlock { Text = "Profile name" });
            panel.Children.Add(input);
            var save = new Button { Content = "Create", Padding = new Thickness(12, 12, 12, 12), HorizontalAlignment = HorizontalAlignment.Right };
            panel.Children.Add(save);
            save.Click += (_, _) => { var p = App.Profiles.CreateProfile(input.Text); RefreshList(); ProfilesList.SelectedItem = p; dialog.Close(); };
            dialog.ShowDialog();
        }

        private void DeleteProfile_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.DataContext is not UserProfile p) return;
            var username = p.DisplayName?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(username)) return;

            var input = new TextBox { Margin = new Thickness(0, 8, 0, 8), Padding = new Thickness(8, 8, 8, 8), MinWidth = 280 };
            var confirm = new Button { Content = "Delete profile", Padding = new Thickness(12, 8, 12, 8), IsEnabled = false, HorizontalAlignment = HorizontalAlignment.Right };
            input.TextChanged += (_, _) => { confirm.IsEnabled = string.Equals(input.Text, username, StringComparison.Ordinal); };

            var brushConverter = new System.Windows.Media.BrushConverter();
            var dialog = new Window
            {
                Owner = this,
                Title = "Delete profile",
                Width = 420,
                Height = 240,
                ResizeMode = ResizeMode.NoResize,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Background = brushConverter.ConvertFromString("#0B1018") as System.Windows.Media.Brush,
                Foreground = brushConverter.ConvertFromString("#F2F6FA") as System.Windows.Media.Brush
            };

            var panel = new StackPanel { Margin = new Thickness(20) };
            panel.Children.Add(new TextBlock
            {
                Text = $"Delete profile \"{username}\"? This permanently removes the profile and its profile-scoped Aurora data.",
                TextWrapping = TextWrapping.Wrap
            });
            panel.Children.Add(new TextBlock
            {
                Text = $"Type {username} exactly to enable deletion.",
                Foreground = brushConverter.ConvertFromString("#9AA8B8") as System.Windows.Media.Brush,
                Margin = new Thickness(0, 10, 0, 0)
            });
            panel.Children.Add(input);
            panel.Children.Add(confirm);
            dialog.Content = panel;

            confirm.Click += (_, _) =>
            {
                if (!string.Equals(input.Text, username, StringComparison.Ordinal)) return;
                try
                {
                    if (!App.Profiles.DeleteProfile(p.Id))
                    {
                        MessageBox.Show(this, "Aurora must keep at least one profile, and the selected profile could not be deleted.", "Delete profile", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    RefreshList();
                    MessageBox.Show(this, $"Profile \"{username}\" was deleted.", "Profile deleted", MessageBoxButton.OK, MessageBoxImage.Information);
                    dialog.Close();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, $"Aurora could not delete profile \"{username}\". No partial deletion was intended.\n\n{ex.Message}", "Delete profile", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };

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

        private void RecoveryCode_Click(object sender, RoutedEventArgs e)
        {
            if (ProfilesList.SelectedItem is not UserProfile p || p.Id != App.Profiles.ActiveProfile.Id)
            {
                MessageBox.Show(this, "Select the active profile to manage its recovery code.", "Recovery code", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            new RecoveryCodeWindow { Owner = this }.ShowDialog();
        }

        private void RecoveryStorage_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Choose where Aurora stores recovery-code verifiers",
                Multiselect = false,
                InitialDirectory = RecoveryStorageSettings.DirectoryPath
            };

            if (dialog.ShowDialog(this) != true) return;
            try
            {
                RecoveryStorageSettings.ChangeDirectory(dialog.FolderName);
                RecoveryStoragePathText.Text = RecoveryStorageSettings.DirectoryPath;
                MessageBox.Show(this, "Recovery-code verifier files were moved to the selected folder. The recovery codes themselves are never stored there in plaintext.", "Recovery storage", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Aurora could not change the recovery storage folder. No recovery code was changed.\n\n{ex.Message}", "Recovery storage", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OpenRecoveryFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var path = RecoveryStorageSettings.DirectoryPath;
                System.IO.Directory.CreateDirectory(path);
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"\"{path}\"",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Aurora could not open the recovery storage folder.\n\n{ex.Message}", "Recovery storage", MessageBoxButton.OK, MessageBoxImage.Error);
            }
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

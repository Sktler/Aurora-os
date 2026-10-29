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
            DialogLayout.ConstrainToWorkArea(this);
            RefreshList();
            SpeakerSuggestionsBox.IsChecked = App.Settings.SpeakerRecognitionEnabled;
            RecoveryStoragePathText.Text = Localizer.FormatUiText("Recovery storage: {0}", RecoveryStorageSettings.DirectoryPath);
        }

        private void RefreshList()
        {
            // ProfileManager exposes a plain list, so force WPF to rebind after add/delete.
            ProfilesList.ItemsSource = null;
            ProfilesList.ItemsSource = App.Profiles.Profiles;
            ProfilesList.SelectedItem = App.Profiles.ActiveProfile;
        }

        private void RefreshProfiles_Click(object sender, RoutedEventArgs e) => RefreshList();

        private void AddProfile_Click(object sender, RoutedEventArgs e)
        {
            var input = new TextBox { Text = Localizer.GetUiText("New User"), Margin = new Thickness(0, 8, 0, 8), Padding = new Thickness(8, 8, 8, 8) };
            var panel = new StackPanel { Margin = new Thickness(18, 18, 18, 18) };
            var dialog = new Window { Owner = this, Title = "New profile", Width = 340, WindowStartupLocation = WindowStartupLocation.CenterOwner, Content = panel };
            panel.Children.Add(new TextBlock { Text = "Profile name" });
            panel.Children.Add(input);
            var save = new Button { Content = "Create", Padding = new Thickness(12, 12, 12, 12), HorizontalAlignment = HorizontalAlignment.Right };
            panel.Children.Add(save);
            save.Click += (_, _) => { var p = App.Profiles.CreateProfile(input.Text); RefreshList(); ProfilesList.SelectedItem = p; dialog.Close(); };
            DialogLayout.UseContentHeight(dialog);
            dialog.ShowDialog();
        }

        private void DeleteProfile_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.DataContext is not UserProfile p) return;
            var username = p.DisplayName?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(username)) return;

            var input = new TextBox { Margin = new Thickness(0, 14, 0, 14), Padding = new Thickness(10, 10, 10, 10), MinWidth = 400, FontSize = 15 };
            var confirm = new Button { Content = "Delete profile", Padding = new Thickness(14, 10, 14, 10), IsEnabled = false, MinWidth = 150, HorizontalAlignment = HorizontalAlignment.Right };
            input.TextChanged += (_, _) => { confirm.IsEnabled = string.Equals(input.Text, username, StringComparison.Ordinal); };

            var brushConverter = new System.Windows.Media.BrushConverter();
            var dialog = new Window
            {
                Owner = this,
                Title = "Delete profile",
                Width = 520,
                MinWidth = 520,
                MinHeight = 320,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Background = brushConverter.ConvertFromString("#0B1018") as System.Windows.Media.Brush,
                Foreground = brushConverter.ConvertFromString("#F2F6FA") as System.Windows.Media.Brush
            };

            var panel = new StackPanel { Margin = new Thickness(28), VerticalAlignment = VerticalAlignment.Stretch };
            panel.Children.Add(new TextBlock
            {
                Text = Localizer.FormatUiText("Delete profile \"{0}\"? This permanently removes the profile and its profile-scoped Aurora data.", username),
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 440,
                FontSize = 16,
                LineHeight = 24
            });
            panel.Children.Add(new TextBlock
            {
                Text = Localizer.FormatUiText("Type {0} exactly to enable deletion.", username),
                Foreground = brushConverter.ConvertFromString("#9AA8B8") as System.Windows.Media.Brush,
                Margin = new Thickness(0, 14, 0, 0),
                TextWrapping = TextWrapping.Wrap
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
                        MessageBox.Show(this, Localizer.GetUiText("Aurora must keep at least one profile, and the selected profile could not be deleted."), Localizer.GetUiText("Delete profile"), MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    RefreshList();
                    MessageBox.Show(this, Localizer.FormatUiText("Profile \"{0}\" was deleted.", username), Localizer.GetUiText("Profile deleted"), MessageBoxButton.OK, MessageBoxImage.Information);
                    dialog.Close();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, Localizer.FormatUiText("Aurora could not delete profile \"{0}\". No partial deletion was intended.\n\n{1}", username, ex.Message), Localizer.GetUiText("Delete profile"), MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };

            DialogLayout.UseContentHeight(dialog);
            dialog.ShowDialog();
        }

        private void UseSelected_Click(object sender, RoutedEventArgs e)
        {
            if (ProfilesList.SelectedItem is not UserProfile p) return;
            if (p.Id == App.Profiles.ActiveProfile.Id) return;

            if (new ProfilePasswordStore().HasPassword(p.Id))
            {
                var passwordBox = new PasswordBox
                {
                    MinWidth = 280,
                    Padding = new Thickness(8, 8, 8, 8),
                    Margin = new Thickness(0, 8, 0, 12)
                };
                var dialog = new Window
                {
                    Owner = this,
                    Title = Localizer.FormatUiText("Unlock {0}", p.DisplayName),
                    Width = 380,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner
                };
                var panel = new StackPanel { Margin = new Thickness(20) };
                panel.Children.Add(new TextBlock { Text = Localizer.FormatUiText("Enter the password for {0} to switch profiles.", p.DisplayName), TextWrapping = TextWrapping.Wrap });
                panel.Children.Add(passwordBox);
                var unlock = new Button { Content = "Unlock", Padding = new Thickness(12, 8, 12, 8), HorizontalAlignment = HorizontalAlignment.Right };
                panel.Children.Add(unlock);
                dialog.Content = panel;

                var verified = false;
                unlock.Click += (_, _) =>
                {
                    verified = new ProfilePasswordStore().VerifyPassword(p.Id, passwordBox.Password);
                    if (!verified)
                    {
                        MessageBox.Show(dialog, Localizer.GetUiText("That password is not valid for this profile."), Localizer.GetUiText("Profile password"), MessageBoxButton.OK, MessageBoxImage.Warning);
                        passwordBox.Clear();
                        passwordBox.Focus();
                        return;
                    }

                    dialog.DialogResult = true;
                    dialog.Close();
                };

                passwordBox.KeyDown += (_, e) =>
                {
                    if (e.Key == System.Windows.Input.Key.Enter) unlock.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                };

                DialogLayout.UseContentHeight(dialog);
                dialog.ShowDialog();
                if (!verified) return;
            }

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
                MessageBox.Show(this, Localizer.GetUiText("Select the active profile to manage its recovery code."), Localizer.GetUiText("Recovery code"), MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            new RecoveryCodeWindow { Owner = this }.ShowDialog();
        }

        private void RecoveryStorage_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = Localizer.GetUiText("Choose where Aurora stores recovery-code verifiers"),
                Multiselect = false,
                InitialDirectory = RecoveryStorageSettings.DirectoryPath
            };

            if (dialog.ShowDialog(this) != true) return;
            try
            {
                RecoveryStorageSettings.ChangeDirectory(dialog.FolderName);
                RecoveryStoragePathText.Text = Localizer.FormatUiText("Recovery storage: {0}", RecoveryStorageSettings.DirectoryPath);
                MessageBox.Show(this, Localizer.GetUiText("Recovery-code verifier files were moved to the selected folder. The recovery codes themselves are never stored there in plaintext."), Localizer.GetUiText("Recovery storage"), MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, Localizer.FormatUiText("Aurora could not change the recovery storage folder. No recovery code was changed.\n\n{0}", ex.Message), Localizer.GetUiText("Recovery storage"), MessageBoxButton.OK, MessageBoxImage.Error);
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
                MessageBox.Show(this, Localizer.FormatUiText("Aurora could not open the recovery storage folder.\n\n{0}", ex.Message), Localizer.GetUiText("Recovery storage"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void EnrollVoice_Click(object sender, RoutedEventArgs e)
        {
            if (ProfilesList.SelectedItem is not UserProfile p) return;
            App.Voice.BeginSpeakerEnrollment(p.Id);
            MessageBox.Show(this, Localizer.GetUiText("Speak a normal sentence using Aurora's microphone after closing this window. Aurora will store only a local voice signature for this profile. Voice matching is not authentication."), Localizer.GetUiText("Voice profile"), MessageBoxButton.OK, MessageBoxImage.Information);
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

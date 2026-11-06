using System;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Aurora.App.Services;

namespace Aurora.App.Views
{
    public partial class SetupWindow : Window
    {
        private readonly bool _restartOnSave;
        private string? _generatedRecoveryCode;
        public bool KeySaved { get; private set; }

        public SetupWindow(bool restartOnSave = true)
        {
            InitializeComponent();
            DialogLayout.ConstrainToWorkArea(this);
            _restartOnSave = restartOnSave;
            ProviderSettings.SaveProviderButton.Visibility = Visibility.Collapsed;
            ProfileNameBox.Text = !restartOnSave || string.IsNullOrWhiteSpace(App.Settings.UserName)
                ? Localizer.GetUiText("New User") : App.Settings.UserName;
            SkipButton.Content = Localizer.GetUiText("Cancel");
            SaveContinueButton.Content = Localizer.GetUiText("Save and Continue");
            Closed += (_, _) => ProviderSettings.ViewModel.Dispose();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Normal;
            Activate();
            Focus();
        }

        private void Window_Activated(object sender, EventArgs e)
        {
            var vm = ProviderSettings.ViewModel;
            if (!string.IsNullOrWhiteSpace(vm.ApiKey) || vm.Provider == null) return;
            try
            {
                var clip = Clipboard.ContainsText() ? Clipboard.GetText().Trim() : "";
                if (clip.StartsWith(vm.Provider.KeyShapePrefix, StringComparison.Ordinal) &&
                    clip.Length is >= 20 and <= 120 && !clip.Contains(' '))
                {
                    vm.ApiKey = clip;
                    _ = vm.RefreshAsync();
                }
            }
            catch { }
        }

        private void GeneratePassword_Click(object sender, RoutedEventArgs e)
        {
            var bytes = System.Security.Cryptography.RandomNumberGenerator.GetBytes(18);
            try
            {
                const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@#$%";
                var password = new string(bytes.Select(b => alphabet[b % alphabet.Length]).ToArray());
                ProfilePasswordBox.Password = password;
            }
            finally
            {
                System.Security.Cryptography.CryptographicOperations.ZeroMemory(bytes);
            }
        }

        private void GenerateRecoveryCode_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var code = AuroraRecoveryService.GenerateCode();
                var normalized = AuroraRecoveryService.NormalizeRecoveryCode(code);
                _generatedRecoveryCode = $"{normalized.Substring(0,8)}-{normalized.Substring(8,8)}-{normalized.Substring(16,8)}-{normalized.Substring(24,8)}";

                var dialog = new Window
                {
                    Owner = this,
                    Title = Localizer.Get("Setup.RecoveryTitle"),
                    Width = 560,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner
                };
                var panel = new StackPanel { Margin = new Thickness(24, 24, 24, 24) };
                panel.Children.Add(new TextBlock { Text = Localizer.Get("Setup.RecoveryLead"), TextWrapping = TextWrapping.Wrap, FontSize = 18, FontWeight = FontWeights.SemiBold });
                panel.Children.Add(new TextBlock { Text = Localizer.Get("Setup.RecoveryBody"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 14) });
                var box = new TextBox { Text = _generatedRecoveryCode, IsReadOnly = true, FontSize = 20, Padding = new Thickness(10, 10, 10, 10), HorizontalContentAlignment = HorizontalAlignment.Center };
                panel.Children.Add(box);
                var actions = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 18, 0, 0) };
                var copy = new Button { Content = "Copy", Padding = new Thickness(14, 8, 14, 8), Margin = new Thickness(0, 0, 8, 0) };
                var close = new Button { Content = "Done", Padding = new Thickness(14, 8, 14, 8) };
                copy.Click += (_, _) => Clipboard.SetText(_generatedRecoveryCode);
                close.Click += (_, _) => dialog.Close();
                actions.Children.Add(copy);
                actions.Children.Add(close);
                panel.Children.Add(actions);
                dialog.Content = panel;
                DialogLayout.UseContentHeight(dialog);
                dialog.ShowDialog();
            }
            catch (Exception ex)
            {
                ErrorText.Text = string.Format(Localizer.Get("Setup.RecoveryError"), ex.Message);
                ErrorText.Visibility = Visibility.Visible;
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(ProfileNameBox.Text))
            {
                ErrorText.Text = Localizer.Get("Setup.ProfileNameRequired");
                ErrorText.Visibility = Visibility.Visible;
                return;
            }
            if (string.IsNullOrWhiteSpace(ProfilePasswordBox.Password))
            {
                ErrorText.Text = Localizer.Get("Setup.ProfilePasswordRequired");
                ErrorText.Visibility = Visibility.Visible;
                return;
            }
            try
            {
                if (!ProviderSettings.ViewModel.Save(refreshClients: false)) return;
                if (ProviderSettings.ViewModel.Provider?.Key == "gemini") App.Settings.ImageProvider = "gemini";
                App.Settings.UserName = ProfileNameBox.Text.Trim();
                App.Profiles.SaveActiveSettings();
                new ProfilePasswordStore().SetPassword(App.Profiles.ActiveProfile.Id, ProfilePasswordBox.Password);
                if (!string.IsNullOrWhiteSpace(_generatedRecoveryCode))
                    new ProfileRecoveryCodeStore().SetCode(App.Profiles.ActiveProfile.Id, _generatedRecoveryCode);
                App.Settings.Save();
                KeySaved = true;
                if (_restartOnSave) RestartApp();
                else DialogResult = true;
            }
            catch (Exception ex)
            {
                ErrorText.Text = Localizer.FormatUiText("Could not save settings: {0}", ex.Message);
                ErrorText.Visibility = Visibility.Visible;
            }
        }

        private static void RestartApp()
        {
            var exePath = Process.GetCurrentProcess().MainModule?.FileName;
            if (!string.IsNullOrEmpty(exePath))
            {
                try
                {
                    Process.Start(new ProcessStartInfo { FileName = exePath, UseShellExecute = true });
                }
                catch
                {
                }
            }
            Environment.Exit(0);
        }

        private void Skip_Click(object sender, RoutedEventArgs e)
        {
            KeySaved = false;
            Close();
        }
    }
}

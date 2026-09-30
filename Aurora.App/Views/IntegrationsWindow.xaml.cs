using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using Aurora.App.Models;
using Aurora.App.Services;
using Aurora.App.ViewModels;

namespace Aurora.App.Views
{
    public partial class IntegrationsWindow : Window
    {
        private readonly SettingsSection _initialSection;

        public IntegrationsWindow(IEnumerable<Companion> companions)
            : this(companions, SettingsSection.Hub)
        {
        }

        public IntegrationsWindow(IEnumerable<Companion> companions, SettingsSection initialSection)
        {
            InitializeComponent();
            _initialSection = initialSection;
            DataContext = new IntegrationsViewModel(companions);

            if (DataContext is IntegrationsViewModel vm && !string.IsNullOrEmpty(vm.GoogleClientSecret))
                GoogleSecretBox.Password = vm.GoogleClientSecret;

            Loaded += (_, _) =>
            {
                if (DataContext is IntegrationsViewModel targetVm && _initialSection != SettingsSection.Hub)
                    Dispatcher.BeginInvoke(new Action(() => targetVm.GoToSectionCommand.Execute(_initialSection)), System.Windows.Threading.DispatcherPriority.Loaded);
            };
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            MicaHelper.ApplyMica(new WindowInteropHelper(this).Handle);
        }

        private void OpenUrl(string url)
        {
            try { Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true }); }
            catch (Exception ex) { if (DataContext is IntegrationsViewModel vm) vm.VoiceStatus = $"Couldn't open the browser automatically ({ex.Message}). Go to {url} manually."; }
        }

        private void GetOpenAiKey_Click(object sender, RoutedEventArgs e) => OpenUrl("https://platform.openai.com/api-keys");
        private void GetElevenLabsKey_Click(object sender, RoutedEventArgs e) => OpenUrl("https://elevenlabs.io/app/settings/api-keys");
        private void GetAzureKey_Click(object sender, RoutedEventArgs e) => OpenUrl("https://portal.azure.com/#create/Microsoft.CognitiveServicesSpeechServices");
        private void OverrideCodeBox_PasswordChanged(object sender, RoutedEventArgs e) { if (DataContext is IntegrationsViewModel vm) vm.OverrideCodeInput = OverrideCodeBox.Password; }
        private void UnlockDevMode_Click(object sender, RoutedEventArgs e) { if (DataContext is IntegrationsViewModel vm) { vm.UnlockDevModeCommand.Execute(null); OverrideCodeBox.Password = ""; } }
        private void CreateDeveloperPassword_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not IntegrationsViewModel vm) return;
            if (vm.SetDeveloperPassword(DeveloperPasswordBox.Password, DeveloperPasswordConfirmBox.Password))
            {
                DeveloperPasswordBox.Password = "";
                DeveloperPasswordConfirmBox.Password = "";
                OverrideCodeBox.Password = "";
            }
        }

        private void DeveloperPasswordToggle_Click(object sender, RoutedEventArgs e)
        {
            if (DeveloperPasswordTextBox.Visibility == Visibility.Visible)
            {
                DeveloperPasswordBox.Password = DeveloperPasswordTextBox.Text;
                DeveloperPasswordTextBox.Visibility = Visibility.Collapsed;
                DeveloperPasswordBox.Visibility = Visibility.Visible;
                DeveloperPasswordToggleButton.Content = "Show";
            }
            else
            {
                DeveloperPasswordTextBox.Text = DeveloperPasswordBox.Password;
                DeveloperPasswordBox.Visibility = Visibility.Collapsed;
                DeveloperPasswordTextBox.Visibility = Visibility.Visible;
                DeveloperPasswordToggleButton.Content = "Hide";
            }
        }
        private void SaveOverrides_Click(object sender, RoutedEventArgs e) { if (sender is Button { DataContext: Companion companion } && DataContext is IntegrationsViewModel vm) vm.SaveCompanionOverrides(companion); }
        private void ColorSwatch_Click(object sender, RoutedEventArgs e) { if (sender is Button { Tag: string hex, DataContext: Companion companion } && DataContext is IntegrationsViewModel vm) vm.SetCompanionColor(companion, hex); }
        private void CustomColor_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { DataContext: Companion companion } || DataContext is not IntegrationsViewModel vm)
                return;

            using var dialog = new System.Windows.Forms.ColorDialog { FullOpen = true, AllowFullOpen = true };
            if (!dialog.ShowDialog().Equals(System.Windows.Forms.DialogResult.OK))
                return;

            vm.SetCompanionColor(companion, $"#{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}");
        }
        private void GoogleSecretBox_PasswordChanged(object sender, RoutedEventArgs e) { if (DataContext is IntegrationsViewModel vm) vm.GoogleClientSecret = GoogleSecretBox.Password; }
        private void ChangeApiKey_Click(object sender, RoutedEventArgs e) { new SetupWindow { Owner = this }.ShowDialog(); }

        private void ResetAurora_Click(object sender, RoutedEventArgs e)
        {
            var credentialBox = new PasswordBox
            {
                Width = 320,
                Padding = new Thickness(8, 8, 8, 8),
                Margin = new Thickness(0, 8, 0, 0)
            };

            var panel = new StackPanel { Margin = new Thickness(16, 16, 16, 16) };
            panel.Children.Add(new TextBlock
            {
                Text = "Developer credential required to reset Aurora.",
                TextWrapping = TextWrapping.Wrap
            });
            panel.Children.Add(credentialBox);

            var credentialDialog = new Window
            {
                Title = "Developer reset",
                Owner = this,
                Content = panel,
                Width = 390,
                Height = 165,
                ResizeMode = ResizeMode.NoResize,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ShowInTaskbar = false
            };

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 10, 0, 0)
            };
            var cancel = new Button { Content = "Cancel", Padding = new Thickness(14, 6, 14, 6), Margin = new Thickness(0, 0, 8, 0) };
            var verify = new Button { Content = "Verify", Padding = new Thickness(14, 6, 14, 6), IsDefault = true };
            cancel.Click += (_, _) => credentialDialog.DialogResult = false;
            verify.Click += (_, _) => credentialDialog.DialogResult = true;
            buttons.Children.Add(cancel);
            buttons.Children.Add(verify);
            panel.Children.Add(buttons);

            if (credentialDialog.ShowDialog() != true)
                return;

            var developerPasswordStore = new DeveloperPasswordStore();
            var validDeveloperPassword = developerPasswordStore.HasPassword()
                ? developerPasswordStore.VerifyPassword(credentialBox.Password)
                : DeveloperResetVerifier.Verify(credentialBox.Password);

            if (!validDeveloperPassword)
            {
                MessageBox.Show(this, "The developer password was not accepted. Aurora was not reset.", "Developer reset", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var confirm = MessageBox.Show(this, "This deletes every saved API key, integration token, and every companion's renamed name and chat history, then restarts Aurora as if freshly installed. This can't be undone.\n\nReset Aurora now?", "Reset Aurora completely", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
            if (confirm == MessageBoxResult.Yes)
                App.ResetEverythingAndRestart();
        }

        private void ChooseTrustedFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog { Title = "Choose a folder Sift can read files from", Multiselect = false };
            if (dialog.ShowDialog(this) == true && DataContext is IntegrationsViewModel vm) vm.SetTrustedFolder(dialog.FolderName);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using ZoeyOS.App.Models;
using ZoeyOS.App.Services;
using ZoeyOS.App.ViewModels;

namespace ZoeyOS.App.Views
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
        private void SaveOverrides_Click(object sender, RoutedEventArgs e) { if (sender is Button { DataContext: Companion companion } && DataContext is IntegrationsViewModel vm) vm.SaveCompanionOverrides(companion); }
        private void ColorSwatch_Click(object sender, RoutedEventArgs e) { if (sender is Button { Tag: string hex, DataContext: Companion companion } && DataContext is IntegrationsViewModel vm) vm.SetCompanionColor(companion, hex); }
        private void GoogleSecretBox_PasswordChanged(object sender, RoutedEventArgs e) { if (DataContext is IntegrationsViewModel vm) vm.GoogleClientSecret = GoogleSecretBox.Password; }
        private void ChangeApiKey_Click(object sender, RoutedEventArgs e) { new SetupWindow { Owner = this }.ShowDialog(); }

        private void ResetAurora_Click(object sender, RoutedEventArgs e)
        {
            var confirm = MessageBox.Show(this, "This deletes every saved API key, integration token, and every companion's renamed name and chat history, then restarts Aurora as if freshly installed. This can't be undone.\n\nReset Aurora now?", "Reset Aurora completely", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
            if (confirm == MessageBoxResult.Yes) App.ResetEverythingAndRestart();
        }

        private void ChooseTrustedFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog { Title = "Choose a folder Sift can read files from", Multiselect = false };
            if (dialog.ShowDialog(this) == true && DataContext is IntegrationsViewModel vm) vm.SetTrustedFolder(dialog.FolderName);
        }
    }
}

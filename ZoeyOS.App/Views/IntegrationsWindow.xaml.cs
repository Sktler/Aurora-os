using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using ZoeyOS.App.Models;
using ZoeyOS.App.ViewModels;

namespace ZoeyOS.App.Views
{
    public partial class IntegrationsWindow : Window
    {
        public IntegrationsWindow(IEnumerable<Companion> companions)
        {
            InitializeComponent();
            DataContext = new IntegrationsViewModel(companions);

            // Pre-fill the secret box from stored settings on open (PasswordBox can't be
            // data-bound directly, so this and the PasswordChanged handler below keep it
            // in sync with the view model manually).
            if (DataContext is IntegrationsViewModel vm && !string.IsNullOrEmpty(vm.GoogleClientSecret))
                GoogleSecretBox.Password = vm.GoogleClientSecret;
        }

        private void OverrideCodeBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is IntegrationsViewModel vm)
                vm.OverrideCodeInput = OverrideCodeBox.Password;
        }

        private void UnlockDevMode_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is IntegrationsViewModel vm)
            {
                vm.UnlockDevModeCommand.Execute(null);
                OverrideCodeBox.Password = "";
            }
        }

        private void SaveOverrides_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { DataContext: Companion companion } && DataContext is IntegrationsViewModel vm)
                vm.SaveCompanionOverrides(companion);
        }

        private void ColorSwatch_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: string hex, DataContext: Companion companion } &&
                DataContext is IntegrationsViewModel vm)
            {
                vm.SetCompanionColor(companion, hex);
            }
        }

        private void GoogleSecretBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is IntegrationsViewModel vm)
                vm.GoogleClientSecret = GoogleSecretBox.Password;
        }

        private void ChangeApiKey_Click(object sender, RoutedEventArgs e)
        {
            // SetupWindow's Save restarts the whole app, so nothing further to do here
            // on success. If the user cancels, they're just back where they started.
            var setup = new SetupWindow { Owner = this };
            setup.ShowDialog();
        }

        private void ResetAurora_Click(object sender, RoutedEventArgs e)
        {
            var confirm = MessageBox.Show(
                this,
                "This deletes every saved API key, integration token, and every companion's " +
                "renamed name and chat history, then restarts Aurora as if freshly installed. " +
                "This can't be undone.\n\nReset Aurora now?",
                "Reset Aurora completely",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No);

            if (confirm == MessageBoxResult.Yes)
                App.ResetEverythingAndRestart();
        }

        private void ChooseTrustedFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Choose a folder Sift can read files from",
                Multiselect = false
            };

            if (dialog.ShowDialog(this) == true && DataContext is IntegrationsViewModel vm)
                vm.SetTrustedFolder(dialog.FolderName);
        }
    }
}

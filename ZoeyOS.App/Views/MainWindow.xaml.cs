using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Shell;
using ZoeyOS.App.ViewModels;

namespace ZoeyOS.App.Views
{
    public partial class MainWindow : Window
    {
        private MiniCompanionWindow? _miniCompanion;
        private bool _allowClose;

        public MainWindow() { InitializeComponent(); }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2) { MaximizeRestore_Click(sender, e); return; }
            try { DragMove(); } catch (InvalidOperationException) { }
        }

        private void Minimize_Click(object sender, RoutedEventArgs e) => EnterMiniMode();
        private void MiniMode_Click(object sender, RoutedEventArgs e) => EnterMiniMode();

        private void EnterMiniMode()
        {
            WindowState = WindowState.Normal;
            Hide();
            ShowMiniCompanion();
        }

        private void MaximizeRestore_Click(object sender, RoutedEventArgs e)
        {
            if (WindowState == WindowState.Maximized) SystemCommands.RestoreWindow(this);
            else SystemCommands.MaximizeWindow(this);
        }

        private void Close_Click(object sender, RoutedEventArgs e) => ExitApplication();

        private void Window_StateChanged(object sender, EventArgs e)
        {
            var maximized = WindowState == WindowState.Maximized;
            MaximizeRestoreButton.Content = maximized ? "🗗" : "🗖";
            MaximizeRestoreButton.ToolTip = maximized ? "Restore" : "Maximize";
        }

        private void ShowMiniCompanion()
        {
            if (_miniCompanion != null) { _miniCompanion.Activate(); return; }
            _miniCompanion = new MiniCompanionWindow();
            _miniCompanion.RestoreRequested += MiniCompanion_RestoreRequested;
            _miniCompanion.ExitRequested += MiniCompanion_ExitRequested;
            _miniCompanion.Closed += (_, _) => _miniCompanion = null;
            _miniCompanion.Show();
            _miniCompanion.Activate();
        }

        private void MiniCompanion_RestoreRequested(object? sender, EventArgs e)
        {
            _miniCompanion?.Close(); _miniCompanion = null;
            Show(); WindowState = WindowState.Normal; Activate(); Focus();
        }

        private void MiniCompanion_ExitRequested(object? sender, EventArgs e)
        {
            _miniCompanion?.Close(); _miniCompanion = null; ExitApplication();
        }

        private void ExitApplication() { _allowClose = true; _miniCompanion?.Close(); _miniCompanion = null; Close(); }

        private void OpenIntegrations_Click(object sender, RoutedEventArgs e)
        {
            var companions = DataContext is DashboardViewModel dvm
                ? dvm.Companions.Select(c => c.Companion)
                : Enumerable.Empty<Models.Companion>();
            new IntegrationsWindow(companions) { Owner = this }.ShowDialog();
        }

        private void OpenCapabilities_Click(object sender, RoutedEventArgs e)
            => new CapabilitiesWindow { Owner = this }.ShowDialog();

        private void Navigation_Click(object sender, RoutedEventArgs e)
        {
            var target = (sender as Button)?.Tag?.ToString()?.ToLowerInvariant();
            switch (target)
            {
                case "chat":
                    Chat_Click(sender, e);
                    break;
                case "memory":
                    ComposerTextBox.Text = "Show me my saved memories.";
                    ComposerTextBox.Focus();
                    ComposerTextBox.CaretIndex = ComposerTextBox.Text.Length;
                    break;
                case "tasks":
                    ComposerTextBox.Text = "Show me my tasks.";
                    ComposerTextBox.Focus();
                    ComposerTextBox.CaretIndex = ComposerTextBox.Text.Length;
                    break;
                case "schedule":
                    ComposerTextBox.Text = "Show me my schedule.";
                    ComposerTextBox.Focus();
                    ComposerTextBox.CaretIndex = ComposerTextBox.Text.Length;
                    break;
                case "music":
                    PlayMusic_Click(sender, e);
                    break;
                case "smarthome":
                    SmartHome_Click(sender, e);
                    break;
                case "tools":
                case "camera":
                    OpenCapabilities_Click(sender, e);
                    break;
                case "settings":
                    OpenIntegrations_Click(sender, e);
                    break;
            }
        }

        private void Chat_Click(object sender, RoutedEventArgs e)
        {
            ComposerTextBox.Focus();
            ComposerTextBox.SelectAll();
        }

        private void StartTask_Click(object sender, RoutedEventArgs e)
        {
            ComposerTextBox.Text = "Help me start a task: ";
            ComposerTextBox.CaretIndex = ComposerTextBox.Text.Length;
            ComposerTextBox.Focus();
        }

        private async void PlayMusic_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is DashboardViewModel dvm)
                await dvm.MusicPlayer.TogglePlayPauseCommand.ExecuteAsync(null);
        }

        private async void TakePhoto_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!App.Settings.WindowsCameraEnabled)
                {
                    new CapabilitiesWindow { Owner = this }.ShowDialog();
                    return;
                }
                if (!App.Camera.IsInitialized) await App.Camera.InitializeAsync();
                var file = await App.Camera.CapturePhotoAsync();
                MessageBox.Show(this, $"Photo saved to:\n{file.Path}", "Aurora Camera", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Aurora Camera", MessageBoxButton.OK, MessageBoxImage.Warning); }
        }

        private async void RecordVideo_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!App.Settings.WindowsCameraEnabled)
                {
                    new CapabilitiesWindow { Owner = this }.ShowDialog();
                    return;
                }
                if (!App.Camera.IsInitialized) await App.Camera.InitializeAsync();
                if (App.Camera.IsRecording)
                {
                    await App.Camera.StopRecordingAsync();
                    RecordVideoButton.Content = "▣\nRecord Video";
                    MessageBox.Show(this, "Video recording stopped.", "Aurora Camera", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                var file = await App.Camera.StartRecordingAsync();
                RecordVideoButton.Content = "■\nStop Recording";
                MessageBox.Show(this, $"Recording started.\n\nFile:\n{file.Path}\n\nClick Record Video again to stop.", "Aurora Camera", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Aurora Camera", MessageBoxButton.OK, MessageBoxImage.Warning); }
        }

        private void SmartHome_Click(object sender, RoutedEventArgs e) => OpenIntegrations_Click(sender, e);

        private void AddReminder_Click(object sender, RoutedEventArgs e)
        {
            ComposerTextBox.Text = "Create a reminder: ";
            ComposerTextBox.CaretIndex = ComposerTextBox.Text.Length;
            ComposerTextBox.Focus();
        }

        private async void Send_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not DashboardViewModel dvm || dvm.SelectedCompanion == null) return;
            var source = sender as Button;
            var text = source == null || source == FindName("SendButton") as Button
                ? ComposerTextBox.Text.Trim()
                : (source == null ? ComposerTextBox.Text.Trim() : QuickPromptTextBox.Text.Trim());
            if (source != null && source.Name == "QuickPromptSendButton") text = QuickPromptTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(text)) return;
            dvm.SelectedCompanion.DraftMessage = text;
            await dvm.SelectedCompanion.SendCommand.ExecuteAsync(null);
            ComposerTextBox.Clear();
            QuickPromptTextBox.Clear();
        }

        private void Attach_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog { Title = "Attach a file", Multiselect = false, Filter = "All files (*.*)|*.*" };
            if (dialog.ShowDialog(this) != true) return;
            if (DataContext is DashboardViewModel dvm && dvm.SelectedCompanion != null) dvm.SelectedCompanion.AttachFile(dialog.FileName);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Shell;
using ZoeyOS.App.Models;
using ZoeyOS.App.Services;
using ZoeyOS.App.ViewModels;

namespace ZoeyOS.App.Views
{
    public partial class MainWindow : Window
    {
        private MiniCompanionWindow? _miniCompanion;
        private bool _leftCollapsed;
        private bool _rightCollapsed;

        public MainWindow()
        {
            InitializeComponent();
            AuroraOrbLoader.Apply(this);
            ComposerTextBox.KeyDown += ComposerTextBox_KeyDown;
            Loaded += MainWindow_Loaded;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= MainWindow_Loaded;

            // Privacy consent is requested by App.OnStartup before this window is
            // created. The dashboard only needs to fetch weather after it is visible.
            if (DataContext is DashboardViewModel dvm)
                await dvm.RefreshWeatherAsync();
        }

        private void ComposerTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter || Keyboard.Modifiers != ModifierKeys.None) return;
            e.Handled = true;
            _ = SendComposerAsync();
        }

        private async System.Threading.Tasks.Task SendComposerAsync()
        {
            if (DataContext is not DashboardViewModel dvm || dvm.SelectedCompanion == null) return;
            var text = ComposerTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(text) || dvm.SelectedCompanion.IsBusy) return;
            dvm.SelectedCompanion.DraftMessage = text;
            try { await dvm.SelectedCompanion.SendCommand.ExecuteAsync(null); }
            finally { ComposerTextBox.Clear(); }
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2) { MaximizeRestore_Click(sender, e); return; }
            try { DragMove(); } catch (InvalidOperationException) { }
        }

        private void ToggleLeftSidebar_Click(object sender, RoutedEventArgs e)
        {
            _leftCollapsed = !_leftCollapsed;
            LeftSidebarColumn.Width = _leftCollapsed ? new GridLength(0) : new GridLength(230);
            ToggleLeftSidebarButton.ToolTip = _leftCollapsed ? "Show navigation" : "Hide navigation";
        }

        private void ToggleRightSidebar_Click(object sender, RoutedEventArgs e)
        {
            _rightCollapsed = !_rightCollapsed;
            RightSidebarColumn.Width = _rightCollapsed ? new GridLength(0) : new GridLength(350);
            ToggleRightSidebarButton.ToolTip = _rightCollapsed ? "Show information panel" : "Hide information panel";
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
            _miniCompanion?.Close();
            _miniCompanion = null;
            Show();
            WindowState = WindowState.Normal;
            Activate();
            Focus();
        }

        private void MiniCompanion_ExitRequested(object? sender, EventArgs e)
        {
            _miniCompanion?.Close();
            _miniCompanion = null;
            ExitApplication();
        }

        private void ExitApplication()
        {
            _miniCompanion?.Close();
            _miniCompanion = null;
            Close();
        }

        private IEnumerable<Companion> GetCompanions() =>
            DataContext is DashboardViewModel dvm
                ? dvm.Companions.Select(c => c.Companion)
                : Enumerable.Empty<Companion>();

        private void OpenSettings(SettingsSection section = SettingsSection.Hub)
        {
            var window = new IntegrationsWindow(GetCompanions(), section) { Owner = this };
            window.ShowDialog();
        }

        private void OpenIntegrations_Click(object sender, RoutedEventArgs e) => OpenSettings();
        private void OpenCapabilities_Click(object sender, RoutedEventArgs e) => new CapabilitiesWindow { Owner = this }.ShowDialog();

        private void Navigation_Click(object sender, RoutedEventArgs e)
        {
            var target = (sender as Button)?.Tag?.ToString()?.ToLowerInvariant();
            switch (target)
            {
                case "home": Activate(); break;
                case "chat": OpenChatWindow(); break;
                case "memory": new MemoryWindow { Owner = this }.ShowDialog(); break;
                case "music": OpenSettings(SettingsSection.Music); break;
                case "smarthome": OpenSettings(SettingsSection.SmartHome); break;
                case "tools":
                case "camera": OpenCapabilities_Click(sender, e); break;
            }
        }

        private void OpenChatWindow()
        {
            if (DataContext is DashboardViewModel dvm && dvm.SelectedCompanion != null)
                new ChatWindow(dvm.SelectedCompanion) { Owner = this }.Show();
        }

        private void SetPrompt(string prompt)
        {
            ComposerTextBox.Text = prompt;
            ComposerTextBox.CaretIndex = ComposerTextBox.Text.Length;
            ComposerTextBox.Focus();
        }

        private void Chat_Click(object sender, RoutedEventArgs e) => OpenChatWindow();
        private void StartTask_Click(object sender, RoutedEventArgs e) => SetPrompt("Help me start a task: ");
        private void OpenTodayOverview_Click(object sender, RoutedEventArgs e) => SetPrompt("Show me today's schedule, tasks, and reminders.");

        private async void PlayMusic_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is DashboardViewModel dvm)
                await dvm.MusicPlayer.TogglePlayPauseCommand.ExecuteAsync(null);
        }

        private async void TakePhoto_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!App.Settings.WindowsCameraEnabled) { OpenCapabilities_Click(sender, e); return; }
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
                if (!App.Settings.WindowsCameraEnabled) { OpenCapabilities_Click(sender, e); return; }
                if (!App.Camera.IsInitialized) await App.Camera.InitializeAsync();
                if (App.Camera.IsRecording)
                {
                    await App.Camera.StopRecordingAsync();
                    return;
                }
                await App.Camera.StartRecordingAsync();
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Aurora Camera", MessageBoxButton.OK, MessageBoxImage.Warning); }
        }

        private void SmartHome_Click(object sender, RoutedEventArgs e) => OpenSettings(SettingsSection.SmartHome);
        private void AddReminder_Click(object sender, RoutedEventArgs e) => SetPrompt("Create a reminder: ");
        private async void Send_Click(object sender, RoutedEventArgs e) => await SendComposerAsync();

        private void Attach_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog { Title = "Attach a file", Multiselect = false, Filter = "All files (*.*)|*.*" };
            if (dialog.ShowDialog(this) != true) return;
            if (DataContext is DashboardViewModel dvm && dvm.SelectedCompanion != null)
                dvm.SelectedCompanion.AttachFile(dialog.FileName);
        }

        private void OpenComposerMenu_Click(object sender, RoutedEventArgs e)
        {
            if (FindResource("ComposerMenu") is ContextMenu menu)
            {
                menu.PlacementTarget = sender as UIElement;
                menu.IsOpen = true;
            }
        }

        private void ComposerMusic_Click(object sender, RoutedEventArgs e) => PlayMusic_Click(sender, e);
        private void ComposerSettings_Click(object sender, RoutedEventArgs e) => OpenSettings();

        private void ProfileMenu_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button) return;
            var menu = new ContextMenu
            {
                PlacementTarget = button,
                Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom,
                StaysOpen = false
            };
            var settings = new MenuItem { Header = "⚙  Settings" };
            settings.Click += (_, _) => OpenSettings();
            menu.Items.Add(settings);
            menu.Items.Add(new Separator());
            var capabilities = new MenuItem { Header = "⚒  Tools & Capabilities" };
            capabilities.Click += (_, _) => OpenCapabilities_Click(button, e);
            menu.Items.Add(capabilities);
            menu.IsOpen = true;
        }
    }
}
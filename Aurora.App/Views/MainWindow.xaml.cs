using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shell;
using Aurora.App.Models;
using Aurora.App.Services;
using Aurora.App.ViewModels;

namespace Aurora.App.Views
{
    public partial class MainWindow : Window
    {
        private MiniCompanionWindow? _miniCompanion;
        private DashboardLayoutPreset _preset = DashboardLayoutPreset.Default;
        private DashboardLayout _layout = DashboardLayout.ForPreset(DashboardLayoutPreset.Default);

        public MainWindow()
        {
            InitializeComponent();
            AuroraOrbLoader.Apply(this);
            ComposerTextBox.KeyDown += ComposerTextBox_KeyDown;
            Loaded += MainWindow_Loaded;
            LoadSavedLayout();
        }

        private void LoadSavedLayout()
        {
            try
            {
                var s = App.Settings;
                if (s == null) return;
                _preset = DashboardLayout.ParsePreset(s.DashboardLayoutPreset);
                _layout = new DashboardLayout(s.DashboardLeftVisible, s.DashboardRightVisible, s.DashboardLeftWidth, s.DashboardRightWidth).Normalize();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Dashboard] Layout load failed: {ex}");
                _preset = DashboardLayoutPreset.Default;
                _layout = DashboardLayout.ForPreset(_preset);
            }
            ApplyLayout(save: false);
        }

        private void ApplyLayout(bool save)
        {
            _layout = _layout.Normalize();
            LeftSidebarColumn.Width = new GridLength(_layout.LeftVisible ? _layout.LeftWidth : 0);
            RightSidebarColumn.Width = new GridLength(_layout.RightVisible ? _layout.RightWidth : 0);
            LeftSplitter.Visibility = _layout.LeftVisible ? Visibility.Visible : Visibility.Collapsed;
            RightSplitter.Visibility = _layout.RightVisible ? Visibility.Visible : Visibility.Collapsed;
            ToggleLeftSidebarButton.ToolTip = _layout.LeftVisible ? "Hide navigation" : "Show navigation";
            ToggleRightSidebarButton.ToolTip = _layout.RightVisible ? "Hide information panel" : "Show information panel";
            if (save) SaveLayout();
        }

        private void SaveLayout()
        {
            try
            {
                var s = App.Settings;
                if (s == null) return;
                s.DashboardLayoutPreset = _preset.ToString();
                s.DashboardLeftVisible = _layout.LeftVisible;
                s.DashboardRightVisible = _layout.RightVisible;
                s.DashboardLeftWidth = _layout.LeftWidth;
                s.DashboardRightWidth = _layout.RightWidth;
                s.Save();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Dashboard] Layout save failed: {ex}");
            }
        }

        private void SetPreset(DashboardLayoutPreset preset)
        {
            _preset = preset;
            _layout = DashboardLayout.ForPreset(preset);
            ApplyLayout(save: true);
        }

        private void LayoutMenu_Click(object sender, RoutedEventArgs e)
        {
            var menu = new ContextMenu
            {
                PlacementTarget = sender as UIElement,
                Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom,
                StaysOpen = false
            };
            void Add(string header, DashboardLayoutPreset preset)
            {
                var item = new MenuItem { Header = header, IsCheckable = true, IsChecked = _preset == preset };
                item.Click += (_, _) => SetPreset(preset);
                menu.Items.Add(item);
            }
            Add("Default", DashboardLayoutPreset.Default);
            Add("Compact", DashboardLayoutPreset.Compact);
            Add("Chat focused", DashboardLayoutPreset.ChatFocused);
            Add("Companion focused", DashboardLayoutPreset.CompanionFocused);
            menu.Items.Add(new Separator());
            var reset = new MenuItem { Header = "Reset to default" };
            reset.Click += (_, _) => SetPreset(DashboardLayoutPreset.Default);
            menu.Items.Add(reset);
            menu.IsOpen = true;
        }

        private void Splitter_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            _preset = DashboardLayoutPreset.Custom;
            _layout = _layout with
            {
                LeftWidth = _layout.LeftVisible ? LeftSidebarColumn.ActualWidth : _layout.LeftWidth,
                RightWidth = _layout.RightVisible ? RightSidebarColumn.ActualWidth : _layout.RightWidth
            };
            ApplyLayout(save: true);
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= MainWindow_Loaded;
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
            try
            {
#pragma warning disable CA1416
                await dvm.SelectedCompanion.SendCommand.ExecuteAsync(null);
#pragma warning restore CA1416
            }
            finally { ComposerTextBox.Clear(); }
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2) { MaximizeRestore_Click(sender, e); return; }
            try { DragMove(); } catch (InvalidOperationException) { }
        }

        private void ToggleLeftSidebar_Click(object sender, RoutedEventArgs e)
        {
            _preset = DashboardLayoutPreset.Custom;
            _layout = _layout with { LeftVisible = !_layout.LeftVisible };
            ApplyLayout(save: true);
        }

        private void ToggleRightSidebar_Click(object sender, RoutedEventArgs e)
        {
            _preset = DashboardLayoutPreset.Custom;
            _layout = _layout with { RightVisible = !_layout.RightVisible };
            ApplyLayout(save: true);
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

        private void OpenProfiles()
        {
            new ProfileWindow { Owner = this }.ShowDialog();
            ReloadAfterProfileSwitch();
        }

        public void ReloadAfterProfileSwitch()
        {
            if (DataContext is DashboardViewModel dvm)
                dvm.ReloadForActiveProfile();
        }

        private void OpenSettings(SettingsSection section = SettingsSection.Hub)
        {
            var window = new IntegrationsWindow(GetCompanions(), section) { Owner = this };
            window.ShowDialog();
            if (DataContext is DashboardViewModel dvm)
                dvm.RefreshUserName();
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

            var profiles = new MenuItem { Header = "👥  Profiles" };
            profiles.Click += (_, _) => OpenProfiles();
            menu.Items.Add(profiles);

            var changeName = new MenuItem { Header = "✎  Change name" };
            changeName.Click += (_, _) => ChangeUserName();
            menu.Items.Add(changeName);

            var settings = new MenuItem { Header = "⚙  Settings" };
            settings.Click += (_, _) => OpenSettings();
            menu.Items.Add(settings);
            menu.Items.Add(new Separator());
            var capabilities = new MenuItem { Header = "⚒  Tools & Capabilities" };
            capabilities.Click += (_, _) => OpenCapabilities_Click(button, e);
            menu.Items.Add(capabilities);
            menu.IsOpen = true;
        }

        private void ChangeUserName()
        {
            var currentName = DataContext is DashboardViewModel dvm ? dvm.UserName : App.Settings.UserName;
            var input = new TextBox
            {
                Text = currentName,
                Margin = new Thickness(0, 8, 0, 14),
                Padding = new Thickness(8, 6, 8, 6),
                FontSize = 14,
                MinWidth = 260
            };
            input.SelectAll();

            var save = new Button
            {
                Content = "Save name",
                Padding = new Thickness(16, 7, 16, 7),
                HorizontalAlignment = HorizontalAlignment.Right,
                Background = Brushes.Transparent
            };

            var dialog = new Window
            {
                Owner = this,
                Title = "Your name",
                Width = 340,
                SizeToContent = SizeToContent.Height,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ResizeMode = ResizeMode.NoResize,
                Background = Brushes.Transparent,
                Content = new StackPanel { Margin = new Thickness(22) }
            };

            var panel = (StackPanel)dialog.Content;
            panel.Children.Add(new TextBlock { Text = "What should Aurora call you?", FontSize = 16, FontWeight = FontWeights.SemiBold });
            panel.Children.Add(input);
            panel.Children.Add(save);

            save.Click += (_, _) =>
            {
                var name = input.Text.Trim();
                if (string.IsNullOrWhiteSpace(name))
                {
                    MessageBox.Show(dialog, "Please enter a name.", "Your name", MessageBoxButton.OK, MessageBoxImage.Information);
                    input.Focus();
                    return;
                }

                if (name.Length > 40) name = name.Substring(0, 40);
                App.Settings.UserName = name;
                App.Settings.Save();
                if (DataContext is DashboardViewModel dashboard)
                    dashboard.RefreshUserName();
                dialog.DialogResult = true;
                dialog.Close();
            };

            input.KeyDown += (_, args) =>
            {
                if (args.Key == Key.Enter) save.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                else if (args.Key == Key.Escape) dialog.Close();
            };

            dialog.ShowDialog();
        }

    }
}

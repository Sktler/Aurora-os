using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
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
                Dispatcher.BeginInvoke(new Action(AddCapabilityTiles), System.Windows.Threading.DispatcherPriority.Loaded);
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

        private void AddCapabilityTiles()
        {
            if (Content is not Grid root) return;
            var hub = FindDescendant<WrapPanel>(root, wp => FindDescendant<TextBlock>(wp, t => t.Text == "🧠 AI engine") != null);
            if (hub == null || FindDescendant<TextBlock>(hub, t => t.Text == "📷 Camera & vision") != null) return;

            hub.Children.Add(CreateCapabilityTile("📷 Camera & vision", "Windows camera permissions, devices, capture", "camera"));
            hub.Children.Add(CreateCapabilityTile("🧩 MCP & tools", "Connect MCP servers and discover model tools", "mcp"));
            hub.Children.Add(CreateCapabilityTile("🎨 Image generation", "Google/Gemini and OpenAI image providers", "image"));
            hub.Children.Add(CreateCapabilityTile("🔐 Permissions", "Review hardware and tool access", "permissions"));
        }

        private Button CreateCapabilityTile(string title, string description, string kind)
        {
            var button = new Button
            {
                Width = 300, Height = 92, Margin = new Thickness(0, 0, 14, 14), Cursor = System.Windows.Input.Cursors.Hand,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Background = (Brush)FindResource("BgPanelBrush"), BorderThickness = new Thickness(0)
            };
            var panel = new StackPanel();
            panel.Children.Add(new TextBlock { Text = title, FontWeight = FontWeights.SemiBold, FontSize = 14 });
            panel.Children.Add(new TextBlock { Text = description, Foreground = (Brush)FindResource("TextMutedBrush"), Margin = new Thickness(0,4,0,0), TextWrapping = TextWrapping.Wrap });
            button.Content = panel;
            button.Click += (_, _) => new CapabilitiesWindow { Owner = this }.ShowDialog();
            return button;
        }

        private static T? FindDescendant<T>(DependencyObject root, Func<T, bool> predicate) where T : DependencyObject
        {
            if (root is T match && predicate(match)) return match;
            var count = VisualTreeHelper.GetChildrenCount(root);
            for (var i = 0; i < count; i++) { var result = FindDescendant<T>(VisualTreeHelper.GetChild(root, i), predicate); if (result != null) return result; }
            return null;
        }
    }
}

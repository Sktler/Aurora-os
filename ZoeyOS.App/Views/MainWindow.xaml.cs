using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Shell;

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
            try { DragMove(); } catch (System.InvalidOperationException) { }
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

        private void Window_StateChanged(object sender, System.EventArgs e)
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

        private void MiniCompanion_RestoreRequested(object? sender, System.EventArgs e)
        {
            _miniCompanion?.Close(); _miniCompanion = null;
            Show(); WindowState = WindowState.Normal; Activate(); Focus();
        }

        private void MiniCompanion_ExitRequested(object? sender, System.EventArgs e)
        {
            _miniCompanion?.Close(); _miniCompanion = null; ExitApplication();
        }

        private void ExitApplication() { _allowClose = true; _miniCompanion?.Close(); _miniCompanion = null; Close(); }

        private void OpenIntegrations_Click(object sender, RoutedEventArgs e)
        {
            var companions = DataContext is ViewModels.DashboardViewModel dvm ? dvm.Companions.Select(c => c.Companion) : Enumerable.Empty<Models.Companion>();
            new IntegrationsWindow(companions) { Owner = this }.ShowDialog();
        }

        private void Attach_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog { Title = "Attach a file", Multiselect = false, Filter = "All files (*.*)|*.*" };
            if (dialog.ShowDialog(this) != true) return;
            if (DataContext is ViewModels.DashboardViewModel dvm && dvm.SelectedCompanion != null) dvm.SelectedCompanion.AttachFile(dialog.FileName);
        }
    }
}

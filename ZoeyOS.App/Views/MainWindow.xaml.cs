using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Shell;

namespace ZoeyOS.App.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        // --- Custom title bar (replaces the native one removed via WindowStyle="None") ---

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                MaximizeRestore_Click(sender, e);
                return;
            }

            // DragMove() throws if called outside a button-down gesture (e.g. programmatically) -
            // it's safe here since this handler only ever runs from an actual mouse-down.
            try { DragMove(); } catch (System.InvalidOperationException) { /* button already released */ }
        }

        private void Minimize_Click(object sender, RoutedEventArgs e) => SystemCommands.MinimizeWindow(this);

        private void MaximizeRestore_Click(object sender, RoutedEventArgs e)
        {
            if (WindowState == WindowState.Maximized)
                SystemCommands.RestoreWindow(this);
            else
                SystemCommands.MaximizeWindow(this);
        }

        private void Close_Click(object sender, RoutedEventArgs e) => SystemCommands.CloseWindow(this);

        private void Window_StateChanged(object sender, System.EventArgs e)
        {
            var maximized = WindowState == WindowState.Maximized;
            MaximizeRestoreButton.Content = maximized ? "🗗" : "🗖";
            MaximizeRestoreButton.ToolTip = maximized ? "Restore" : "Maximize";
        }

        private void OpenIntegrations_Click(object sender, RoutedEventArgs e)
        {
            var companions = DataContext is ViewModels.DashboardViewModel dvm
                ? dvm.Companions.Select(c => c.Companion)
                : Enumerable.Empty<Models.Companion>();

            var window = new IntegrationsWindow(companions) { Owner = this };
            window.ShowDialog();
        }

        private void Attach_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Attach a file",
                Multiselect = false,
                Filter = "Text-readable files (*.txt;*.md;*.csv;*.json;*.log;*.xml;*.yml;*.cs;*.py;*.js;*.ts;*.html;*.css;*.sql)|" +
                         "*.txt;*.md;*.markdown;*.csv;*.tsv;*.json;*.log;*.xml;*.yml;*.yaml;*.ini;*.cfg;*.conf;*.cs;*.py;*.js;*.ts;*.html;*.htm;*.css;*.sql;*.sh;*.bat;*.ps1|" +
                         "All files (*.*)|*.*"
            };

            if (dialog.ShowDialog(this) != true) return;
            if (DataContext is ViewModels.DashboardViewModel dvm && dvm.SelectedCompanion != null)
                dvm.SelectedCompanion.AttachFile(dialog.FileName);
        }
    }
}

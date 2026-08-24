using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Shell;

namespace ZoeyOS.App.Views
{
    public partial class MainWindow : Window
    {
        private bool _allowClose;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                MaximizeRestore_Click(sender, e);
                return;
            }

            try { DragMove(); } catch (System.InvalidOperationException) { }
        }

        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            // Collapse Aurora into its own small floating companion, not the Windows notification area.
            WindowState = WindowState.Minimized;
            Hide();
            ShowMiniCompanion();
        }

        private void MaximizeRestore_Click(object sender, RoutedEventArgs e)
        {
            if (WindowState == WindowState.Maximized)
                SystemCommands.RestoreWindow(this);
            else
                SystemCommands.MaximizeWindow(this);
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
            // Keep the minimized companion as a separate WPF window so it remains visible on the desktop.
            var mini = new MiniCompanionWindow { Owner = this };
            mini.RestoreRequested += (_, _) =>
            {
                mini.Close();
                Show();
                WindowState = WindowState.Normal;
                Activate();
                Focus();
            };
            mini.ExitRequested += (_, _) =>
            {
                mini.Close();
                ExitApplication();
            };
            mini.Show();
        }

        private void ExitApplication()
        {
            _allowClose = true;
            Close();
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

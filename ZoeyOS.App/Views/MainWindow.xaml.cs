using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Shell;
using Forms = System.Windows.Forms;

namespace ZoeyOS.App.Views
{
    public partial class MainWindow : Window
    {
        private readonly Forms.NotifyIcon _trayIcon;
        private readonly Forms.ContextMenuStrip _trayMenu;
        private bool _allowClose;

        public MainWindow()
        {
            InitializeComponent();

            _trayMenu = new Forms.ContextMenuStrip();
            _trayMenu.Items.Add("Show Aurora", null, (_, _) => RestoreFromTray());
            _trayMenu.Items.Add("Exit Aurora", null, (_, _) => ExitFromTray());

            _trayIcon = new Forms.NotifyIcon
            {
                Icon = CreateAuroraPersonIcon(),
                Text = "Aurora",
                Visible = true,
                ContextMenuStrip = _trayMenu
            };
            _trayIcon.DoubleClick += (_, _) => RestoreFromTray();

            Closed += (_, _) => DisposeTrayIcon();
        }

        // --- Custom title bar (replaces the native one removed via WindowStyle="None") ---

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
            // Minimize into the notification area instead of leaving an empty taskbar button.
            Hide();
            _trayIcon.ShowBalloonTip(1200, "Aurora", "Aurora is still running. Double-click the person icon to restore it.", Forms.ToolTipIcon.Info);
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

        private void RestoreFromTray()
        {
            Show();
            if (WindowState == WindowState.Minimized)
                WindowState = WindowState.Normal;
            Activate();
            Topmost = true;
            Topmost = false;
            Focus();
        }

        private void ExitFromTray()
        {
            _allowClose = true;
            Close();
        }

        private void ExitApplication()
        {
            _allowClose = true;
            Close();
        }

        private void DisposeTrayIcon()
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            _trayMenu.Dispose();
        }

        /// <summary>
        /// Creates a small, high-contrast Aurora person/avatar for the notification area.
        /// It is generated at runtime so the tray icon stays independent of the app's globe artwork.
        /// </summary>
        private static Icon CreateAuroraPersonIcon()
        {
            using var bitmap = new Bitmap(32, 32, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (var graphics = Graphics.FromImage(bitmap))
            using (var brush = new SolidBrush(Color.FromArgb(79, 216, 232)))
            using (var outline = new Pen(Color.White, 1.5f))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.Clear(Color.Transparent);
                graphics.FillEllipse(brush, 10, 4, 12, 12);
                graphics.FillEllipse(brush, 5, 16, 22, 13);
                graphics.DrawEllipse(outline, 10, 4, 12, 12);
                graphics.DrawArc(outline, 5, 16, 22, 13, 180, 180);
            }

            var handle = bitmap.GetHicon();
            return Icon.FromHandle(handle);
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

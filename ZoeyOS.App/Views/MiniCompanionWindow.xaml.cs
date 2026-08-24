using System;
using System.Windows;
using System.Windows.Input;

namespace ZoeyOS.App.Views
{
    public partial class MiniCompanionWindow : Window
    {
        public event EventHandler? RestoreRequested;
        public event EventHandler? ExitRequested;

        public MiniCompanionWindow()
        {
            InitializeComponent();
            Left = SystemParameters.WorkArea.Right - Width - 24;
            Top = SystemParameters.WorkArea.Bottom - Height - 24;
        }

        private void Avatar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
                RestoreRequested?.Invoke(this, EventArgs.Empty);
            else
                try { DragMove(); } catch (InvalidOperationException) { }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
            => RestoreRequested?.Invoke(this, EventArgs.Empty);
    }
}

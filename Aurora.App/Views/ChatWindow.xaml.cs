using System.Windows;
using Aurora.App.ViewModels;

namespace Aurora.App.Views
{
    public partial class ChatWindow : Window
    {
        public ChatWindow(CompanionViewModel companion)
        {
            InitializeComponent();
            DialogLayout.ConstrainToWorkArea(this);
            DataContext = new DashboardViewModelProxy(companion);
            Activated += OnWindowActivated;
        }

        private void OnWindowActivated(object? sender, System.EventArgs e)
        {
            if (DataContext is DashboardViewModelProxy proxy) proxy.SelectedCompanion.RefreshFoldersCommand.Execute(null);
        }

        private sealed class DashboardViewModelProxy
        {
            public CompanionViewModel SelectedCompanion { get; }
            public DashboardViewModelProxy(CompanionViewModel companion) => SelectedCompanion = companion;
        }
    }
}

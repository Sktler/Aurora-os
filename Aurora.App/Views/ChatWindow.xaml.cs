using System.Windows;
using Aurora.App.ViewModels;

namespace Aurora.App.Views
{
    public partial class ChatWindow : Window
    {
        public ChatWindow(CompanionViewModel companion)
        {
            InitializeComponent();
            DataContext = new DashboardViewModelProxy(companion);
        }

        private sealed class DashboardViewModelProxy
        {
            public CompanionViewModel SelectedCompanion { get; }
            public DashboardViewModelProxy(CompanionViewModel companion) => SelectedCompanion = companion;
        }
    }
}

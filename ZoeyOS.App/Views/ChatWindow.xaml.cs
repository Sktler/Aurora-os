using System.Windows;
using ZoeyOS.App.ViewModels;

namespace ZoeyOS.App.Views
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

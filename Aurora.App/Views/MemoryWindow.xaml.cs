using System.Windows;
using Aurora.App.ViewModels;

namespace Aurora.App.Views
{
    public partial class MemoryWindow : Window
    {
        public MemoryWindow()
        {
            InitializeComponent();
            DialogLayout.ConstrainToWorkArea(this);
            DataContext = new DashboardViewModel();
            BrowserPanel.DataContext = new ConversationBrowserViewModel();
        }
    }
}

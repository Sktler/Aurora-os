using System.Windows;
using ZoeyOS.App.ViewModels;

namespace ZoeyOS.App.Views
{
    public partial class MemoryWindow : Window
    {
        public MemoryWindow()
        {
            InitializeComponent();
            DataContext = new DashboardViewModel();
        }
    }
}

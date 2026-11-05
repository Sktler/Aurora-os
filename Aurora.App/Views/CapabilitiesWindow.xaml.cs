using System.Windows;
namespace Aurora.App.Views;
public partial class CapabilitiesWindow : Window
{
    public CapabilitiesWindow()
    {
        InitializeComponent();
        DialogLayout.ConstrainToWorkArea(this);
    }
}

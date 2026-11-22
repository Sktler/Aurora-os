using System.Windows;
namespace Aurora.App.Views;
public partial class CapabilitiesWindow : Window
{
    public CapabilitiesWindow() : this(false) { }

    public CapabilitiesWindow(bool showPhone)
    {
        InitializeComponent();
        DialogLayout.ConstrainToWorkArea(this);
        if (showPhone)
            Loaded += (_, _) => ((CapabilitiesView)Content).ShowPhonePanel();
    }
}

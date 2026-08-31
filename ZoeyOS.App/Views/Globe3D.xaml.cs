using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ZoeyOS.App.Views
{
    /// <summary>
    /// Displays the shared Aurora orb artwork. The orb is intentionally an image asset so
    /// the main orb and minimized companion use the exact same colors, shading, shape and
    /// orbital rings as the supplied reference artwork.
    /// </summary>
    public partial class Globe3D : UserControl
    {
        // Kept as a compatibility property because existing dashboard XAML can provide
        // an AccentColor binding. The reference artwork itself is never recolored.
        public static readonly DependencyProperty AccentColorProperty =
            DependencyProperty.Register(nameof(AccentColor), typeof(Brush), typeof(Globe3D), new PropertyMetadata(null));

        public Brush? AccentColor
        {
            get => (Brush?)GetValue(AccentColorProperty);
            set => SetValue(AccentColorProperty, value);
        }

        public Globe3D()
        {
            InitializeComponent();
        }
    }
}
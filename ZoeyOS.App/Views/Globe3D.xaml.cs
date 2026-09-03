using System.Windows;
using System.Windows.Media;
using ZoeyOS.App.Services;

namespace ZoeyOS.App.Views
{
    /// <summary>Displays the shared blue flame Aurora orb artwork.</summary>
    public partial class Globe3D : System.Windows.Controls.UserControl
    {
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
            AuroraOrbLoader.Apply(this);
        }
    }
}
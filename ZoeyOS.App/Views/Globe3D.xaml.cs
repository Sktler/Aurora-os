using System.Windows;
using System.Windows.Controls;

namespace ZoeyOS.App.Views
{
    /// <summary>
    /// Displays the shared Aurora orb artwork. The orb is intentionally an image asset so
    /// the main orb and minimized companion use the exact same colors, shading, shape and
    /// orbital rings as the supplied reference artwork.
    /// </summary>
    public partial class Globe3D : UserControl
    {
        public Globe3D()
        {
            InitializeComponent();
        }
    }
}
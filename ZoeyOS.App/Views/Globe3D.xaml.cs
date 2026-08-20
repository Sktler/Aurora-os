using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Media3D;

namespace ZoeyOS.App.Views
{
    /// <summary>
    /// A small, real 3D globe: a procedurally generated sphere mesh, lit by a directional
    /// light so it actually shades light-to-dark across its surface, with faint latitude/
    /// longitude lines baked into its texture (so it reads as a "globe" rather than a plain
    /// ball) and a slow continuous spin. Used in place of the flat colored circles that used
    /// to represent each companion, per the Zoe OS-style dashboard look.
    ///
    /// Deliberately kept to one control (not scattered inline XAML) so every companion orb
    /// in the sidebar shares one mesh-building code path - fixing a rendering issue here
    /// fixes it everywhere at once.
    /// </summary>
    public partial class Globe3D : UserControl
    {
        public static readonly DependencyProperty AccentColorProperty =
            DependencyProperty.Register(nameof(AccentColor), typeof(Color), typeof(Globe3D),
                new PropertyMetadata(Color.FromRgb(0x4F, 0xD8, 0xE8), OnAccentColorChanged));

        public Color AccentColor
        {
            get => (Color)GetValue(AccentColorProperty);
            set => SetValue(AccentColorProperty, value);
        }

        // Mesh resolution - enough segments to look smooth at typical 32-48px display size
        // without costing much: an 8px avatar and a 200px hero globe use the same mesh here,
        // since WPF's 3D pipeline scales cheaply and re-tessellating per size isn't worth it.
        private const int Stacks = 18;
        private const int Slices = 28;

        public Globe3D()
        {
            InitializeComponent();
            Loaded += (_, _) => BuildGlobe();
        }

        private static void OnAccentColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is Globe3D globe && globe.IsLoaded)
                globe.BuildGlobe();
        }

        private void BuildGlobe()
        {
            var mesh = BuildSphereMesh(Stacks, Slices);
            var material = BuildGlobeMaterial(AccentColor);

            var model = new GeometryModel3D(mesh, material)
            {
                BackMaterial = material // visible from the inside too - avoids a "hollow" look
                                         // at any point mid-spin if the camera angle drifts
            };

            var rotation = new AxisAngleRotation3D(new Vector3D(0.15, 1, 0), 0);
            var transform = new RotateTransform3D(rotation);
            model.Transform = transform;

            SphereVisual.Content = model;

            var spin = new DoubleAnimation
            {
                From = 0,
                To = 360,
                Duration = TimeSpan.FromSeconds(14), // slow, ambient spin - not distracting
                RepeatBehavior = RepeatBehavior.Forever
            };
            rotation.BeginAnimation(AxisAngleRotation3D.AngleProperty, spin);
        }

        /// <summary>Builds a UV sphere: rings of latitude from pole to pole, each split into
        /// longitude slices, triangulated into two triangles per quad.</summary>
        private static MeshGeometry3D BuildSphereMesh(int stacks, int slices)
        {
            var mesh = new MeshGeometry3D();

            for (int i = 0; i <= stacks; i++)
            {
                double phi = Math.PI * i / stacks; // 0 (north pole) to PI (south pole)
                double y = Math.Cos(phi);
                double ringRadius = Math.Sin(phi);

                for (int j = 0; j <= slices; j++)
                {
                    double theta = 2 * Math.PI * j / slices;
                    double x = ringRadius * Math.Cos(theta);
                    double z = ringRadius * Math.Sin(theta);

                    mesh.Positions.Add(new Point3D(x, y, z));
                    mesh.Normals.Add(new Vector3D(x, y, z)); // sphere centered at origin: normal == position
                    mesh.TextureCoordinates.Add(new Point((double)j / slices, (double)i / stacks));
                }
            }

            int rowLength = slices + 1;
            for (int i = 0; i < stacks; i++)
            {
                for (int j = 0; j < slices; j++)
                {
                    int topLeft = i * rowLength + j;
                    int topRight = topLeft + 1;
                    int bottomLeft = (i + 1) * rowLength + j;
                    int bottomRight = bottomLeft + 1;

                    mesh.TriangleIndices.Add(topLeft);
                    mesh.TriangleIndices.Add(bottomLeft);
                    mesh.TriangleIndices.Add(topRight);

                    mesh.TriangleIndices.Add(topRight);
                    mesh.TriangleIndices.Add(bottomLeft);
                    mesh.TriangleIndices.Add(bottomRight);
                }
            }

            return mesh;
        }

        /// <summary>Builds the sphere's material: the companion's accent color as a base,
        /// baked latitude/longitude grid lines drawn on top so it reads as a globe rather
        /// than a plain ball, plus a soft specular highlight for a glossy, lit look.</summary>
        private static Material BuildGlobeMaterial(Color accent)
        {
            var diffuse = new DiffuseMaterial(new SolidColorBrush(accent) { Opacity = 1.0 });

            var group = new MaterialGroup();
            group.Children.Add(diffuse);
            group.Children.Add(new DiffuseMaterial(BuildGridLinesBrush()));
            group.Children.Add(new SpecularMaterial(new SolidColorBrush(Color.FromArgb(140, 255, 255, 255)), 42));

            return group;
        }

        /// <summary>Draws faint latitude/longitude lines onto a small DrawingBrush, tiled
        /// across the sphere's UV space - this is what makes it look like a globe (map
        /// grid) instead of a plain tinted sphere.</summary>
        private static Brush BuildGridLinesBrush()
        {
            const int lonLines = 8;
            const int latLines = 5;

            var drawing = new DrawingGroup();
            using (var ctx = drawing.Open())
            {
                var pen = new Pen(new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)), 0.006);

                for (int i = 1; i < lonLines; i++)
                {
                    double x = (double)i / lonLines;
                    ctx.DrawLine(pen, new Point(x, 0), new Point(x, 1));
                }
                for (int i = 1; i < latLines; i++)
                {
                    double y = (double)i / latLines;
                    ctx.DrawLine(pen, new Point(0, y), new Point(1, y));
                }
            }

            var brush = new DrawingBrush(drawing)
            {
                Viewport = new Rect(0, 0, 1, 1),
                Stretch = Stretch.Fill,
                TileMode = TileMode.None
            };
            brush.Freeze();
            return brush;
        }
    }
}

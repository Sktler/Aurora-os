using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace ZoeyOS.App.Views
{
    /// <summary>
    /// The big "voice mode" visual - a rippling point-cloud sphere matching the blue-to-
    /// purple particle globe look the user referenced. WPF's Viewport3D has no point-sprite
    /// primitive, so true per-particle sphere rendering there would mean hundreds of separate
    /// 3D objects (too costly for real-time animation). Instead this does the 3D math by hand
    /// - generate points on a sphere, rotate and wave-perturb them in 3D, project to 2D with
    /// real perspective - and draws each one as a small Ellipse on a Canvas. Real 3D geometry
    /// underneath, just rendered through 2D primitives instead of Viewport3D.
    ///
    /// Only animates while actually visible (hooked to IsVisibleChanged) so toggling to text
    /// chat view stops the per-frame work entirely rather than animating off-screen.
    /// </summary>
    public partial class ParticleOrb : UserControl
    {
        private const int ParticleCount = 700;
        private const double SphereRadius = 1.0; // unit sphere; scaled to control size at render time
        private const double CameraDistance = 2.6;
        private const double RotationSpeed = 0.18; // radians/sec
        private const double WaveSpeed = 1.1;
        private const double WaveAmplitude = 0.07;

        // Base (un-rotated, un-waved) unit-sphere positions - computed once, reused every frame.
        private readonly Point3 [] _basePoints = new Point3[ParticleCount];
        private readonly Ellipse[] _dots = new Ellipse[ParticleCount];
        private readonly SolidColorBrush[] _brushes = new SolidColorBrush[ParticleCount];

        // Fixed per-particle hue offset, assigned once at build time - makes neighboring
        // particles read as visibly different colors instead of one smooth gradient band.
        private readonly double[] _hueJitter = new double[ParticleCount];

        private static readonly Color TopColor = Color.FromRgb(0x4F, 0xC3, 0xF7);    // cyan-blue - initial brush color before the first frame renders
        private static readonly Color CrestColor = Color.FromRgb(0xE8, 0xF6, 0xFF);  // near-white wave highlight

        private DateTime _startTime;
        private bool _animating;

        private readonly struct Point3
        {
            public readonly double X, Y, Z, Lat, Lon;
            public Point3(double x, double y, double z, double lat, double lon) { X = x; Y = y; Z = z; Lat = lat; Lon = lon; }
        }

        public ParticleOrb()
        {
            InitializeComponent();
            BuildParticles();
            Loaded += (_, _) => { _startTime = DateTime.Now; };
            IsVisibleChanged += (_, e) =>
            {
                if ((bool)e.NewValue) StartAnimating();
                else StopAnimating();
            };
        }

        /// <summary>Distributes points evenly across a sphere using a Fibonacci lattice
        /// (golden-angle spiral) - far more uniform than random placement, and cheap to
        /// compute once up front since the base layout never changes, only its rotation
        /// and wave perturbation do.</summary>
        private void BuildParticles()
        {
            Surface.Children.Clear();
            const double goldenAngle = Math.PI * (3.0 - 2.23606797749979); // pi * (3 - sqrt(5))
            var rng = new Random(1337); // fixed seed - same jitter pattern every run, not a new random look each launch

            for (int i = 0; i < ParticleCount; i++)
            {
                double y = 1 - (2.0 * i) / (ParticleCount - 1); // 1 -> -1
                double ringRadius = Math.Sqrt(Math.Max(0, 1 - y * y));
                double theta = goldenAngle * i;

                double x = Math.Cos(theta) * ringRadius;
                double z = Math.Sin(theta) * ringRadius;
                double lat = Math.Acos(y);
                double lon = Math.Atan2(z, x);

                _basePoints[i] = new Point3(x, y, z, lat, lon);
                _hueJitter[i] = (rng.NextDouble() - 0.5) * 70; // +/-35 degrees per particle

                var brush = new SolidColorBrush(TopColor);
                _brushes[i] = brush;

                var dot = new Ellipse { Width = 3, Height = 3, Fill = brush };
                _dots[i] = dot;
                Surface.Children.Add(dot);
            }
        }

        private void StartAnimating()
        {
            if (_animating) return;
            _animating = true;
            _startTime = DateTime.Now;
            CompositionTarget.Rendering += OnRendering;
        }

        private void StopAnimating()
        {
            if (!_animating) return;
            _animating = false;
            CompositionTarget.Rendering -= OnRendering;
        }

        private void OnRendering(object? sender, EventArgs e)
        {
            double w = ActualWidth, h = ActualHeight;
            if (w <= 0 || h <= 0) return;

            double t = (DateTime.Now - _startTime).TotalSeconds;
            double rotY = t * RotationSpeed;
            double cosR = Math.Cos(rotY), sinR = Math.Sin(rotY);

            double cx = w / 2, cy = h / 2;
            double pixelRadius = Math.Min(w, h) / 2 * 0.92;

            for (int i = 0; i < ParticleCount; i++)
            {
                var p = _basePoints[i];

                // Traveling wave across the surface (diagonal band of ripple/brightness,
                // matching the rippled look in the reference image) - perturbs each point's
                // distance from center as a function of its position on the sphere and time.
                double wave = Math.Sin(p.Lat * 3.0 + p.Lon * 2.0 - t * WaveSpeed);
                double r = SphereRadius + wave * WaveAmplitude;

                double x = p.X * r, y = p.Y * r, z = p.Z * r;

                // Rotate around Y axis for the continuous spin.
                double rx = x * cosR + z * sinR;
                double rz = -x * sinR + z * cosR;

                // Perspective projection: points closer to the camera (larger rz) project
                // bigger and land further from center; this is what makes it read as an
                // actual sphere rather than a flat dot pattern.
                double scale = CameraDistance / (CameraDistance - rz);
                double screenX = cx + rx * pixelRadius * scale;
                double screenY = cy - y * pixelRadius * scale;

                var dot = _dots[i];
                Canvas.SetLeft(dot, screenX - dot.Width / 2);
                Canvas.SetTop(dot, screenY - dot.Height / 2);

                double depthNorm = Math.Clamp((rz + SphereRadius) / (2 * SphereRadius), 0, 1); // 0 = far side, 1 = near side
                double size = 1.6 + depthNorm * 2.6;
                dot.Width = size;
                dot.Height = size;
                dot.Opacity = 0.25 + depthNorm * 0.75;

                double crestAmount = Math.Clamp((wave + 1) / 2, 0, 1); // 0..1, peaks at the wave crest
                double verticalBlend = (y + 1) / 2; // 0 (bottom) .. 1 (top)

                // Hue sweeps across the visible range by longitude - cyan-blue around one
                // side, through violet, into magenta/pink on the other - so spinning the
                // sphere reveals a full band of color instead of just the two fixed tones
                // it had before. Vertical position nudges it slightly warmer toward the
                // bottom, matching the reference image's blue-top/purple-bottom lean.
                double lonNorm = (p.Lon + Math.PI) / (2 * Math.PI); // 0..1
                double hue = 185 + lonNorm * 260 + (1 - verticalBlend) * 20 + _hueJitter[i];
                hue = ((hue % 360) + 360) % 360;

                double saturation = 0.65 + crestAmount * 0.25;
                double brightness = 0.55 + depthNorm * 0.35 + crestAmount * 0.15;
                var baseColor = HsvToRgb(hue, Math.Clamp(saturation, 0, 1), Math.Clamp(brightness, 0, 1));
                var finalColor = LerpColor(baseColor, CrestColor, crestAmount * 0.35);
                _brushes[i].Color = finalColor;
            }
        }

        private static Color LerpColor(Color a, Color b, double t)
        {
            t = Math.Clamp(t, 0, 1);
            return Color.FromRgb(
                (byte)(a.R + (b.R - a.R) * t),
                (byte)(a.G + (b.G - a.G) * t),
                (byte)(a.B + (b.B - a.B) * t));
        }

        /// <summary>Standard HSV->RGB conversion. hue in degrees [0,360), saturation/value in [0,1].</summary>
        private static Color HsvToRgb(double hue, double saturation, double value)
        {
            double c = value * saturation;
            double h = hue / 60.0;
            double x = c * (1 - Math.Abs(h % 2 - 1));
            double m = value - c;

            (double r, double g, double b) = h switch
            {
                < 1 => (c, x, 0.0),
                < 2 => (x, c, 0.0),
                < 3 => (0.0, c, x),
                < 4 => (0.0, x, c),
                < 5 => (x, 0.0, c),
                _ => (c, 0.0, x)
            };

            return Color.FromRgb(
                (byte)Math.Round((r + m) * 255),
                (byte)Math.Round((g + m) * 255),
                (byte)Math.Round((b + m) * 255));
        }
    }
}

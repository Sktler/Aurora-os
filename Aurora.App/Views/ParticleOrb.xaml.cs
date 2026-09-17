using System;
using System.Windows.Controls;
using System.Windows.Media;

namespace Aurora.App.Views
{
    /// <summary>
    /// Displays the supplied Aurora artwork with a lightweight, frame-rate-independent
    /// gentle nodding and two-axis movement. The original facial features move with the artwork.
    /// </summary>
    public partial class ParticleOrb : UserControl
    {
        private const double DriftSpeed = 1.4;
        private const double DriftAmount = 3.0;
        private const double NodAmount = 2.5;

        private DateTime _startTime;
        private bool _animating;

        public ParticleOrb()
        {
            InitializeComponent();
            Loaded += (_, _) => StartAnimating();
            Unloaded += (_, _) => StopAnimating();
            IsVisibleChanged += (_, e) =>
            {
                if ((bool)e.NewValue) StartAnimating();
                else StopAnimating();
            };
        }

        private void StartAnimating()
        {
            if (_animating) return;
            _animating = true;
            _startTime = DateTime.UtcNow;
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
            double t = (DateTime.UtcNow - _startTime).TotalSeconds;
            double motion = t * DriftSpeed * Math.PI;

            OrbNod.Angle = Math.Sin(motion * 0.65) * NodAmount;
            OrbTranslation.X = Math.Sin(motion) * DriftAmount;
            OrbTranslation.Y = Math.Cos(motion * 0.8) * DriftAmount;

        }
    }
}

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace Aurora.App.Views
{
    /// <summary>
    /// Displays the supplied Aurora artwork with a lightweight, frame-rate-independent
    /// breathing and rotation animation. Only animates while visible.
    /// </summary>
    public partial class ParticleOrb : UserControl
    {
        private const double RotationSpeed = 7.5;
        private const double BreathSpeed = 1.4;
        private const double BreathAmount = 0.035;
        private const double DriftAmount = 3.0;

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
            double breath = Math.Sin(t * BreathSpeed * Math.PI * 2);

            OrbScale.ScaleX = 1 + breath * BreathAmount;
            OrbScale.ScaleY = 1 + breath * BreathAmount;
            OrbRotation.Angle = t * RotationSpeed;
            OrbTranslation.X = Math.Sin(t * BreathSpeed * Math.PI) * DriftAmount;
            OrbTranslation.Y = Math.Cos(t * BreathSpeed * Math.PI * 0.8) * DriftAmount;
            OrbImage.Opacity = 0.91 + (breath + 1) * 0.045;
        }
    }
}

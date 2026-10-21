using System;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows;

namespace Aurora.App.Views
{
    /// <summary>
    /// Displays the supplied Aurora artwork with a lightweight, frame-rate-independent
    /// gentle nodding, two-axis movement, and expressive eye and mouth overlays.
    /// </summary>
    public partial class ParticleOrb : UserControl
    {
        private const double DriftSpeed = 1.4;
        private const double DriftAmount = 3.0;
        private const double NodAmount = 2.5;

        private DateTime _startTime;
        private bool _animating;

        public static readonly DependencyProperty IsRespondingProperty =
            DependencyProperty.Register(
                nameof(IsResponding),
                typeof(bool),
                typeof(ParticleOrb),
                new PropertyMetadata(false));

        public bool IsResponding
        {
            get => (bool)GetValue(IsRespondingProperty);
            set => SetValue(IsRespondingProperty, value);
        }

        public static readonly DependencyProperty EmotionProperty =
            DependencyProperty.Register(
                nameof(Emotion),
                typeof(Aurora.App.Services.EmotionCue),
                typeof(ParticleOrb),
                new PropertyMetadata(Aurora.App.Services.EmotionCue.Neutral, (d, _) => ((ParticleOrb)d).ApplyEmotion()));

        public Aurora.App.Services.EmotionCue Emotion
        {
            get => (Aurora.App.Services.EmotionCue)GetValue(EmotionProperty);
            set => SetValue(EmotionProperty, value);
        }

        // Only the glow hue shifts, with no flashing, so the cue stays subtle and accessible.
        private void ApplyEmotion()
        {
            try
            {
                var mode = Aurora.App.App.Settings?.EmotionCueMode;
                var cue = Aurora.App.Services.EmotionCues.Intensity(mode) > 0
                    ? Emotion
                    : Aurora.App.Services.EmotionCue.Neutral;
                OrbGlow.Color = (Color)ColorConverter.ConvertFromString(Aurora.App.Services.EmotionCues.GlowColor(cue));
                OrbGlow.Opacity = cue == Aurora.App.Services.EmotionCue.Neutral
                    ? 0.48
                    : 0.48 + 0.2 * Aurora.App.Services.EmotionCues.Intensity(mode);
            }
            catch
            {
                // Cosmetic only; never let a cue failure affect the orb.
            }
        }

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
            double blinkProgress = t % 3.5;
            double eyeScale = blinkProgress < 0.22
                ? 0.12 + 0.88 * Math.Abs(blinkProgress - 0.11) / 0.11
                : 1;
            LeftEyeScale.ScaleY = eyeScale;
            RightEyeScale.ScaleY = eyeScale;
            MouthScale.ScaleY = IsResponding
                ? 0.45 + (Math.Sin(motion * 2.4) + 1) * 0.45
                : 0.45;
        }
    }
}

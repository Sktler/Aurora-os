using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Aurora.App.Services;

namespace Aurora.App.Views
{
    /// <summary>Displays the shared Aurora orb artwork and applies a live companion color tint.</summary>
    public partial class Globe3D : System.Windows.Controls.UserControl
    {
        public static readonly DependencyProperty AccentColorProperty =
            DependencyProperty.Register(nameof(AccentColor), typeof(string), typeof(Globe3D),
                new PropertyMetadata("#4FD8E8", OnAccentColorChanged));

        public string AccentColor
        {
            get => (string)GetValue(AccentColorProperty);
            set => SetValue(AccentColorProperty, value);
        }

        public Globe3D()
        {
            InitializeComponent();
            AuroraOrbLoader.Apply(this);
            Loaded += (_, _) => ApplyTint();
        }

        private static void OnAccentColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is Globe3D globe) globe.ApplyTint();
        }

        private void ApplyTint()
        {
            if (AuroraOrbImage.Source is not BitmapSource source) return;

            var color = ParseColor(AccentColor);
            var converted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
            var width = converted.PixelWidth;
            var height = converted.PixelHeight;
            var stride = width * 4;
            var pixels = new byte[stride * height];
            converted.CopyPixels(pixels, stride, 0);

            for (var i = 0; i < pixels.Length; i += 4)
            {
                var blue = pixels[i];
                var green = pixels[i + 1];
                var red = pixels[i + 2];
                var alpha = pixels[i + 3];
                if (alpha == 0) continue;

                var luminance = (red + green + blue) / (255f * 3f);
                luminance = Math.Clamp(0.25f + luminance * 0.9f, 0f, 1f);
                pixels[i] = (byte)(color.B * luminance);
                pixels[i + 1] = (byte)(color.G * luminance);
                pixels[i + 2] = (byte)(color.R * luminance);
            }

            var tinted = new WriteableBitmap(width, height, converted.DpiX, converted.DpiY, PixelFormats.Bgra32, null);
            tinted.WritePixels(new Int32Rect(0, 0, width, height), pixels, stride, 0);
            tinted.Freeze();
            AuroraOrbImage.Source = tinted;
        }

        private static Color ParseColor(string? value)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(value))
                    return (Color)ColorConverter.ConvertFromString(value)!;
            }
            catch { }
            return Color.FromRgb(79, 216, 232);
        }
    }
}
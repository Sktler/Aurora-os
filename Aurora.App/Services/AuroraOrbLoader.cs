using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Aurora.App.Services
{
    internal static class AuroraOrbLoader
    {
        private const string ResourceUri = "/Aurora;component/Assets/AuroraOrb.png";

        public static void Apply(DependencyObject root)
        {
            var image = FindTargetImage(root);
            if (image == null) return;

            try
            {
                var streamInfo = Application.GetResourceStream(new Uri(ResourceUri, UriKind.Absolute));
                if (streamInfo == null) return;

                using var stream = streamInfo.Stream;
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = stream;
                bitmap.EndInit();
                bitmap.Freeze();
                image.Source = bitmap;
            }
            catch
            {
                // The orb is visual-only; never let asset loading crash Aurora.
            }
        }

        private static Image? FindTargetImage(DependencyObject root)
        {
            if (root is Image image && IsOrbImage(image)) return image;
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            {
                var found = FindTargetImage(VisualTreeHelper.GetChild(root, i));
                if (found != null) return found;
            }
            return null;
        }

        private static bool IsOrbImage(Image image)
            => string.Equals(image.Tag as string, "AuroraOrb", StringComparison.OrdinalIgnoreCase)
            || string.Equals(image.Name, "AuroraOrbImage", StringComparison.OrdinalIgnoreCase);
    }
}
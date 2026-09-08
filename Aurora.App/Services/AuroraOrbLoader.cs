using System;
using System.IO;
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

            var streamInfo = Application.GetResourceStream(new Uri(ResourceUri, UriKind.Absolute));
            if (streamInfo == null) return;

            using var reader = new StreamReader(streamInfo.Stream);
            var base64 = reader.ReadToEnd().Trim();
            var bytes = Convert.FromBase64String(base64);
            using var ms = new MemoryStream(bytes);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = ms;
            bitmap.EndInit();
            bitmap.Freeze();
            image.Source = bitmap;
        }

        private static Image? FindTargetImage(DependencyObject root)
        {
            if (root is Image image && IsOrbImage(image))
            {
                return image;
            }

            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                var found = FindTargetImage(child);
                if (found != null) return found;
            }

            return null;
        }

        private static bool IsOrbImage(Image image)
        {
            return string.Equals(image.Tag as string, "AuroraOrb", StringComparison.OrdinalIgnoreCase)
                || string.Equals(image.Name, "AuroraOrbImage", StringComparison.OrdinalIgnoreCase);
        }
    }
}
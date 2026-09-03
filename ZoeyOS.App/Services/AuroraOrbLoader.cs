using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ZoeyOS.App.Services
{
    internal static class AuroraOrbLoader
    {
        private const string ResourceUri = "/ZoeyOS.App;component/Assets/AuroraOrbNew.base64";

        public static void Apply(DependencyObject root)
        {
            var image = FindFirstImage(root);
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

        private static Image? FindFirstImage(DependencyObject root)
        {
            if (root is Image image) return image;
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                var found = FindFirstImage(child);
                if (found != null) return found;
            }
            return null;
        }
    }
}
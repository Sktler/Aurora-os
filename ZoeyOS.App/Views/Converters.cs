using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace ZoeyOS.App.Views
{
    // All the app's small XAML value converters live here together rather than
    // one file each - each is a handful of lines and they're only ever used from XAML.

    public class RoleToAlignmentConverter : IValueConverter
    {
        public static readonly RoleToAlignmentConverter Instance = new();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            (value as string) == "user" ? HorizontalAlignment.Right : HorizontalAlignment.Left;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotImplementedException();
    }

    public class RoleToBrushConverter : IValueConverter
    {
        public static readonly RoleToBrushConverter Instance = new();

        private static readonly SolidColorBrush UserBrush = new(Color.FromRgb(0x8C, 0x6F, 0xF0));
        private static readonly SolidColorBrush AssistantBrush = new(Color.FromRgb(0x17, 0x1C, 0x28));

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            (value as string) == "user" ? UserBrush : AssistantBrush;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotImplementedException();
    }

    /// <summary>
    /// The user bubble's violet background (#8C6FF0) is too bright for the app's usual
    /// near-white text - that combination measures ~3.2:1 contrast, below the 4.5:1 WCAG AA
    /// minimum for normal-size text, which is what made it hard to read. Assistant bubbles
    /// use a near-black background where the same near-white text already passes AA
    /// comfortably, so only the user bubble's text needs to flip to a dark color instead.
    /// </summary>
    public class RoleToTextBrushConverter : IValueConverter
    {
        public static readonly RoleToTextBrushConverter Instance = new();

        private static readonly SolidColorBrush UserTextBrush = new(Color.FromRgb(0x0B, 0x0E, 0x14)); // BgDeepColor
        private static readonly SolidColorBrush AssistantTextBrush = new(Color.FromRgb(0xEA, 0xEE, 0xF5)); // TextPrimaryColor

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            (value as string) == "user" ? UserTextBrush : AssistantTextBrush;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotImplementedException();
    }

    /// <summary>
    /// Converts a bool to Visibility. Pass parameter "Invert" to flip the mapping
    /// (used to show the name/pencil when NOT renaming, and the edit box when renaming).
    /// </summary>
    public class BoolToVisibilityConverter : IValueConverter
    {
        public static readonly BoolToVisibilityConverter Instance = new();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var flag = value is bool b && b;
            if (parameter is string s && s == "Invert") flag = !flag;
            return flag ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotImplementedException();
    }

    public class BoolToStatusTextConverter : IValueConverter
    {
        public static readonly BoolToStatusTextConverter Instance = new();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            (value is bool b && b) ? "Connected" : "Not connected";

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotImplementedException();
    }

    public class BoolToToggleLabelConverter : IValueConverter
    {
        public static readonly BoolToToggleLabelConverter Instance = new();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            (value is bool b && b) ? "Mark as not connected" : "Mark as connected";

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotImplementedException();
    }

    /// <summary>True (playing) shows a pause glyph; false (paused) shows a play glyph -
    /// the button's icon always shows the action a click will perform, not the current state.</summary>
    public class BoolToPlayPauseIconConverter : IValueConverter
    {
        public static readonly BoolToPlayPauseIconConverter Instance = new();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            (value is bool b && b) ? "⏸" : "▶";

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotImplementedException();
    }
}

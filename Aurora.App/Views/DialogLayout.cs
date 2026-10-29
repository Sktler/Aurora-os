using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;

namespace Aurora.App.Views
{
    public static class DialogLayout
    {
        // Keep a readable width, let translated text determine the height, and
        // retain scrolling when the content cannot fit on the current monitor.
        public static void UseContentHeight(Window window)
        {
            var content = window.Content;
            window.Content = null;
            window.Content = new ScrollViewer
            {
                Content = content,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };
            window.SizeToContent = SizeToContent.Height;
            window.ResizeMode = ResizeMode.CanResize;
            if (!double.IsNaN(window.Width))
                window.MinWidth = Math.Max(window.MinWidth, Math.Min(window.Width, 320));
            window.MinHeight = Math.Max(window.MinHeight, 160);
            ConstrainToWorkArea(window);
        }

        public static void ConstrainToWorkArea(Window window)
        {
            var minWidth = window.MinWidth;
            var minHeight = window.MinHeight;
            var maxWidth = window.MaxWidth;
            var maxHeight = window.MaxHeight;
            var workArea = SystemParameters.WorkArea;

            void UpdateBounds()
            {
                var handle = new WindowInteropHelper(window).Handle;
                if (handle == IntPtr.Zero) return;
                var area = System.Windows.Forms.Screen.FromHandle(handle).WorkingArea;
                var dpi = VisualTreeHelper.GetDpi(window);
                workArea = new Rect(area.Left / dpi.DpiScaleX, area.Top / dpi.DpiScaleY,
                    area.Width / dpi.DpiScaleX, area.Height / dpi.DpiScaleY);
                var width = Math.Min(maxWidth, workArea.Width);
                var height = Math.Min(maxHeight, workArea.Height);
                window.MinWidth = Math.Min(minWidth, width);
                window.MinHeight = Math.Min(minHeight, height);
                window.MaxWidth = width;
                window.MaxHeight = height;
            }

            void KeepOnScreen()
            {
                if (!window.IsLoaded || window.WindowState != WindowState.Normal) return;
                window.Left = Math.Clamp(window.Left, workArea.Left,
                    Math.Max(workArea.Left, workArea.Right - window.ActualWidth));
                window.Top = Math.Clamp(window.Top, workArea.Top,
                    Math.Max(workArea.Top, workArea.Bottom - window.ActualHeight));
            }

            window.SourceInitialized += (_, _) => UpdateBounds();
            window.LocationChanged += (_, _) => UpdateBounds();
            window.DpiChanged += (_, _) => UpdateBounds();
            window.Loaded += (_, _) => { UpdateBounds(); KeepOnScreen(); };
            window.SizeChanged += (_, _) => KeepOnScreen();
            UpdateBounds();
        }
    }
}

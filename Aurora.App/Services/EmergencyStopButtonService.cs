using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;

namespace Aurora.App.Services;

public sealed class EmergencyStopButtonService : IDisposable
{
    private readonly DispatcherTimer _timer;
    private readonly Dictionary<Window, Popup> _buttons = new();
    private bool _disposed;

    public EmergencyStopButtonService()
    {
        _timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };
        _timer.Tick += (_, _) => AttachToWindows();
        _timer.Start();
        AttachToWindows();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timer.Stop();

        foreach (var pair in _buttons)
        {
            pair.Key.Closed -= Window_Closed;
            pair.Value.IsOpen = false;
        }

        _buttons.Clear();
    }

    private void AttachToWindows()
    {
        if (_disposed || Application.Current == null) return;

        foreach (Window window in Application.Current.Windows)
        {
            if (window is Views.LockdownWindow || _buttons.ContainsKey(window))
                continue;

            var root = window.Content as UIElement;
            if (root == null) continue;

            var button = new Button
            {
                Content = "⛔  EMERGENCY STOP",
                ToolTip = "Immediately lock Aurora",
                Padding = new Thickness(12, 7, 12, 7),
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                Background = new SolidColorBrush(Color.FromRgb(150, 35, 45)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(255, 105, 115)),
                BorderThickness = new Thickness(1),
                Cursor = System.Windows.Input.Cursors.Hand,
                Focusable = true
            };
            button.Click += (_, _) => TriggerLockdown();

            var popup = new Popup
            {
                PlacementTarget = root,
                Placement = PlacementMode.RelativePoint,
                HorizontalOffset = Math.Max(8, window.ActualWidth - 185),
                VerticalOffset = 10,
                AllowsTransparency = true,
                StaysOpen = true,
                IsOpen = window.IsVisible,
                Child = new Border
                {
                    CornerRadius = new CornerRadius(8),
                    Background = new SolidColorBrush(Color.FromArgb(245, 20, 23, 31)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(100, 35, 45)),
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(1),
                    Child = button
                }
            };

            window.SizeChanged += Window_SizeChanged;
            window.LocationChanged += Window_LocationChanged;
            window.IsVisibleChanged += Window_IsVisibleChanged;
            window.Closed += Window_Closed;
            _buttons[window] = popup;
        }
    }

    private void TriggerLockdown()
    {
        if (App.Security == null) return;
        App.EnterLockdown("Emergency stop requested from Aurora UI.");
    }

    private void Window_SizeChanged(object sender, SizeChangedEventArgs e) => UpdatePosition(sender as Window);
    private void Window_LocationChanged(object? sender, EventArgs e) => UpdatePosition(sender as Window);

    private void Window_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is not Window window || !_buttons.TryGetValue(window, out var popup)) return;
        popup.IsOpen = window.IsVisible && !App.Security.IsLockedDown;
    }

    private void UpdatePosition(Window? window)
    {
        if (window == null || !_buttons.TryGetValue(window, out var popup)) return;
        popup.HorizontalOffset = Math.Max(8, window.ActualWidth - 185);
        popup.VerticalOffset = 10;
    }

    private void Window_Closed(object? sender, EventArgs e)
    {
        if (sender is not Window window || !_buttons.Remove(window, out var popup)) return;
        window.SizeChanged -= Window_SizeChanged;
        window.LocationChanged -= Window_LocationChanged;
        window.IsVisibleChanged -= Window_IsVisibleChanged;
        window.Closed -= Window_Closed;
        popup.IsOpen = false;
    }
}

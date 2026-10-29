using System.IO;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using Aurora.App;
using Aurora.App.Services;
using Xunit;

namespace AuroraUpdater.Tests;

[Collection("App.Settings")]
public sealed class EmergencyStopButtonServiceTests
{
    [Fact]
    public void Popups_follow_their_own_windows_during_moves_and_resizes()
    {
        RunOnSta(_ =>
        {
            var first = CreateWindow(40, 40);
            var second = CreateWindow(100, 100);
            using var service = new EmergencyStopButtonService();
            try
            {
                first.Show();
                second.Show();
                var firstPopup = Assert.IsType<Popup>(service.AttachToWindow(first));
                var secondPopup = Assert.IsType<Popup>(service.AttachToWindow(second));
                DrainDispatcher();
                Assert.Same(firstPopup, service.AttachToWindow(first));

                var firstAnchor = GetAnchor(firstPopup);
                var secondPosition = GetScreenPosition(secondPopup);
                var closedCount = 0;
                firstPopup.Closed += (_, _) => closedCount++;

                first.Left += 40;
                DrainDispatcher();
                AssertPoint(firstAnchor, GetAnchor(firstPopup));
                AssertPoint(secondPosition, GetScreenPosition(secondPopup));

                first.Top += 40;
                DrainDispatcher();
                AssertPoint(firstAnchor, GetAnchor(firstPopup));

                first.Width += 80;
                DrainDispatcher();
                AssertPoint(new Point(firstAnchor.X + 80, firstAnchor.Y), GetAnchor(firstPopup));

                first.Height += 40;
                DrainDispatcher();
                AssertPoint(new Point(firstAnchor.X + 80, firstAnchor.Y), GetAnchor(firstPopup));

                // The horizontal offset stays at its minimum for narrow windows.
                first.Width = 180;
                DrainDispatcher();
                var narrowAnchor = GetAnchor(firstPopup);
                Assert.Equal(8, firstPopup.HorizontalOffset);
                first.Left += 20;
                first.Width = 160;
                DrainDispatcher();
                AssertPoint(narrowAnchor, GetAnchor(firstPopup));

                var firstPosition = GetScreenPosition(firstPopup);
                var secondAnchor = GetAnchor(secondPopup);
                second.Left += 30;
                second.Top += 30;
                DrainDispatcher();
                AssertPoint(secondAnchor, GetAnchor(secondPopup));
                AssertPoint(firstPosition, GetScreenPosition(firstPopup));
                Assert.True(firstPopup.IsOpen);
                Assert.True(secondPopup.IsOpen);
                Assert.Equal(0, closedCount);
            }
            finally
            {
                first.Close();
                second.Close();
            }
        });
    }

    [Fact]
    public void Moving_and_resizing_do_not_reopen_hidden_or_locked_down_controls()
    {
        RunOnSta(security =>
        {
            var window = CreateWindow(40, 40);
            using var service = new EmergencyStopButtonService();
            try
            {
                var popup = Assert.IsType<Popup>(service.AttachToWindow(window));
                Assert.False(popup.IsOpen);
                window.Show();
                DrainDispatcher();
                Assert.True(popup.IsOpen);

                window.Hide();
                window.Left += 40;
                window.Width += 40;
                DrainDispatcher();
                Assert.False(popup.IsOpen);

                window.Show();
                DrainDispatcher();
                Assert.True(popup.IsOpen);
                Assert.Equal(Math.Max(8, window.ActualWidth - 185), GetAnchor(popup).X, 0);

                security.EnterLockdown("popup regression test");
                window.Hide();
                window.Show();
                window.Left += 40;
                window.Width += 40;
                DrainDispatcher();
                Assert.False(popup.IsOpen);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void Closing_a_window_or_disposing_the_service_removes_its_control()
    {
        RunOnSta(_ =>
        {
            var first = CreateWindow(40, 40);
            var second = CreateWindow(100, 100);
            using var service = new EmergencyStopButtonService();
            try
            {
                first.Show();
                second.Show();
                var firstPopup = Assert.IsType<Popup>(service.AttachToWindow(first));
                var secondPopup = Assert.IsType<Popup>(service.AttachToWindow(second));
                DrainDispatcher();

                first.Close();
                DrainDispatcher();
                Assert.False(firstPopup.IsOpen);
                Assert.True(secondPopup.IsOpen);

                service.Dispose();
                second.Hide();
                second.Show();
                second.Left += 40;
                second.Width += 40;
                DrainDispatcher();
                Assert.False(secondPopup.IsOpen);
                Assert.Null(service.AttachToWindow(second));
            }
            finally
            {
                first.Close();
                second.Close();
            }
        });
    }

    private static Window CreateWindow(double left, double top) => new()
    {
        Left = SystemParameters.WorkArea.Left + left,
        Top = SystemParameters.WorkArea.Top + top,
        Width = 320,
        Height = 200,
        WindowStartupLocation = WindowStartupLocation.Manual,
        WindowStyle = WindowStyle.None,
        ShowInTaskbar = false,
        ShowActivated = false,
        Content = new Grid()
    };

    private static Point GetScreenPosition(Popup popup) => popup.Child.PointToScreen(new Point());

    private static Point GetAnchor(Popup popup) => popup.PlacementTarget.PointFromScreen(GetScreenPosition(popup));

    private static void AssertPoint(Point expected, Point actual)
    {
        // Native popup placement rounds to whole screen pixels.
        Assert.InRange(actual.X, expected.X - 1, expected.X + 1);
        Assert.InRange(actual.Y, expected.Y - 1, expected.Y + 1);
    }

    private static void DrainDispatcher()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }

    private static void RunOnSta(Action<InstallationSecurityService> test)
    {
        ExceptionDispatchInfo? failure = null;
        var thread = new Thread(() =>
        {
            var previousSecurity = App.Security;
            var statePath = Path.Combine(AppContext.BaseDirectory, $"aurora-popup-{Guid.NewGuid():N}.lock");
            var security = new InstallationSecurityService(statePath);
            var property = typeof(App).GetProperty(nameof(App.Security), BindingFlags.Public | BindingFlags.Static);
            try
            {
                Assert.NotNull(property);
                property.SetValue(null, security);
                test(security);
            }
            catch (Exception ex)
            {
                failure = ExceptionDispatchInfo.Capture(ex);
            }
            finally
            {
                property?.SetValue(null, previousSecurity);
                File.Delete(statePath);
                Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        }) { IsBackground = true };

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(15)), "The popup test did not complete.");
        failure?.Throw();
    }
}

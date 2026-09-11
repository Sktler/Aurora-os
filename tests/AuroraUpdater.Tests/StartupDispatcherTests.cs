using System;
using System.Threading;
using System.Windows.Threading;
using Xunit;

namespace AuroraUpdater.Tests;

public sealed class StartupDispatcherTests
{
    [Fact]
    public void ApplicationIdleAwait_ResumesBeforeDispatcherShutdown()
    {
        Exception? failure = null;
        var continuationReached = false;
        using var completed = new ManualResetEventSlim();

        var thread = new Thread(() =>
        {
            try
            {
                var dispatcher = Dispatcher.CurrentDispatcher;
                dispatcher.InvokeAsync(async () =>
                {
                    await dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                    continuationReached = true;
                    dispatcher.BeginInvokeShutdown(DispatcherPriority.Background);
                }, DispatcherPriority.Send);

                Dispatcher.Run();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
            finally
            {
                completed.Set();
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        Assert.True(completed.Wait(TimeSpan.FromSeconds(5)), "The dispatcher did not complete.");
        thread.Join(TimeSpan.FromSeconds(1));

        Assert.Null(failure);
        Assert.True(continuationReached, "The continuation did not resume after ApplicationIdle.");
    }
}

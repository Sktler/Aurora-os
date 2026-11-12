using System.Text.Json;
using System.Windows.Threading;
using Aurora.App;
using Aurora.App.Services;
using Xunit;

namespace AuroraUpdater.Tests;

[Collection("App.Settings")]
public sealed class CameraServiceTests
{
    [Fact]
    public async Task DisabledCamera_RejectsToolsAndAliasesBeforeCallingTheService()
    {
        using var settings = new AppSettingsTestScope();
        App.Settings.WindowsCameraEnabled = false;
        using var input = JsonDocument.Parse("{}");
        const string denied = "Camera permission is disabled in Aurora Settings.";

        foreach (var definition in CameraTools.Definitions)
        {
            var toolName = (string)definition.GetType().GetProperty("name")!.GetValue(definition)!;
            Assert.Equal(denied, await CameraTools.ExecuteAsync(toolName, input.RootElement));
        }

        Assert.Equal(denied, await SystemTools.ExecuteAsync("camera_list_devices", input.RootElement));
        Assert.Equal(denied, await SystemTools.ExecuteAsync("camera_check_permission", input.RootElement));
    }

    [Fact]
    public async Task DisabledCamera_RejectsDirectAccessAndStillAllowsCleanup()
    {
        await using var camera = new CameraService(() => false);
        var cancellationToken = TestContext.Current.CancellationToken;

        Assert.Equal(CameraPermissionResult.Denied, await camera.CheckPermissionAsync());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => camera.RefreshDevicesAsync(cancellationToken));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => camera.InitializeAsync(cancellationToken: cancellationToken));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => camera.StartPreviewAsync(cancellationToken));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => camera.CapturePhotoAsync(cancellationToken: cancellationToken));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => camera.StartRecordingAsync(cancellationToken: cancellationToken));
        Assert.Throws<UnauthorizedAccessException>(camera.OpenWindowsCameraApp);
        await camera.StopAsync();
    }

    [Fact]
    public async Task DisposalDuringDiscovery_CompletesWhileDispatcherIsBlocked()
    {
        var timeout = TimeSpan.FromSeconds(5);
        var cancellationToken = TestContext.Current.CancellationToken;
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            var uiThreadId = Environment.CurrentManagedThreadId;
            var probeStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var finishProbe = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var camera = new CameraService(() =>
            {
                if (Environment.CurrentManagedThreadId == uiThreadId)
                    return true;

                // Stall discovery before it opens hardware, then simulate revocation.
                probeStarted.TrySetResult();
                finishProbe.Task.WaitAsync(timeout, cancellationToken).GetAwaiter().GetResult();
                return false;
            });

            var dispatcher = Dispatcher.CurrentDispatcher;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
            try
            {
                var refresh = camera.RefreshDevicesAsync(cancellationToken);
                probeStarted.Task.WaitAsync(timeout, cancellationToken).GetAwaiter().GetResult();
                var shutdown = camera.DisposeAsync().AsTask();
                finishProbe.SetResult();

                // Match App.OnExit, where the dispatcher cannot run while disposal is pending.
                shutdown.WaitAsync(timeout, cancellationToken).GetAwaiter().GetResult();
                Assert.Throws<UnauthorizedAccessException>(
                    () => { refresh.WaitAsync(timeout, cancellationToken).GetAwaiter().GetResult(); });
                completed.SetResult();
            }
            catch (Exception ex)
            {
                completed.SetException(ex);
            }
            finally
            {
                finishProbe.TrySetResult();
                dispatcher.InvokeShutdown();
            }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        await completed.Task.WaitAsync(timeout + timeout, cancellationToken);
    }
}

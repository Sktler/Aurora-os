using Aurora.App;
using Aurora.App.Services;
using Xunit;

namespace AuroraUpdater.Tests;

public sealed class WakeWordServiceTests
{
    [Fact]
    public void Denied_permission_never_creates_a_recognizer_and_can_be_enabled_later()
    {
        var allowed = false;
        var creations = 0;
        var recognizer = new FakeRecognizer();
        using var service = new WakeWordService(() => allowed, () => 0.7, () =>
        {
            creations++;
            return recognizer;
        });

        Assert.Equal(0, creations);
        Assert.False(service.IsEnabled);
        Assert.False(service.Start());
        Assert.Equal(0, creations);
        Assert.Contains("microphone access", service.Status);

        allowed = true;
        Assert.True(service.Start());
        Assert.True(service.IsAvailable);
        Assert.True(service.Start());
        Assert.Equal(1, creations);

        allowed = false;
        Assert.False(service.Start());
        Assert.False(service.IsEnabled);
        Assert.Equal(1, recognizer.Disposals);
    }

    [Fact]
    public void Failed_start_releases_resources_and_retries_instead_of_caching_unavailability()
    {
        var failed = new FakeRecognizer { FailWakeStart = true };
        var working = new FakeRecognizer();
        var creations = 0;
        using var service = new WakeWordService(() => true, () => 0.7,
            () => ++creations == 1 ? failed : working);

        Assert.False(service.Start());
        Assert.False(service.IsAvailable);
        Assert.Equal(1, failed.Disposals);
        Assert.True(service.Start());
        Assert.True(service.IsAvailable);
        Assert.Equal(2, creations);
    }

    [Fact]
    public void Activation_waits_for_completion_then_captures_one_command_on_the_same_recognizer()
    {
        var recognizer = new FakeRecognizer();
        var creations = 0;
        using var service = new WakeWordService(() => true, () => 0.7, () =>
        {
            creations++;
            return recognizer;
        });
        var activations = 0;
        var commands = new List<string>();
        service.WakeWordDetected += () => activations++;
        service.CommandRecognized += commands.Add;
        service.Start();

        recognizer.Hear("hey aurora", 0.3f);
        recognizer.Hear("ordinary conversation", 0.95f);
        Assert.Equal(0, recognizer.Cancellations);
        recognizer.Hear("Hey Aurora", 0.65f);
        recognizer.Hear("Hey Aurora", 0.65f);
        Assert.Equal(1, activations);
        Assert.Equal(1, recognizer.Cancellations);
        Assert.Equal(0, recognizer.CommandStarts);
        Assert.Equal(0, recognizer.Disposals);

        recognizer.Complete(cancelled: true);
        Assert.Equal(1, recognizer.CommandStarts);
        recognizer.Hear("turn on the lights", 0.95f);
        Assert.Empty(commands); // Only a completed dictation result is submitted.
        recognizer.Complete(" turn on the lights ", 0.95f);

        Assert.Equal(new[] { "turn on the lights" }, commands);
        Assert.Equal(2, recognizer.WakeStarts);
        Assert.Equal(1, creations);
        Assert.Equal(0, recognizer.Disposals);
        Assert.True(service.IsEnabled);
    }

    [Fact]
    public void Command_confidence_uses_current_settings_and_completion_cannot_bypass_the_filter()
    {
        var threshold = 0.9;
        var recognizer = new FakeRecognizer();
        using var service = new WakeWordService(() => true, () => threshold, () => recognizer);
        var commands = new List<string>();
        service.CommandRecognized += commands.Add;
        service.Start();

        BeginCommand(recognizer);
        recognizer.Complete("low confidence", 0.8f);
        Assert.Empty(commands);

        threshold = 0.6;
        BeginCommand(recognizer);
        recognizer.Complete("accepted with saved threshold", 0.8f);
        Assert.Single(commands);

        BeginCommand(recognizer);
        recognizer.Complete("cancelled result", 0.99f, cancelled: true);
        BeginCommand(recognizer);
        recognizer.Complete(" ", 0.99f);
        Assert.Single(commands);
        Assert.Equal(5, recognizer.WakeStarts);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Stop_during_activation_or_dictation_ignores_late_results_and_cannot_restart(bool startDictation)
    {
        var first = new FakeRecognizer();
        var second = new FakeRecognizer();
        var creations = 0;
        using var service = new WakeWordService(() => true, () => 0.7,
            () => ++creations == 1 ? first : second);
        var commands = new List<string>();
        service.CommandRecognized += commands.Add;
        service.Start();
        first.Hear("hey aurora", 0.8f);
        if (startDictation) first.Complete(cancelled: true);

        service.Stop();
        Assert.False(service.IsEnabled);
        Assert.Equal(1, first.Disposals);
        Assert.True(service.Start());
        first.Complete("stale command", 0.99f);
        first.Hear("hey aurora", 0.99f);
        Assert.Empty(commands);
        Assert.Equal(1, first.WakeStarts);
        Assert.Equal(0, second.CommandStarts);

        BeginCommand(second);
        second.Complete("new command", 0.99f);
        Assert.Equal(new[] { "new command" }, commands);
    }

    [Fact]
    public void Revocation_and_disposal_prevent_pending_commands_and_future_restarts()
    {
        var allowed = true;
        var recognizer = new FakeRecognizer();
        using var service = new WakeWordService(() => allowed, () => 0.7, () => recognizer);
        var commands = new List<string>();
        service.CommandRecognized += commands.Add;
        service.Start();
        BeginCommand(recognizer);

        allowed = false;
        recognizer.Complete("must not be submitted", 0.99f);
        Assert.Empty(commands);
        service.Start();
        service.Dispose();
        allowed = true;
        Assert.False(service.Start());
        Assert.Equal(1, recognizer.Disposals);
        Assert.Equal(1, recognizer.WakeStarts);
    }

    [Fact]
    public async Task Recognition_error_stops_listening_and_can_be_retried()
    {
        var recognizer = new FakeRecognizer();
        var replacement = new FakeRecognizer();
        var creations = 0;
        using var service = new WakeWordService(() => true, () => 0.7,
            () => ++creations == 1 ? recognizer : replacement);
        service.Start();
        recognizer.Complete(error: new InvalidOperationException("Microphone disconnected"));

        Assert.False(service.IsEnabled);
        Assert.False(service.IsAvailable);
        Assert.Contains("Wake word stopped", service.Status);
        await recognizer.Disposed.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.True(service.Start());
        Assert.Equal(1, replacement.WakeStarts);
    }

    [Fact]
    public async Task Command_timeout_cancels_capture_and_resumes_wake_listening_without_submitting_audio()
    {
        var recognizer = new FakeRecognizer();
        using var service = new WakeWordService(() => true, () => 0.7, () => recognizer);
        var commands = new List<string>();
        service.CommandRecognized += commands.Add;
        service.Start();
        BeginCommand(recognizer);

        await recognizer.SecondCancellation.Task.WaitAsync(TimeSpan.FromSeconds(12), TestContext.Current.CancellationToken);
        recognizer.Complete("incomplete command", 0.99f, cancelled: true);

        Assert.Empty(commands);
        Assert.Contains("Listening timed out", service.Status);
        Assert.Equal(2, recognizer.WakeStarts);
        Assert.True(service.IsEnabled);
    }

    private static void BeginCommand(FakeRecognizer recognizer)
    {
        recognizer.Hear("hey aurora", 0.8f);
        recognizer.Complete(cancelled: true);
    }

    internal sealed class FakeRecognizer : IWakeWordRecognizer
    {
        public event Action<string, float>? SpeechRecognized;
        public event Action<string?, float, bool, Exception?>? RecognitionCompleted;
        public int WakeStarts { get; private set; }
        public int CommandStarts { get; private set; }
        public int Cancellations { get; private set; }
        public int Disposals { get; private set; }
        public bool FailWakeStart { get; init; }
        public TaskCompletionSource Disposed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource SecondCancellation { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void StartWakeWord()
        {
            if (FailWakeStart) throw new InvalidOperationException("Microphone unavailable");
            WakeStarts++;
        }

        public void StartCommand() => CommandStarts++;
        public void Cancel()
        {
            Cancellations++;
            if (Cancellations == 2) SecondCancellation.TrySetResult();
        }
        public void Hear(string text, float confidence) => SpeechRecognized?.Invoke(text, confidence);
        public void Complete(string? text = null, float confidence = 0, bool cancelled = false, Exception? error = null) =>
            RecognitionCompleted?.Invoke(text, confidence, cancelled, error);

        public void Dispose()
        {
            Disposals++;
            Disposed.TrySetResult();
        }
    }
}

[Collection("App.Settings")]
public sealed class VoicePermissionTests
{
    [Fact]
    public void Permission_refresh_stops_activation_and_spoken_reply_preference_does_not_enable_the_microphone()
    {
        var previousSettings = App.Settings;
        var wakeProperty = typeof(App).GetProperty(nameof(App.WakeWord))!;
        var voiceProperty = typeof(App).GetProperty(nameof(App.Voice))!;
        var previousWake = wakeProperty.GetValue(null);
        var previousVoice = voiceProperty.GetValue(null);
        var creations = 0;
        var recognizer = new WakeWordServiceTests.FakeRecognizer();
        using var service = new WakeWordService(() => App.Settings.WindowsMicrophoneEnabled, () => 0.7, () =>
        {
            creations++;
            return recognizer;
        });

        try
        {
            App.Settings = new AppSettings { WindowsMicrophoneEnabled = false, SpeakRepliesByDefault = true };
            wakeProperty.GetSetMethod(true)!.Invoke(null, new object?[] { service });
            voiceProperty.GetSetMethod(true)!.Invoke(null, new object?[] { null });

            App.RefreshVoiceActivation();
            Assert.Equal(0, creations);
            Assert.False(service.IsEnabled);
            Assert.Contains("microphone access", service.Status);

            // Permission, rather than the independent speech-output preference, controls input.
            App.Settings.SpeakRepliesByDefault = false;
            App.Settings.WindowsMicrophoneEnabled = true;
            App.RefreshVoiceActivation();
            Assert.Equal(1, creations);
            Assert.True(service.IsEnabled);
            Assert.False(App.Settings.SpeakRepliesByDefault);

            App.Settings.WindowsMicrophoneEnabled = false;
            App.RefreshVoiceActivation();
            Assert.False(service.IsEnabled);
            Assert.Equal(1, recognizer.Disposals);
            Assert.Contains("microphone access", service.Status);
        }
        finally
        {
            App.Settings = previousSettings;
            wakeProperty.GetSetMethod(true)!.Invoke(null, new[] { previousWake });
            voiceProperty.GetSetMethod(true)!.Invoke(null, new[] { previousVoice });
        }
    }

    [Fact]
    public void Manual_listening_is_denied_when_microphone_access_is_disabled()
    {
        var previous = App.Settings;
        try
        {
            App.Settings = new AppSettings { WindowsMicrophoneEnabled = false, SpeakRepliesByDefault = true };
            using var voice = new VoiceService();
            Assert.False(voice.StartContinuousListening(_ => Assert.Fail("Unexpected voice command"), () => { }));
            Assert.False(voice.IsListening);
            Assert.False(App.Settings.WindowsMicrophoneEnabled);
            Assert.True(App.Settings.SpeakRepliesByDefault);
        }
        finally { App.Settings = previous; }
    }
}

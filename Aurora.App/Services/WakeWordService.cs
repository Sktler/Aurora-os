using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Aurora.App.Services
{
    /// <summary>Local, offline wake-word detection followed by one bounded dictation utterance.</summary>
    public sealed class WakeWordService : IDisposable
    {
        private enum ListeningPhase { WakeWord, CommandPending, Command }

        private readonly object _gate = new();
        private readonly Func<bool> _microphoneAllowed;
        private readonly Func<double> _commandConfidence;
        private readonly Func<IWakeWordRecognizer> _createRecognizer;
        private IWakeWordRecognizer? _recognizer;
        private Timer? _commandTimeout;
        private int _commandGeneration;
        private ListeningPhase _phase;
        private bool _commandTimedOut;
        private bool _enabled;
        private bool _disposed;

        public bool IsAvailable { get; private set; }
        public bool IsEnabled => _enabled;
        public string WakePhrase => "Hey Aurora";
        public string Status { get; private set; } = "Wake word is off.";

        public event Action? WakeWordDetected;
        public event Action<string>? CommandRecognized;
        public event Action<string>? StatusChanged;

        public WakeWordService() : this(
            () => App.Settings?.WindowsMicrophoneEnabled == true,
            () => App.Settings?.VoiceConfidenceThreshold ?? VoiceTranscriptFilter.DefaultConfidenceThreshold,
            () => new WakeWordRecognizer(App.Settings?.MicrophoneDeviceName))
        {
        }

        internal WakeWordService(Func<bool> microphoneAllowed, Func<double> commandConfidence,
            Func<IWakeWordRecognizer> createRecognizer)
        {
            // Construction must not open the microphone or cache a failed permission probe.
            _microphoneAllowed = microphoneAllowed;
            _commandConfidence = commandConfidence;
            _createRecognizer = createRecognizer;
        }

        public bool Start()
        {
            IWakeWordRecognizer? failedRecognizer = null;
            lock (_gate)
            {
                if (_disposed) return false;
                if (!_microphoneAllowed())
                {
                    failedRecognizer = DetachRecognizerLocked();
                    SetStatus("Wake word is off. Enable microphone access in Windows capabilities to use voice input.");
                }
                else if (_enabled)
                {
                    return true;
                }
                else
                {
                    try
                    {
                        var recognizer = _createRecognizer();
                        _recognizer = recognizer;
                        recognizer.SpeechRecognized += (text, confidence) => OnSpeechRecognized(recognizer, text, confidence);
                        recognizer.RecognitionCompleted += (text, confidence, cancelled, error) =>
                            OnRecognitionCompleted(recognizer, text, confidence, cancelled, error);
                        _phase = ListeningPhase.WakeWord;
                        _enabled = true;
                        recognizer.StartWakeWord();
                        IsAvailable = true;
                        SetStatus("Listening for “Hey Aurora”.");
                        return true;
                    }
                    catch (Exception ex)
                    {
                        failedRecognizer = DetachRecognizerLocked();
                        IsAvailable = false;
                        System.Diagnostics.Debug.WriteLine($"[WakeWord] Start failed: {ex}");
                        SetStatus("Wake word could not start. Check Windows microphone privacy settings, the selected input device, and installed speech recognition languages.");
                    }
                }
            }
            // SAPI may wait for callbacks to finish during disposal. Never hold _gate here.
            failedRecognizer?.Dispose();
            return false;
        }

        public void Stop()
        {
            IWakeWordRecognizer? recognizer;
            lock (_gate)
            {
                recognizer = DetachRecognizerLocked();
                if (!_disposed) SetStatus("Wake word is off.");
            }
            recognizer?.Dispose();
        }

        private void OnSpeechRecognized(IWakeWordRecognizer recognizer, string text, float confidence)
        {
            lock (_gate)
            {
                if (!IsCurrent(recognizer) || _phase != ListeningPhase.WakeWord ||
                    !WakeWordMatcher.IsWakeWord(text, confidence)) return;
                try
                {
                    _phase = ListeningPhase.CommandPending;
                    // Wait for RecognizeCompleted before changing grammars. Disposing SAPI
                    // inside SpeechRecognized can deadlock its callback thread.
                    recognizer.Cancel();
                    WakeWordDetected?.Invoke();
                    SetStatus("Wake word detected. Listening for your command…");
                }
                catch (Exception ex)
                {
                    FailLocked(ex);
                }
            }
        }

        private void OnRecognitionCompleted(IWakeWordRecognizer recognizer, string? text,
            float confidence, bool cancelled, Exception? error)
        {
            lock (_gate)
            {
                if (!IsCurrent(recognizer)) return;
                if (error != null)
                {
                    FailLocked(error);
                    return;
                }

                try
                {
                    if (_phase == ListeningPhase.CommandPending)
                    {
                        _phase = ListeningPhase.Command;
                        _commandTimedOut = false;
                        recognizer.StartCommand();
                        var generation = ++_commandGeneration;
                        _commandTimeout = new Timer(_ => TimeoutCommand(recognizer, generation), null,
                            TimeSpan.FromSeconds(8), Timeout.InfiniteTimeSpan);
                        return;
                    }

                    var wasCommand = _phase == ListeningPhase.Command;
                    _commandTimeout?.Dispose();
                    _commandTimeout = null;
                    var command = wasCommand && !cancelled && !_commandTimedOut &&
                        !string.IsNullOrWhiteSpace(text) && VoiceTranscriptFilter.MeetsConfidenceThreshold(
                            confidence, VoiceTranscriptFilter.ClampConfidenceThreshold(_commandConfidence()))
                        ? text.Trim() : null;

                    _phase = ListeningPhase.WakeWord;
                    recognizer.StartWakeWord();
                    SetStatus(_commandTimedOut ? "Listening timed out. Listening for “Hey Aurora”."
                        : wasCommand && command == null ? "I didn't catch a command. Listening for “Hey Aurora”."
                        : "Listening for “Hey Aurora”.");
                    if (command != null) CommandRecognized?.Invoke(command);
                }
                catch (Exception ex)
                {
                    FailLocked(ex);
                }
            }
        }

        private void TimeoutCommand(IWakeWordRecognizer recognizer, int generation)
        {
            lock (_gate)
            {
                if (!IsCurrent(recognizer) || _phase != ListeningPhase.Command || generation != _commandGeneration) return;
                try
                {
                    _commandTimedOut = true;
                    recognizer.Cancel();
                }
                catch (Exception ex) { FailLocked(ex); }
            }
        }

        private bool IsCurrent(IWakeWordRecognizer recognizer) =>
            _enabled && !_disposed && ReferenceEquals(_recognizer, recognizer) && _microphoneAllowed();

        private void FailLocked(Exception error)
        {
            System.Diagnostics.Debug.WriteLine($"[WakeWord] Recognition failed: {error}");
            var recognizer = DetachRecognizerLocked();
            IsAvailable = false;
            SetStatus("Wake word stopped. Check Windows microphone privacy settings, the selected input device, and speech recognition settings.");
            // This path runs in a recognition callback; dispose on a different thread.
            if (recognizer != null) _ = Task.Run(recognizer.Dispose);
        }

        private IWakeWordRecognizer? DetachRecognizerLocked()
        {
            var recognizer = _recognizer;
            _recognizer = null;
            _enabled = false;
            _phase = ListeningPhase.WakeWord;
            _commandGeneration++;
            _commandTimeout?.Dispose();
            _commandTimeout = null;
            return recognizer;
        }

        private void SetStatus(string status)
        {
            Status = status;
            StatusChanged?.Invoke(status);
        }

        public void Dispose()
        {
            IWakeWordRecognizer? recognizer;
            lock (_gate)
            {
                if (_disposed) return;
                _disposed = true;
                recognizer = DetachRecognizerLocked();
            }
            recognizer?.Dispose();
        }
    }
}

namespace Aurora.App.Services
{
    public static class WakeWordMatcher
    {
        public const float MinConfidence = 0.5f;

        public static readonly string[] Phrases = { "hey aurora", "hi aurora", "hey, aurora", "okay aurora" };

        public static bool IsWakeWord(string? text, float confidence)
        {
            if (confidence < MinConfidence || string.IsNullOrWhiteSpace(text)) return false;
            var normalized = text.Replace(",", "").Trim();
            return Phrases.Any(p => string.Equals(p.Replace(",", ""), normalized, StringComparison.OrdinalIgnoreCase));
        }
    }
}

using System;
using System.Globalization;
using System.Speech.Recognition;
using System.Threading;
using System.Threading.Tasks;

namespace ZoeyOS.App.Services
{
    /// <summary>
    /// Local, offline wake-word and dictation pipeline. The recognizer never sends audio to
    /// the cloud: SAPI listens only for the explicit "Hey Aurora" grammar, then captures one
    /// dictation utterance and hands only the resulting text to the model layer.
    /// </summary>
    public sealed class WakeWordService : IDisposable
    {
        private readonly object _gate = new();
        private SpeechRecognitionEngine? _wakeRecognizer;
        private SpeechRecognitionEngine? _dictationRecognizer;
        private bool _enabled;
        private bool _disposed;
        private bool _waitingForCommand;

        public bool IsAvailable { get; private set; }
        public bool IsEnabled => _enabled;
        public string WakePhrase => "Hey Aurora";

        public event Action? WakeWordDetected;
        public event Action<string>? CommandRecognized;
        public event Action<string>? StatusChanged;

        public WakeWordService()
        {
            IsAvailable = CanCreateRecognizer();
        }

        private static bool CanCreateRecognizer()
        {
            try
            {
                using var recognizer = new SpeechRecognitionEngine(CultureInfo.CurrentCulture);
                recognizer.SetInputToDefaultAudioDevice();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool Start()
        {
            lock (_gate)
            {
                if (_disposed || _enabled) return IsAvailable;
                if (!IsAvailable)
                {
                    StatusChanged?.Invoke("Wake word unavailable: no compatible microphone or speech recognizer was found.");
                    return false;
                }

                try
                {
                    _wakeRecognizer = CreateWakeRecognizer();
                    _wakeRecognizer.RecognizeAsync(RecognizeMode.Multiple);
                    _enabled = true;
                    StatusChanged?.Invoke("Listening for “Hey Aurora”.");
                    return true;
                }
                catch
                {
                    StopRecognizersLocked();
                    _enabled = false;
                    StatusChanged?.Invoke("Wake word could not start. Check microphone permissions and the default input device.");
                    return false;
                }
            }
        }

        public void Stop()
        {
            lock (_gate)
            {
                _enabled = false;
                _waitingForCommand = false;
                StopRecognizersLocked();
                if (!_disposed) StatusChanged?.Invoke("Wake word is off.");
            }
        }

        private SpeechRecognitionEngine CreateWakeRecognizer()
        {
            var recognizer = new SpeechRecognitionEngine(CultureInfo.CurrentCulture);
            recognizer.SetInputToDefaultAudioDevice();

            // A tightly constrained grammar is deliberately used here instead of dictation.
            // This substantially reduces false activations from ordinary conversation.
            var choices = new Choices("hey aurora");
            var grammar = new Grammar(new GrammarBuilder(choices)) { Name = "AuroraWakeWord" };
            recognizer.LoadGrammar(grammar);
            recognizer.SpeechRecognized += OnWakeRecognized;
            recognizer.RecognizeCompleted += OnWakeCompleted;
            return recognizer;
        }

        private void OnWakeRecognized(object? sender, SpeechRecognizedEventArgs e)
        {
            if (e.Result == null || e.Result.Confidence < 0.72f) return;

            lock (_gate)
            {
                if (!_enabled || _waitingForCommand || _disposed) return;
                _waitingForCommand = true;
                StopRecognizer(_wakeRecognizer);
                _wakeRecognizer = null;
            }

            WakeWordDetected?.Invoke();
            StatusChanged?.Invoke("Wake word detected. Listening for your command…");
            _ = CaptureCommandAsync();
        }

        private async Task CaptureCommandAsync()
        {
            SpeechRecognitionEngine? recognizer = null;
            try
            {
                recognizer = new SpeechRecognitionEngine(CultureInfo.CurrentCulture);
                recognizer.SetInputToDefaultAudioDevice();
                recognizer.LoadGrammar(new DictationGrammar());

                var completion = new TaskCompletionSource<RecognitionResult?>(TaskCreationOptions.RunContinuationsAsynchronously);
                recognizer.SpeechRecognized += (_, e) =>
                {
                    if (e.Result != null && e.Result.Confidence >= 0.45f)
                        completion.TrySetResult(e.Result);
                };
                recognizer.RecognizeCompleted += (_, e) =>
                {
                    if (e.Error == null || e.Cancelled)
                        completion.TrySetResult(e.Result);
                    else
                        completion.TrySetResult(null);
                };

                lock (_gate)
                {
                    if (!_enabled || _disposed) return;
                    _dictationRecognizer = recognizer;
                }

                recognizer.RecognizeAsync(RecognizeMode.Single);

                // Never leave the microphone open indefinitely if the user doesn't speak.
                var result = await completion.Task.WaitAsync(TimeSpan.FromSeconds(8));
                var text = result?.Text?.Trim();
                if (!string.IsNullOrWhiteSpace(text))
                    CommandRecognized?.Invoke(text);
                else
                    StatusChanged?.Invoke("I didn't catch a command.");
            }
            catch (TimeoutException)
            {
                StatusChanged?.Invoke("Listening timed out.");
            }
            catch
            {
                StatusChanged?.Invoke("Dictation failed. Check the microphone and speech settings.");
            }
            finally
            {
                lock (_gate)
                {
                    if (ReferenceEquals(_dictationRecognizer, recognizer))
                        _dictationRecognizer = null;
                    _waitingForCommand = false;
                    if (_enabled && !_disposed)
                    {
                        try
                        {
                            _wakeRecognizer = CreateWakeRecognizer();
                            _wakeRecognizer.RecognizeAsync(RecognizeMode.Multiple);
                            StatusChanged?.Invoke("Listening for “Hey Aurora”.");
                        }
                        catch
                        {
                            _enabled = false;
                            StatusChanged?.Invoke("Wake word stopped because the microphone could not be restarted.");
                        }
                    }
                }
                StopRecognizer(recognizer);
            }
        }

        private void OnWakeCompleted(object? sender, RecognizeCompletedEventArgs e)
        {
            lock (_gate)
            {
                if (!_enabled || _waitingForCommand || _disposed) return;
                try
                {
                    _wakeRecognizer?.RecognizeAsync(RecognizeMode.Multiple);
                }
                catch { }
            }
        }

        private static void StopRecognizer(SpeechRecognitionEngine? recognizer)
        {
            if (recognizer == null) return;
            try { recognizer.RecognizeAsyncCancel(); } catch { }
            try { recognizer.RecognizeAsyncStop(); } catch { }
            recognizer.Dispose();
        }

        private void StopRecognizersLocked()
        {
            StopRecognizer(_wakeRecognizer);
            StopRecognizer(_dictationRecognizer);
            _wakeRecognizer = null;
            _dictationRecognizer = null;
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed) return;
                _disposed = true;
                _enabled = false;
                _waitingForCommand = false;
                StopRecognizersLocked();
            }
        }
    }
}
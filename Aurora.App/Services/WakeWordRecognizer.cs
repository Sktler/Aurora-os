using System;
using System.Globalization;
using System.Linq;
using System.Speech.AudioFormat;
using System.Speech.Recognition;
using NAudio.Wave;

namespace Aurora.App.Services
{
    internal interface IWakeWordRecognizer : IDisposable
    {
        event Action<string, float>? SpeechRecognized;
        event Action<string?, float, bool, Exception?>? RecognitionCompleted;
        void StartWakeWord();
        void StartCommand();
        void Cancel();
    }

    /// <summary>Keeps one microphone input open while switching between wake grammar and dictation.</summary>
    internal sealed class WakeWordRecognizer : IWakeWordRecognizer
    {
        private readonly SpeechRecognitionEngine _engine;
        private readonly Grammar _wakeGrammar;
        private readonly DictationGrammar _dictationGrammar;
        private WaveInEvent? _capture;
        private LiveMicrophoneStream? _stream;
        private bool _captureStarted;

        public event Action<string, float>? SpeechRecognized;
        public event Action<string?, float, bool, Exception?>? RecognitionCompleted;

        public WakeWordRecognizer(string? microphoneName)
        {
            _engine = CreateEngine();
            try
            {
                _wakeGrammar = new Grammar(new GrammarBuilder(new Choices(WakeWordMatcher.Phrases))
                {
                    Culture = _engine.RecognizerInfo.Culture
                }) { Name = "AuroraWakeWord" };
                _dictationGrammar = new DictationGrammar { Enabled = false };
                _engine.LoadGrammar(_wakeGrammar);
                _engine.LoadGrammar(_dictationGrammar);
                var device = AudioDeviceCatalog.ResolveInputDeviceNumber(microphoneName);
                if (device < 0)
                {
                    _engine.SetInputToDefaultAudioDevice();
                }
                else
                {
                    _stream = new LiveMicrophoneStream();
                    _capture = new WaveInEvent { DeviceNumber = device, WaveFormat = new WaveFormat(16000, 16, 1) };
                    _capture.DataAvailable += OnDataAvailable;
                    _capture.RecordingStopped += OnRecordingStopped;
                    _engine.SetInputToAudioStream(_stream,
                        new SpeechAudioFormatInfo(EncodingFormat.Pcm, 16000, 16, 1, 32000, 2, null));
                }
                _engine.SpeechRecognized += OnSpeechRecognized;
                _engine.RecognizeCompleted += OnRecognitionCompleted;
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        internal static SpeechRecognitionEngine CreateEngine()
        {
            var installed = SpeechRecognitionEngine.InstalledRecognizers();
            var info = installed.FirstOrDefault(r => r.Culture.Equals(CultureInfo.CurrentCulture))
                ?? installed.FirstOrDefault(r => r.Culture.TwoLetterISOLanguageName == "en")
                ?? installed.FirstOrDefault();
            return info == null
                ? new SpeechRecognitionEngine(CultureInfo.CurrentCulture)
                : new SpeechRecognitionEngine(info);
        }

        public void StartWakeWord()
        {
            _dictationGrammar.Enabled = false;
            _wakeGrammar.Enabled = true;
            _engine.RecognizeAsync(RecognizeMode.Multiple);
            if (!_captureStarted)
            {
                _capture?.StartRecording();
                _captureStarted = true;
            }
        }

        public void StartCommand()
        {
            _wakeGrammar.Enabled = false;
            _dictationGrammar.Enabled = true;
            _engine.RecognizeAsync(RecognizeMode.Single);
        }

        public void Cancel() => _engine.RecognizeAsyncCancel();

        private void OnSpeechRecognized(object? sender, SpeechRecognizedEventArgs e) =>
            SpeechRecognized?.Invoke(e.Result.Text, e.Result.Confidence);

        private void OnRecognitionCompleted(object? sender, RecognizeCompletedEventArgs e)
        {
            var result = e.Error == null && !e.Cancelled ? e.Result : null;
            RecognitionCompleted?.Invoke(result?.Text, result?.Confidence ?? 0, e.Cancelled, e.Error);
        }

        private void OnDataAvailable(object? sender, WaveInEventArgs e)
        {
            if (e.BytesRecorded <= 0) return;
            var chunk = new byte[e.BytesRecorded];
            Array.Copy(e.Buffer, chunk, e.BytesRecorded);
            try { _stream?.Push(chunk); }
            catch (InvalidOperationException) { /* capture was stopped */ }
        }

        private void OnRecordingStopped(object? sender, StoppedEventArgs e)
        {
            _stream?.Complete();
            if (e.Exception != null) RecognitionCompleted?.Invoke(null, 0, false, e.Exception);
        }

        public void Dispose()
        {
            _engine.SpeechRecognized -= OnSpeechRecognized;
            _engine.RecognizeCompleted -= OnRecognitionCompleted;
            if (_capture != null)
            {
                _capture.DataAvailable -= OnDataAvailable;
                _capture.RecordingStopped -= OnRecordingStopped;
            }
            try { _capture?.StopRecording(); } catch { }
            _capture?.Dispose();
            _capture = null;
            // Unblock a pending stream read before disposing SAPI.
            _stream?.Complete();
            try { _engine.RecognizeAsyncCancel(); } catch { }
            _engine.Dispose();
            _stream?.Dispose();
            _stream = null;
        }
    }
}

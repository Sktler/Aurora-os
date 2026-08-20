using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Speech.Recognition;
using System.Speech.Synthesis;

namespace ZoeyOS.App.Services
{
    public record VoiceOption(string Name, string Gender, string Culture)
    {
        public string DisplayName => $"{Name} ({Gender}, {Culture})";
    }

    /// <summary>
    /// Wraps Windows' built-in speech recognition (STT) and speech synthesis (TTS).
    /// Fully local/offline and free - no API key, no cloud service, just whatever
    /// speech components already ship with Windows.
    /// </summary>
    public class VoiceService : IDisposable
    {
        private readonly SpeechSynthesizer? _synth;
        private readonly bool _synthAvailable;
        private SpeechRecognitionEngine? _continuousRecognizer;
        private Action? _onStoppedExternally;

        public VoiceService(string? preferredVoiceName = null)
        {
            try
            {
                _synth = new SpeechSynthesizer();
                _synth.SetOutputToDefaultAudioDevice();
                _synthAvailable = true;

                if (!string.IsNullOrWhiteSpace(preferredVoiceName))
                    SelectVoice(preferredVoiceName);
            }
            catch
            {
                // No speech components installed / no audio device - voice features quietly
                // stay unavailable instead of crashing the app.
                _synthAvailable = false;
            }
        }

        public bool CanSpeak => _synthAvailable;

        /// <summary>Every voice Windows currently has installed - typically at least one
        /// male and one female voice (e.g. "Microsoft David", "Microsoft Zira"), but this
        /// varies by machine and language packs installed.</summary>
        public List<VoiceOption> GetAvailableVoices()
        {
            if (!_synthAvailable) return new List<VoiceOption>();

            try
            {
                return _synth!.GetInstalledVoices()
                    .Where(v => v.Enabled)
                    .Select(v => new VoiceOption(v.VoiceInfo.Name, v.VoiceInfo.Gender.ToString(), v.VoiceInfo.Culture.Name))
                    .ToList();
            }
            catch
            {
                return new List<VoiceOption>();
            }
        }

        /// <summary>Switches the active voice by its exact installed name (from GetAvailableVoices).
        /// Returns false if the name doesn't match anything installed.</summary>
        public bool SelectVoice(string voiceName)
        {
            if (!_synthAvailable || string.IsNullOrWhiteSpace(voiceName)) return false;
            try
            {
                _synth!.SelectVoice(voiceName);
                return true;
            }
            catch
            {
                // Unknown voice name (e.g. settings from a machine with different voices
                // installed) - just keep whatever voice was already selected.
                return false;
            }
        }

        public void Speak(string text)
        {
            if (!_synthAvailable || string.IsNullOrWhiteSpace(text)) return;
            try
            {
                _synth!.SpeakAsyncCancelAll();
                _synth.SpeakAsync(text);
            }
            catch
            {
                // A speech engine hiccup shouldn't take down a chat reply - just skip the audio.
            }
        }

        public void StopSpeaking()
        {
            try { _synth?.SpeakAsyncCancelAll(); } catch { /* ignore */ }
        }

        /// <summary>
        /// Starts hands-free continuous listening: stays on and fires
        /// <paramref name="onUtteranceRecognized"/> once per spoken phrase (silence marks
        /// the end of each one, same natural-pause detection dictation always used) until
        /// <see cref="StopContinuousListening"/> is called. Only one continuous listener can
        /// run at a time (one physical microphone) - starting a new one automatically stops
        /// and cleanly hands off from whichever companion had it running, notifying that
        /// companion via <paramref name="onStoppedByAnotherListener"/> so its own mic button
        /// can flip back off in the UI. Returns false if no recognizer/microphone is available.
        /// </summary>
        public bool StartContinuousListening(Action<string> onUtteranceRecognized, Action onStoppedByAnotherListener)
        {
            if (_continuousRecognizer != null)
            {
                var previousNotify = _onStoppedExternally;
                StopContinuousListeningInternal();
                previousNotify?.Invoke();
            }

            try
            {
                var recognizer = new SpeechRecognitionEngine(CultureInfo.CurrentCulture);
                recognizer.SetInputToDefaultAudioDevice();
                recognizer.LoadGrammar(new DictationGrammar());
                recognizer.SpeechRecognized += (s, e) =>
                {
                    var text = e.Result?.Text;
                    if (!string.IsNullOrWhiteSpace(text))
                        onUtteranceRecognized(text);
                    // SpeechRecognitionRejected (mumbled/unintelligible audio) is deliberately
                    // ignored here rather than surfaced - in continuous mode that's just
                    // background noise or a false start, not something worth interrupting for.
                };
                recognizer.RecognizeAsync(RecognizeMode.Multiple);

                _continuousRecognizer = recognizer;
                _onStoppedExternally = onStoppedByAnotherListener;
                return true;
            }
            catch
            {
                // No recognizer installed for this culture, or no microphone available.
                _continuousRecognizer = null;
                _onStoppedExternally = null;
                return false;
            }
        }

        /// <summary>Turns off continuous listening. Safe to call even if it isn't running.</summary>
        public void StopContinuousListening()
        {
            _onStoppedExternally = null; // deliberate stop - the caller already knows, no notification needed
            StopContinuousListeningInternal();
        }

        private void StopContinuousListeningInternal()
        {
            if (_continuousRecognizer == null) return;
            var recognizer = _continuousRecognizer;
            _continuousRecognizer = null;
            try { recognizer.RecognizeAsyncStop(); } catch { /* ignore */ }
            recognizer.Dispose();
        }

        public void Dispose()
        {
            StopContinuousListening();
            _synth?.Dispose();
        }
    }
}

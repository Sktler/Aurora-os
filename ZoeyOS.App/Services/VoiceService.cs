using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Speech.Recognition;
using System.Speech.Synthesis;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ZoeyOS.App.Services
{
    public record VoiceOption(string Name, string Gender, string Culture)
    {
        public string DisplayName => $"{Name} ({Gender}, {Culture})";
    }

    /// <summary>An ElevenLabs voice, fetched live from their /v1/voices endpoint - never a
    /// fixed list, since ElevenLabs' catalog includes account-specific cloned/custom voices
    /// that no static list could ever cover.</summary>
    public record ElevenLabsVoiceOption(string VoiceId, string Name);

    /// <summary>An Azure Neural voice, fetched live from the region-specific voices/list
    /// endpoint - Azure's catalog is large (100+ languages) and changes as Microsoft adds
    /// new ones, so this is never hard-coded either.</summary>
    public record AzureVoiceOption(string ShortName, string Gender, string Locale)
    {
        public string DisplayName => $"{ShortName} ({Gender}, {Locale})";
    }

    /// <summary>
    /// Wraps speech recognition (STT, always local Windows SAPI - offline, free, no key)
    /// and speech synthesis (TTS, which can run through OpenAI, ElevenLabs, or Azure Speech
    /// for a much bigger voice selection than Windows ships with, or fall back to the local
    /// Windows voice - controlled by AppSettings.TtsProvider, read fresh on every Speak call
    /// so a provider change in Settings takes effect on the very next reply, no restart).
    /// Whichever cloud provider is picked, a missing key or a failed call falls back to the
    /// local Windows voice automatically, so a reply is never silently unspoken.
    /// </summary>
    public class VoiceService : IDisposable
    {
        private readonly SpeechSynthesizer? _synth;
        private readonly bool _synthAvailable;
        private readonly HttpClient _http = new();
        private SpeechRecognitionEngine? _continuousRecognizer;
        private Action? _onStoppedExternally;

        public event Action<bool>? SpeakingChanged;

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

        /// <summary>Fire-and-forget entry point used by chat replies - same call shape as
        /// before, now routed through whichever provider AppSettings.TtsProvider names.</summary>
        public void Speak(string text) => _ = SpeakAsync(text);

        /// <summary>Speaks through the configured provider, falling back to the local
        /// Windows voice on any failure (missing/invalid key, network error, rate limit) so
        /// a reply is never silently unspoken.</summary>
        public async Task SpeakAsync(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;

            SpeakingChanged?.Invoke(true);
            var provider = App.Settings.TtsProvider;
            try
            {
                switch (provider)
                {
                    case "openai": await SpeakOpenAiAsync(text); break;
                    case "elevenlabs": await SpeakElevenLabsAsync(text); break;
                    case "azure": await SpeakAzureAsync(text); break;
                    default: await SpeakWindowsAsync(text); break; // "windows" or unrecognized
                }
            }
            catch
            {
                // Cloud call failed - fall back to the local voice rather than stay silent.
                await SpeakWindowsAsync(text);
            }
            finally
            {
                SpeakingChanged?.Invoke(false);
            }
        }

        private async Task SpeakWindowsAsync(string text)
        {
            if (!_synthAvailable || string.IsNullOrWhiteSpace(text)) return;

            var completed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            EventHandler<SpeakCompletedEventArgs>? handler = null;
            handler = (_, _) => completed.TrySetResult(true);
            try
            {
                _synth!.SpeakCompleted += handler;
                _synth!.SpeakAsyncCancelAll();
                _synth.SpeakAsync(text);
                await completed.Task;
            }
            catch
            {
                // A speech engine hiccup shouldn't take down a chat reply - just skip the audio.
            }
            finally
            {
                _synth!.SpeakCompleted -= handler;
            }
        }

        private async Task SpeakOpenAiAsync(string text)
        {
            // Falls back to the chat-provider OpenAI key if a separate TTS key was never
            // entered - a nice-to-have for anyone already using OpenAI for chat, without
            // forcing them to paste the same key twice.
            var key = string.IsNullOrWhiteSpace(App.Settings.OpenAiTtsApiKey)
                ? App.Settings.OpenAIApiKey
                : App.Settings.OpenAiTtsApiKey;
            if (string.IsNullOrWhiteSpace(key)) { await SpeakWindowsAsync(text); return; }

            var voice = string.IsNullOrWhiteSpace(App.Settings.OpenAiTtsVoice) ? "alloy" : App.Settings.OpenAiTtsVoice;
            var payload = JsonSerializer.Serialize(new { model = "tts-1", input = text, voice, response_format = "wav" });

            using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/audio/speech");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
            req.Content = new StringContent(payload, Encoding.UTF8, "application/json");

            using var response = await _http.SendAsync(req);
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"OpenAI TTS returned {(int)response.StatusCode}");

            PlayWav(await response.Content.ReadAsByteArrayAsync());
        }

        private async Task SpeakElevenLabsAsync(string text)
        {
            var key = App.Settings.ElevenLabsApiKey;
            var voiceId = App.Settings.ElevenLabsVoiceId;
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(voiceId)) { await SpeakWindowsAsync(text); return; }

            var payload = JsonSerializer.Serialize(new { text, model_id = "eleven_multilingual_v2" });
            using var req = new HttpRequestMessage(HttpMethod.Post,
                $"https://api.elevenlabs.io/v1/text-to-speech/{voiceId}?output_format=pcm_24000");
            req.Headers.Add("xi-api-key", key);
            req.Content = new StringContent(payload, Encoding.UTF8, "application/json");

            using var response = await _http.SendAsync(req);
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"ElevenLabs returned {(int)response.StatusCode}");

            // ElevenLabs returns raw headerless PCM at this output_format - wrap it in a
            // WAV header ourselves so it can play through the same SoundPlayer as the others.
            var pcm = await response.Content.ReadAsByteArrayAsync();
            PlayWav(WrapPcmAsWav(pcm, sampleRate: 24000, bitsPerSample: 16, channels: 1));
        }

        private async Task SpeakAzureAsync(string text)
        {
            var key = App.Settings.AzureSpeechKey;
            var region = App.Settings.AzureSpeechRegion;
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(region)) { await SpeakWindowsAsync(text); return; }

            var voiceName = string.IsNullOrWhiteSpace(App.Settings.AzureVoiceName) ? "en-US-JennyNeural" : App.Settings.AzureVoiceName;
            var ssml = "<speak version='1.0' xml:lang='en-US'><voice name='" + voiceName + "'>" +
                       System.Security.SecurityElement.Escape(text) + "</voice></speak>";

            using var req = new HttpRequestMessage(HttpMethod.Post, $"https://{region}.tts.speech.microsoft.com/cognitiveservices/v1");
            req.Headers.Add("Ocp-Apim-Subscription-Key", key);
            req.Headers.Add("X-Microsoft-OutputFormat", "riff-24khz-16bit-mono-pcm");
            req.Content = new StringContent(ssml, Encoding.UTF8, "application/ssml+xml");

            using var response = await _http.SendAsync(req);
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Azure Speech returned {(int)response.StatusCode}");

            // Azure's riff-* output formats are already a complete WAV file, unlike ElevenLabs'
            // raw PCM option above - no header-wrapping needed here.
            PlayWav(await response.Content.ReadAsByteArrayAsync());
        }

        /// <summary>Live voice catalog from ElevenLabs' own account - includes any custom or
        /// cloned voices, which no static list could ever cover.</summary>
        public async Task<List<ElevenLabsVoiceOption>> ListElevenLabsVoicesAsync(string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey)) return new List<ElevenLabsVoiceOption>();

            using var req = new HttpRequestMessage(HttpMethod.Get, "https://api.elevenlabs.io/v1/voices");
            req.Headers.Add("xi-api-key", apiKey);

            using var response = await _http.SendAsync(req);
            var text = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"ElevenLabs returned {(int)response.StatusCode}: {text}");

            using var doc = JsonDocument.Parse(text);
            var result = new List<ElevenLabsVoiceOption>();
            if (doc.RootElement.TryGetProperty("voices", out var voices))
            {
                foreach (var v in voices.EnumerateArray())
                {
                    var id = v.TryGetProperty("voice_id", out var idEl) ? idEl.GetString() ?? "" : "";
                    var name = v.TryGetProperty("name", out var nameEl) ? nameEl.GetString() ?? "" : "";
                    if (!string.IsNullOrWhiteSpace(id)) result.Add(new ElevenLabsVoiceOption(id, name));
                }
            }
            return result.OrderBy(v => v.Name, StringComparer.OrdinalIgnoreCase).ToList();
        }

        /// <summary>Live voice catalog for this Azure Speech region - Azure's list runs to
        /// hundreds of neural voices across languages and is updated on Microsoft's own
        /// schedule, so this is fetched fresh rather than baked into the app.</summary>
        public async Task<List<AzureVoiceOption>> ListAzureVoicesAsync(string apiKey, string region)
        {
            if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(region))
                return new List<AzureVoiceOption>();

            using var req = new HttpRequestMessage(HttpMethod.Get,
                $"https://{region}.tts.speech.microsoft.com/cognitiveservices/voices/list");
            req.Headers.Add("Ocp-Apim-Subscription-Key", apiKey);

            using var response = await _http.SendAsync(req);
            var text = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Azure Speech returned {(int)response.StatusCode}: {text}");

            using var doc = JsonDocument.Parse(text);
            var result = new List<AzureVoiceOption>();
            foreach (var v in doc.RootElement.EnumerateArray())
            {
                var shortName = v.TryGetProperty("ShortName", out var sn) ? sn.GetString() ?? "" : "";
                var gender = v.TryGetProperty("Gender", out var g) ? g.GetString() ?? "" : "";
                var locale = v.TryGetProperty("Locale", out var l) ? l.GetString() ?? "" : "";
                if (!string.IsNullOrWhiteSpace(shortName)) result.Add(new AzureVoiceOption(shortName, gender, locale));
            }
            return result.OrderBy(v => v.Locale).ThenBy(v => v.ShortName).ToList();
        }

        private static void PlayWav(byte[] wavBytes)
        {
            var stream = new MemoryStream(wavBytes);
            var player = new System.Media.SoundPlayer(stream);
            player.Load(); // reads the full stream into the player synchronously first...
            player.PlaySync();
        }

        /// <summary>Wraps headerless raw PCM in a standard 44-byte WAV header, since
        /// System.Media.SoundPlayer only understands complete WAV files, not raw PCM -
        /// needed for ElevenLabs' pcm_24000 output format specifically.</summary>
        private static byte[] WrapPcmAsWav(byte[] pcmData, int sampleRate, short bitsPerSample, short channels)
        {
            int byteRate = sampleRate * channels * bitsPerSample / 8;
            short blockAlign = (short)(channels * bitsPerSample / 8);

            using var ms = new MemoryStream();
            using (var writer = new BinaryWriter(ms, Encoding.ASCII, leaveOpen: true))
            {
                writer.Write(Encoding.ASCII.GetBytes("RIFF"));
                writer.Write(36 + pcmData.Length);
                writer.Write(Encoding.ASCII.GetBytes("WAVE"));
                writer.Write(Encoding.ASCII.GetBytes("fmt "));
                writer.Write(16);
                writer.Write((short)1); // PCM
                writer.Write(channels);
                writer.Write(sampleRate);
                writer.Write(byteRate);
                writer.Write(blockAlign);
                writer.Write(bitsPerSample);
                writer.Write(Encoding.ASCII.GetBytes("data"));
                writer.Write(pcmData.Length);
                writer.Write(pcmData);
            }
            return ms.ToArray();
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
            _http.Dispose();
        }
    }
}

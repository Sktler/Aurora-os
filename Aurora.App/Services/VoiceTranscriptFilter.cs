using System;

namespace Aurora.App.Services
{
    /// <summary>A single recognized utterance exactly as SAPI reported it, before any
    /// normalization. Kept purely for debugging voice-recognition quality - the text that is
    /// ever sent to the AI is always the separately normalized/cleaned phrase, never this raw
    /// one, and only when <see cref="Accepted"/> is true.</summary>
    public sealed record TranscriptEntry(string RawText, double Confidence, bool Accepted, DateTime TimestampUtc);

    /// <summary>Decides whether a recognized utterance is confident enough to act on at all.
    /// Kept as a small pure function (rather than inline in the recognition callback) so the
    /// threshold behavior is unit-testable without needing a real microphone/recognizer.</summary>
    public static class VoiceTranscriptFilter
    {
        /// <summary>Starting point requested for Aurora's voice pipeline: reject anything the
        /// recognizer itself is less than 70% confident about rather than forwarding a
        /// low-confidence guess to the AI. Adjustable via AppSettings.VoiceConfidenceThreshold.</summary>
        public const double DefaultConfidenceThreshold = 0.70;

        public static bool MeetsConfidenceThreshold(double confidence, double threshold) =>
            confidence >= threshold;
    }
}

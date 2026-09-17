using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;

namespace Aurora.App.Services
{
    /// <summary>
    /// Prepares a reply's text for natural-sounding speech without ever changing what it
    /// actually says. Everything here only adds delivery hints - sentence-sized chunks,
    /// brief pauses at commas, and a pace derived from the user's chosen words-per-minute
    /// target (see AppSettings.SpeechPaceWpm) - it never rewords, summarizes, or "corrects"
    /// the text itself.
    /// </summary>
    public static class SpeechFormatter
    {
        /// <summary>Default target: the midpoint of a natural 150-170 wpm conversational pace.
        /// Used as AppSettings.SpeechPaceWpm's default and clamped range center.</summary>
        public const int TargetWordsPerMinute = 160;

        public const int MinWordsPerMinute = 90;
        public const int MaxWordsPerMinute = 220;

        /// <summary>Windows' default voices speak at roughly this pace at SpeechSynthesizer's
        /// neutral Rate=0 - used as the zero point when converting a wpm target into SAPI's
        /// -10..10 Rate scale (there's no documented wpm formula, so this is an approximation
        /// tuned to land near the requested 150-170 wpm range at Rate=-2).</summary>
        private const double WindowsBaselineWpm = 180;
        private const double WindowsWpmPerRateStep = 9;

        /// <summary>Azure's neural voices default to roughly this pace before any &lt;prosody
        /// rate&gt; adjustment - used to convert a wpm target into Azure's percentage-based
        /// prosody rate string.</summary>
        private const double AzureBaselineWpm = 175;

        /// <summary>OpenAI's TTS "speed" parameter is 1.0 at roughly this pace - used to
        /// convert a wpm target into OpenAI's 0.25-4.0 speed multiplier.</summary>
        private const double OpenAiBaselineWpm = 175;

        private static readonly Regex SentenceBoundary = new(@"(?<=[.!?])\s+", RegexOptions.Compiled);

        /// <summary>Keeps a caller-supplied wpm target inside a sane, still-intelligible range.</summary>
        public static double ClampWordsPerMinute(double wpm) =>
            Math.Clamp(wpm, MinWordsPerMinute, MaxWordsPerMinute);

        /// <summary>Converts a words-per-minute target into System.Speech's -10..10 Rate scale.</summary>
        public static int WindowsSapiRateFor(double wpm)
        {
            var rate = (ClampWordsPerMinute(wpm) - WindowsBaselineWpm) / WindowsWpmPerRateStep;
            return (int)Math.Round(Math.Clamp(rate, -10, 10), MidpointRounding.AwayFromZero);
        }

        /// <summary>Converts a words-per-minute target into Azure SSML's percentage-based
        /// &lt;prosody rate&gt; value, e.g. "-10%" or "+5%".</summary>
        public static string AzureProsodyRateFor(double wpm)
        {
            var percent = (ClampWordsPerMinute(wpm) / AzureBaselineWpm - 1.0) * 100.0;
            var rounded = (int)Math.Round(percent, MidpointRounding.AwayFromZero);
            var sign = rounded >= 0 ? "+" : "";
            return $"{sign}{rounded}%";
        }

        /// <summary>Converts a words-per-minute target into OpenAI TTS's 0.25-4.0 speed multiplier.</summary>
        public static double OpenAiSpeechSpeedFor(double wpm)
        {
            var speed = ClampWordsPerMinute(wpm) / OpenAiBaselineWpm;
            return Math.Round(Math.Clamp(speed, 0.25, 4.0), 2);
        }

        /// <summary>Splits text into sentence-sized chunks, purely so callers can pace/pause
        /// between them - the text of each sentence comes back completely untouched.</summary>
        public static List<string> SplitIntoSentences(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return new List<string>();

            var sentences = SentenceBoundary.Split(text.Trim())
                .Select(s => s.Trim())
                .Where(s => s.Length > 0)
                .ToList();

            return sentences.Count > 0 ? sentences : new List<string> { text.Trim() };
        }

        /// <summary>Wraps text in SSML with a words-per-minute-derived prosody rate and short
        /// breaks after commas/semicolons (and a slightly longer one between sentences) - the
        /// words themselves are only XML-escaped, never altered, reworded, or corrected.</summary>
        public static string BuildAzureSsml(string text, string voiceName, double wpm)
        {
            var sentences = SplitIntoSentences(text);
            var body = new StringBuilder();
            for (var i = 0; i < sentences.Count; i++)
            {
                AppendSentenceWithPauses(body, sentences[i]);
                if (i < sentences.Count - 1)
                    body.Append("<break time=\"350ms\"/>");
            }

            return "<speak version='1.0' xml:lang='en-US'><voice name='" + SecurityElement.Escape(voiceName) + "'>" +
                   "<prosody rate='" + AzureProsodyRateFor(wpm) + "'>" + body + "</prosody></voice></speak>";
        }

        private static void AppendSentenceWithPauses(StringBuilder body, string sentence)
        {
            var clauses = sentence.Split(',');
            for (var i = 0; i < clauses.Length; i++)
            {
                body.Append(SecurityElement.Escape(clauses[i]));
                if (i < clauses.Length - 1)
                    body.Append(",<break time=\"150ms\"/>");
            }
        }
    }
}


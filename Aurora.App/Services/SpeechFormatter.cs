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
    /// brief pauses at commas, and a slightly slower pace aimed at a natural 150-170
    /// words-per-minute conversational speed - it never rewords, summarizes, or
    /// "corrects" the text itself.
    /// </summary>
    public static class SpeechFormatter
    {
        /// <summary>Midpoint of the requested 150-170 wpm natural-pacing target.</summary>
        public const int TargetWordsPerMinute = 160;

        /// <summary>System.Speech's SpeechSynthesizer.Rate has no documented words-per-minute
        /// formula (it's a -10..10 scale); Windows' default voices speak noticeably faster
        /// than natural conversational pace at Rate=0, so a small negative nudge brings them
        /// toward the 150-170 wpm target without sounding sluggish.</summary>
        public const int WindowsSapiRate = -2;

        /// <summary>Azure's neural voices default to a brisk pace; slowing by 10% brings them
        /// down toward the natural 150-170 wpm target while keeping the voice's own prosody.</summary>
        public const string AzureProsodyRate = "-10%";

        /// <summary>OpenAI's TTS "speed" parameter (0.25-4.0, default 1.0, normal pace); a
        /// slight slowdown reads as more natural/conversational than the default.</summary>
        public const double OpenAiSpeechSpeed = 0.92;

        private static readonly Regex SentenceBoundary = new(@"(?<=[.!?])\s+", RegexOptions.Compiled);

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

        /// <summary>Wraps text in SSML with a natural-pace prosody hint and short breaks
        /// after commas/semicolons (and a slightly longer one between sentences) - the words
        /// themselves are only XML-escaped, never altered, reworded, or corrected.</summary>
        public static string BuildAzureSsml(string text, string voiceName)
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
                   "<prosody rate='" + AzureProsodyRate + "'>" + body + "</prosody></voice></speak>";
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

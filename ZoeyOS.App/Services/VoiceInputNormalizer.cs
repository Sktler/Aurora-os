using System;
using System.Text;
using System.Text.RegularExpressions;

namespace ZoeyOS.App.Services
{
    /// <summary>Normalizes Windows speech-to-text output before Aurora sends it to the AI.
    /// This does not guess missing words; it removes transcription noise that commonly
    /// causes duplicate, fragmented, or hard-to-parse requests.</summary>
    public static class VoiceInputNormalizer
    {
        private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled);
        private static readonly Regex RepeatedPunctuation = new(@"([!?.,])\1{2,}", RegexOptions.Compiled);
        private static readonly Regex BrokenSpacing = new(@"\s+([,.!?;:])", RegexOptions.Compiled);

        public static string Normalize(string? input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;

            var text = input.Normalize(NormalizationForm.FormKC).Trim();
            text = text.Replace("\u2018", "'").Replace("\u2019", "'")
                       .Replace("\u201c", "\"").Replace("\u201d", "\"")
                       .Replace("\u2013", "-").Replace("\u2014", "-");
            text = Whitespace.Replace(text, " ");
            text = RepeatedPunctuation.Replace(text, "$1");
            text = BrokenSpacing.Replace(text, "$1");
            return text.Trim();
        }

        public static bool IsUsable(string? input)
        {
            var text = Normalize(input);
            if (text.Length < 2) return false;

            foreach (var ch in text)
                if (char.IsLetterOrDigit(ch)) return true;

            return false;
        }

        public static bool IsLikelyDuplicate(string? current, string? previous)
        {
            var a = Normalize(current);
            var b = Normalize(previous);
            return a.Length > 0 && b.Length > 0 &&
                   string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        }
    }
}

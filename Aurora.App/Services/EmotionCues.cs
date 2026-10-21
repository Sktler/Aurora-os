using System;

namespace Aurora.App.Services
{
    public enum EmotionCue { Neutral, Positive, Curious, Empathetic, Excited }

    /// <summary>
    /// Derives a subtle presentation-only cue from a reply. It never alters reply text.
    /// </summary>
    public static class EmotionCues
    {
        public const string ModeOff = "off";
        public const string ModeReduced = "reduced";
        public const string ModeSubtle = "subtle";

        public static string NormalizeMode(string? mode) => (mode ?? "").Trim().ToLowerInvariant() switch
        {
            ModeOff => ModeOff,
            ModeReduced => ModeReduced,
            _ => ModeSubtle
        };

        public static EmotionCue Detect(string? reply, string? mode)
        {
            if (NormalizeMode(mode) == ModeOff || string.IsNullOrWhiteSpace(reply)) return EmotionCue.Neutral;
            try
            {
                var text = reply.Length > 2000 ? reply[..2000] : reply;
                if (ContainsAny(text, "sorry", "unfortunately", "i'm afraid", "that sounds hard", "i understand how")) return EmotionCue.Empathetic;
                if (ContainsAny(text, "congrat", "great news", "awesome", "wonderful", "amazing") || text.Contains("!!")) return EmotionCue.Excited;
                if (ContainsAny(text, "glad", "happy to", "great", "love that", "nice")) return EmotionCue.Positive;
                if (text.TrimEnd().EndsWith('?') || ContainsAny(text, "interesting", "curious", "i wonder")) return EmotionCue.Curious;
            }
            catch { }
            return EmotionCue.Neutral;
        }

        /// <summary>Glow opacity multiplier; reduced mode halves the visible effect.</summary>
        public static double Intensity(string? mode) => NormalizeMode(mode) switch
        {
            ModeOff => 0,
            ModeReduced => 0.4,
            _ => 1
        };

        public static string GlowColor(EmotionCue cue) => cue switch
        {
            EmotionCue.Positive => "#7FE8B0",
            EmotionCue.Curious => "#8FA8FF",
            EmotionCue.Empathetic => "#C9A0F0",
            EmotionCue.Excited => "#FFD27A",
            _ => "#4FD8E8"
        };

        private static bool ContainsAny(string text, params string[] needles)
        {
            foreach (var n in needles)
                if (text.Contains(n, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
    }
}

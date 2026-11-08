using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Aurora.App.Services
{
    /// <summary>Turns a bare keyword or short phrase ("weather", "next song", "what's the news")
    /// into an explicit request so the companion's tools are used without the user knowing
    /// exact command wording. Longer sentences are never rewritten - they pass through whole.</summary>
    public static class VoiceIntentExpander
    {
        private const int MaxShortPhraseWords = 4;

        private static readonly HashSet<string> Filler = new(StringComparer.OrdinalIgnoreCase)
        {
            "aurora", "please", "the", "a", "my", "me", "show", "tell", "give", "get", "check",
            "what", "whats", "what's", "is", "are", "how", "hows", "how's", "now", "right", "today", "like", "can", "you", "i", "do", "any"
        };

        private static readonly (string[] Keywords, string Request)[] Intents =
        {
            (new[] { "weather alerts", "alerts", "warnings", "storm", "storms" },
                "Check for active weather alerts for my location and tell me about them."),
            (new[] { "weather", "forecast", "temperature", "temp", "outside", "rain", "raining", "snow", "cold", "hot" },
                "What's the current weather and short forecast for my location? Use get_weather."),
            (new[] { "news", "headlines" },
                "Search the web for today's top news headlines and summarize them briefly."),
            (new[] { "time", "clock" }, "What time is it right now?"),
            (new[] { "date", "day" }, "What is today's date and day of the week?"),
            (new[] { "play", "resume", "unpause" }, "Resume or start playing the current media."),
            (new[] { "pause", "stop music", "stop" }, "Pause the current media playback."),
            (new[] { "next", "skip", "next song", "next track" }, "Skip to the next media track."),
            (new[] { "previous", "back", "previous song", "last song", "previous track" }, "Go back to the previous media track."),
            (new[] { "now playing", "song", "music", "what song", "playing" }, "What media is currently playing?"),
            (new[] { "mute" }, "Mute the system audio."),
            (new[] { "unmute" }, "Unmute the system audio."),
            (new[] { "devices", "smart home", "lights", "home" }, "List my smart home devices and their current state."),
            (new[] { "camera", "webcam" }, "Open the camera preview."),
            (new[] { "screenshot", "screen" }, "Capture my screen."),
            (new[] { "clipboard" }, "Read what is on my clipboard."),
            (new[] { "documents", "files", "docs" }, "List my available documents."),
            (new[] { "apps", "applications", "programs" }, "List the applications currently running."),
            (new[] { "wifi", "wi-fi", "network", "internet" }, "Check my network and Wi-Fi status."),
            (new[] { "bluetooth" }, "List my Bluetooth devices."),
            (new[] { "undo" }, "Undo the last reversible action you took.")
        };

        public static string Expand(string? heard)
        {
            var text = VoiceInputNormalizer.Normalize(heard);
            if (text.Length == 0) return text;

            var phrase = Regex.Replace(text.ToLowerInvariant(), @"[^\p{L}\p{N}'\- ]", " ");
            var words = phrase.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0 || words.Length > MaxShortPhraseWords) return text;

            var core = words.Where(w => !Filler.Contains(w)).ToArray();
            if (core.Length == 0) return text;
            var coreText = string.Join(' ', core);

            foreach (var (keywords, request) in Intents)
            {
                if (keywords.Any(k => string.Equals(k, coreText, StringComparison.Ordinal))) return request;
            }

            // Partial phrases such as "weather tomorrow" keep the user's own words alongside the intent.
            foreach (var (keywords, request) in Intents)
            {
                if (core.Any(w => keywords.Contains(w, StringComparer.Ordinal)) && core.Length <= 2)
                    return $"{request} (The user said: \"{text}\")";
            }

            return text;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.Media.Control;

namespace ZoeyOS.App.Services
{
    public record NowPlayingInfo(string AppName, string Title, string Artist, string PlaybackStatus);

    /// <summary>
    /// Controls whatever's currently playing audio on this PC through Windows' System
    /// Media Transport Controls - the same system your keyboard's play/pause/next keys
    /// use. This covers Spotify, Windows Media Player, Apple Music, Pandora (in a
    /// browser tab), YouTube, or anything else that registers a media session, with
    /// zero setup and no per-app API keys - if the app shows up in Windows' media
    /// overlay (Win+Alt+B or the volume flyout), this can control it.
    /// </summary>
    public class MediaControlService
    {
        private async Task<GlobalSystemMediaTransportControlsSessionManager?> GetManagerAsync()
        {
            try
            {
                return await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            }
            catch
            {
                return null;
            }
        }

        public bool IsAvailable
        {
            get
            {
                try
                {
                    return Environment.OSVersion.Version >= new Version(10, 0, 17763, 0);
                }
                catch
                {
                    return false;
                }
            }
        }

        /// <summary>Lists every app currently registered with a media session, with what
        /// they're playing (if anything) - lets a companion pick the right one when more
        /// than one app is active.</summary>
        public async Task<List<NowPlayingInfo>> ListSessionsAsync()
        {
            var results = new List<NowPlayingInfo>();
            var manager = await GetManagerAsync();
            if (manager == null) return results;

            foreach (var session in manager.GetSessions())
            {
                try
                {
                    var props = await session.TryGetMediaPropertiesAsync();
                    var playback = session.GetPlaybackInfo()?.PlaybackStatus.ToString() ?? "Unknown";
                    results.Add(new NowPlayingInfo(
                        session.SourceAppUserModelId,
                        props?.Title ?? "",
                        props?.Artist ?? "",
                        playback));
                }
                catch
                {
                    // One misbehaving session shouldn't block listing the others.
                }
            }
            return results;
        }

        /// <summary>The system's "current" session - usually whichever app the user
        /// interacted with most recently.</summary>
        public async Task<NowPlayingInfo?> GetNowPlayingAsync()
        {
            var manager = await GetManagerAsync();
            var session = manager?.GetCurrentSession();
            if (session == null) return null;

            try
            {
                var props = await session.TryGetMediaPropertiesAsync();
                var playback = session.GetPlaybackInfo()?.PlaybackStatus.ToString() ?? "Unknown";
                return new NowPlayingInfo(session.SourceAppUserModelId, props?.Title ?? "", props?.Artist ?? "", playback);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>Sends a transport command to the current session: "play", "pause",
        /// "toggle", "next", "previous". Optionally target a specific app by matching
        /// (a substring of) its SourceAppUserModelId, e.g. "spotify".</summary>
        public async Task<(bool Success, string Message)> ControlAsync(string action, string? appHint = null)
        {
            var manager = await GetManagerAsync();
            if (manager == null)
                return (false, "Media control isn't available on this system.");

            var session = manager.GetCurrentSession();
            if (!string.IsNullOrWhiteSpace(appHint))
            {
                var match = manager.GetSessions()
                    .FirstOrDefault(s => s.SourceAppUserModelId.Contains(appHint, StringComparison.OrdinalIgnoreCase));
                if (match != null) session = match;
            }

            if (session == null)
                return (false, "Nothing is currently playing that Windows can control.");

            try
            {
                var ok = action.ToLowerInvariant() switch
                {
                    "play" => await session.TryPlayAsync(),
                    "pause" => await session.TryPauseAsync(),
                    "toggle" => await session.TryTogglePlayPauseAsync(),
                    "next" => await session.TrySkipNextAsync(),
                    "previous" => await session.TrySkipPreviousAsync(),
                    _ => false
                };
                return ok
                    ? (true, $"Sent '{action}' to {session.SourceAppUserModelId}.")
                    : (false, $"{session.SourceAppUserModelId} didn't accept the '{action}' command.");
            }
            catch (Exception ex)
            {
                return (false, $"Media control error: {ex.Message}");
            }
        }
    }
}

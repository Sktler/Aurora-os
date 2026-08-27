using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.Media.Control;

namespace ZoeyOS.App.Services
{
    public record NowPlayingInfo(string AppName, string Title, string Artist, string PlaybackStatus);

    /// <summary>Windows System Media Transport Controls integration. This is intentionally
    /// app-agnostic: it can control any application that registers a Windows media session.</summary>
    public class MediaControlService
    {
        private async Task<GlobalSystemMediaTransportControlsSessionManager?> GetManagerAsync()
        {
            try { return await GlobalSystemMediaTransportControlsSessionManager.RequestAsync(); }
            catch { return null; }
        }

        public bool IsAvailable
        {
            get { try { return Environment.OSVersion.Version >= new Version(10, 0, 17763, 0); } catch { return false; } }
        }

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
                    results.Add(new NowPlayingInfo(session.SourceAppUserModelId, props?.Title ?? "", props?.Artist ?? "", playback));
                }
                catch { }
            }
            return results;
        }

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
            catch { return null; }
        }

        public async Task<(bool Success, string Message)> ControlAsync(string action, string? appHint = null)
        {
            var manager = await GetManagerAsync();
            if (manager == null) return (false, "Media control isn't available on this system.");

            var session = manager.GetCurrentSession();
            if (!string.IsNullOrWhiteSpace(appHint))
            {
                var match = manager.GetSessions().FirstOrDefault(s => s.SourceAppUserModelId.Contains(appHint, StringComparison.OrdinalIgnoreCase));
                if (match != null) session = match;
            }
            if (session == null) return (false, "Nothing is currently playing that Windows can control.");

            try
            {
                if (string.Equals(action, "previous", StringComparison.OrdinalIgnoreCase))
                    return await GoToPreviousTrackAsync(session);

                var ok = action.ToLowerInvariant() switch
                {
                    "play" => await session.TryPlayAsync(),
                    "pause" => await session.TryPauseAsync(),
                    "toggle" => await session.TryTogglePlayPauseAsync(),
                    "next" => await session.TrySkipNextAsync(),
                    _ => false
                };
                return ok ? (true, $"Sent '{action}' to {session.SourceAppUserModelId}.") : (false, $"{session.SourceAppUserModelId} didn't accept the '{action}' command.");
            }
            catch (Exception ex) { return (false, $"Media control error: {ex.Message}"); }
        }

        private static async Task<(bool Success, string Message)> GoToPreviousTrackAsync(GlobalSystemMediaTransportControlsSession session)
        {
            var before = await session.TryGetMediaPropertiesAsync();
            var beforeTitle = before?.Title ?? "";
            var beforeArtist = before?.Artist ?? "";
            var timelineBefore = session.GetTimelineProperties();

            var first = await session.TrySkipPreviousAsync();
            if (!first) return (false, $"{session.SourceAppUserModelId} didn't accept the 'previous' command.");

            // Many players interpret Previous as "restart this track" when playback is past
            // their restart threshold. The user-facing Aurora command means "previous track",
            // so if Windows reports that the same track is still at/near the beginning, issue
            // Previous once more. This preserves normal behavior while fixing the restart case.
            await Task.Delay(350);
            try
            {
                var after = await session.TryGetMediaPropertiesAsync();
                var timelineAfter = session.GetTimelineProperties();
                var sameTrack = string.Equals(after?.Title ?? "", beforeTitle, StringComparison.OrdinalIgnoreCase)
                                && string.Equals(after?.Artist ?? "", beforeArtist, StringComparison.OrdinalIgnoreCase);
                var restarted = timelineBefore != null && timelineAfter != null && timelineAfter.Position < TimeSpan.FromSeconds(3);
                if (sameTrack && restarted)
                {
                    var second = await session.TrySkipPreviousAsync();
                    if (!second) return (true, $"Restarted {session.SourceAppUserModelId}'s current track; the player did not expose a true previous-track operation.");
                    await Task.Delay(200);
                }
            }
            catch { }

            return (true, $"Moved to the previous track in {session.SourceAppUserModelId}.");
        }
    }
}

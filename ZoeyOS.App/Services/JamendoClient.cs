using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Media;

namespace ZoeyOS.App.Services
{
    public record JamendoTrack(string Id, string Name, string Artist, string Album, string AudioUrl, string ImageUrl, int DurationSeconds);
    public record JamendoNowPlaying(bool Found, bool IsPlaying, string TrackName, string Artist);

    /// <summary>
    /// Jamendo music catalog and in-app streaming client.
    /// Jamendo's public API supplies stream URLs directly; no OAuth or Premium account is required.
    /// </summary>
    public sealed class JamendoClient : IDisposable
    {
        private readonly HttpClient _http = new();
        private readonly MediaPlayer _player = new();
        private readonly string _clientId;
        private readonly List<JamendoTrack> _queue = new();
        private int _queueIndex = -1;
        private bool _playWhenOpened;
        private bool _disposed;

        public JamendoTrack? CurrentTrack { get; private set; }
        public bool IsPlaying { get; private set; }
        public bool IsConfigured => !string.IsNullOrWhiteSpace(_clientId);

        public JamendoClient(string clientId)
        {
            _clientId = clientId?.Trim() ?? "";
            _player.MediaOpened += (_, _) =>
            {
                if (_playWhenOpened)
                {
                    _playWhenOpened = false;
                    _player.Play();
                    IsPlaying = true;
                }
            };
            _player.MediaEnded += (_, _) =>
            {
                IsPlaying = false;
                _ = PlayNextAsync();
            };
            _player.MediaFailed += (_, _) => IsPlaying = false;
        }

        public async Task<IReadOnlyList<JamendoTrack>> SearchAsync(string query, int limit = 10)
        {
            if (!IsConfigured) return Array.Empty<JamendoTrack>();
            if (string.IsNullOrWhiteSpace(query)) return Array.Empty<JamendoTrack>();

            var url = "https://api.jamendo.com/v3.0/tracks/?" +
                      $"client_id={Uri.EscapeDataString(_clientId)}" +
                      $"&format=json&limit={Math.Clamp(limit, 1, 50)}" +
                      $"&namesearch={Uri.EscapeDataString(query)}&audioformat=mp32&imagesize=300";

            try
            {
                var text = await _http.GetStringAsync(url);
                using var doc = JsonDocument.Parse(text);
                if (!doc.RootElement.TryGetProperty("results", out var results)) return Array.Empty<JamendoTrack>();

                return results.EnumerateArray()
                    .Select(ParseTrack)
                    .Where(t => t != null && !string.IsNullOrWhiteSpace(t.AudioUrl))
                    .Cast<JamendoTrack>()
                    .ToList();
            }
            catch
            {
                return Array.Empty<JamendoTrack>();
            }
        }

        public async Task<string> SearchAndPlayAsync(string query)
        {
            if (!IsConfigured) return "Jamendo isn't configured. Add your Jamendo Client ID in Aurora Settings.";
            var tracks = await SearchAsync(query, 10);
            if (tracks.Count == 0) return $"No Jamendo tracks found for \"{query}\".";

            _queue.Clear();
            _queue.AddRange(tracks);
            _queueIndex = 0;
            return await PlayTrackAsync(_queue[0]);
        }

        public async Task<string> PlayNextAsync()
        {
            if (_queue.Count == 0) return "There is no Jamendo queue yet.";
            if (_queueIndex + 1 >= _queue.Count) _queueIndex = 0;
            else _queueIndex++;
            return await PlayTrackAsync(_queue[_queueIndex]);
        }

        public async Task<string> PlayPreviousAsync()
        {
            if (_queue.Count == 0) return "There is no Jamendo queue yet.";
            if (_queueIndex <= 0) _queueIndex = _queue.Count - 1;
            else _queueIndex--;
            return await PlayTrackAsync(_queue[_queueIndex]);
        }

        private Task<string> PlayTrackAsync(JamendoTrack track)
        {
            if (string.IsNullOrWhiteSpace(track.AudioUrl))
                return Task.FromResult($"Found \"{track.Name}\" but Jamendo did not provide a stream URL.");

            CurrentTrack = track;
            IsPlaying = false;
            _playWhenOpened = true;
            try
            {
                _player.Open(new Uri(track.AudioUrl));
                return Task.FromResult($"Now playing \"{track.Name}\" by {track.Artist} on Jamendo.");
            }
            catch (Exception ex)
            {
                _playWhenOpened = false;
                return Task.FromResult($"Couldn't start \"{track.Name}\": {ex.Message}");
            }
        }

        public string Pause()
        {
            if (CurrentTrack == null) return "Nothing is playing on Jamendo.";
            _player.Pause();
            IsPlaying = false;
            return "Paused.";
        }

        public string Resume()
        {
            if (CurrentTrack == null) return "Nothing is loaded from Jamendo.";
            _player.Play();
            IsPlaying = true;
            return "Resumed.";
        }

        public JamendoNowPlaying GetNowPlaying() =>
            CurrentTrack == null
                ? new JamendoNowPlaying(false, false, "", "")
                : new JamendoNowPlaying(true, IsPlaying, CurrentTrack.Name, CurrentTrack.Artist);

        public string GetCurrentlyPlaying() => CurrentTrack == null
            ? "Nothing is currently playing on Jamendo."
            : $"{(IsPlaying ? "Playing" : "Paused")}: \"{CurrentTrack.Name}\" by {CurrentTrack.Artist}.";

        private static JamendoTrack? ParseTrack(JsonElement item)
        {
            string Get(string name) => item.TryGetProperty(name, out var p) ? p.GetString() ?? "" : "";
            var id = Get("id");
            if (string.IsNullOrWhiteSpace(id)) return null;
            var duration = 0;
            if (item.TryGetProperty("duration", out var d) && int.TryParse(d.GetString(), out var parsed)) duration = parsed;
            return new JamendoTrack(id, Get("name"), Get("artist_name"), Get("album_name"), Get("audio"), Get("image"), duration);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try { _player.Stop(); } catch { }
            _player.Close();
            _http.Dispose();
        }
    }
}

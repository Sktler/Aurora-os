using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ZoeyOS.App.Services
{
    /// <summary>
    /// Talks to the Spotify Web API. Holds a refresh token (persisted in settings) and
    /// exchanges it for a short-lived access token as needed - callers never deal with
    /// token expiry directly. Playback control endpoints (play/pause/skip) require an
    /// active Spotify device and, per Spotify's own restriction, a Premium account;
    /// free-account users can still read what's currently playing and search.
    /// </summary>
    /// <summary>Structured now-playing snapshot for the in-app player widget - the existing
    /// GetCurrentlyPlayingAsync returns a human-readable sentence for chat; this returns the
    /// same data as fields so the UI can bind to them directly instead of parsing text back apart.
    /// Named distinctly from MediaControlService's NowPlayingInfo (same namespace, different
    /// shape - that one covers any Windows media session, this one is Spotify-specific).</summary>
    public record SpotifyNowPlaying(bool Found, bool IsPlaying, string TrackName, string Artist);

    public class SpotifyClient
    {
        private readonly HttpClient _http = new();
        private readonly string _clientId;
        private string _refreshToken;
        private string? _accessToken;
        private DateTime _accessTokenExpiresUtc = DateTime.MinValue;

        /// <summary>Fired when a token refresh rotates the refresh token, so the caller can persist the new one.</summary>
        public event Action<string>? RefreshTokenRotated;

        public SpotifyClient(string clientId, string refreshToken)
        {
            _clientId = clientId ?? "";
            _refreshToken = refreshToken ?? "";
        }

        public bool IsConfigured => !string.IsNullOrWhiteSpace(_clientId) && !string.IsNullOrWhiteSpace(_refreshToken);

        private async Task<string?> GetValidAccessTokenAsync()
        {
            if (!IsConfigured) return null;
            if (_accessToken != null && DateTime.UtcNow < _accessTokenExpiresUtc) return _accessToken;

            var (success, accessToken, newRefreshToken, _) = await SpotifyAuthClient.RefreshAccessTokenAsync(_clientId, _refreshToken);
            if (!success || string.IsNullOrEmpty(accessToken)) return null;

            _accessToken = accessToken;
            _accessTokenExpiresUtc = DateTime.UtcNow.AddMinutes(50); // Spotify tokens last 60 min - refresh a bit early

            if (!string.IsNullOrEmpty(newRefreshToken) && newRefreshToken != _refreshToken)
            {
                _refreshToken = newRefreshToken;
                RefreshTokenRotated?.Invoke(newRefreshToken);
            }

            return _accessToken;
        }

        private async Task<HttpRequestMessage?> AuthedRequestAsync(HttpMethod method, string url)
        {
            var token = await GetValidAccessTokenAsync();
            if (token == null) return null;

            var req = new HttpRequestMessage(method, url);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return req;
        }

        public async Task<SpotifyNowPlaying> GetNowPlayingAsync()
        {
            if (!IsConfigured) return new SpotifyNowPlaying(false, false, "", "");

            using var req = await AuthedRequestAsync(HttpMethod.Get, "https://api.spotify.com/v1/me/player/currently-playing");
            if (req == null) return new SpotifyNowPlaying(false, false, "", "");

            try
            {
                var response = await _http.SendAsync(req);
                if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
                    return new SpotifyNowPlaying(false, false, "", "");

                var text = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode) return new SpotifyNowPlaying(false, false, "", "");

                using var doc = JsonDocument.Parse(text);
                var root = doc.RootElement;
                var isPlaying = root.TryGetProperty("is_playing", out var ip) && ip.GetBoolean();
                var item = root.TryGetProperty("item", out var i) ? i : default;

                if (item.ValueKind != JsonValueKind.Object) return new SpotifyNowPlaying(false, false, "", "");

                var trackName = item.TryGetProperty("name", out var tn) ? (tn.GetString() ?? "") : "";
                var artists = "";
                if (item.TryGetProperty("artists", out var artistsEl))
                {
                    var names = new System.Collections.Generic.List<string>();
                    foreach (var a in artistsEl.EnumerateArray())
                        if (a.TryGetProperty("name", out var an)) names.Add(an.GetString() ?? "");
                    artists = string.Join(", ", names);
                }

                return new SpotifyNowPlaying(true, isPlaying, trackName, artists);
            }
            catch
            {
                return new SpotifyNowPlaying(false, false, "", "");
            }
        }

        public async Task<string> GetCurrentlyPlayingAsync()
        {
            if (!IsConfigured) return "Spotify isn't connected.";

            using var req = await AuthedRequestAsync(HttpMethod.Get, "https://api.spotify.com/v1/me/player/currently-playing");
            if (req == null) return "Couldn't get a valid Spotify session - try reconnecting from Settings.";

            try
            {
                var response = await _http.SendAsync(req);
                if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
                    return "Nothing is currently playing on Spotify.";

                var text = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                    return $"Spotify error ({(int)response.StatusCode}): {text}";

                using var doc = JsonDocument.Parse(text);
                var root = doc.RootElement;
                var isPlaying = root.TryGetProperty("is_playing", out var ip) && ip.GetBoolean();
                var item = root.TryGetProperty("item", out var i) ? i : default;

                if (item.ValueKind != JsonValueKind.Object) return "Nothing is currently playing on Spotify.";

                var trackName = item.TryGetProperty("name", out var tn) ? tn.GetString() : "Unknown track";
                var artists = "";
                if (item.TryGetProperty("artists", out var artistsEl))
                {
                    var names = new System.Collections.Generic.List<string>();
                    foreach (var a in artistsEl.EnumerateArray())
                        if (a.TryGetProperty("name", out var an)) names.Add(an.GetString() ?? "");
                    artists = string.Join(", ", names);
                }

                return $"{(isPlaying ? "Playing" : "Paused")}: \"{trackName}\" by {artists}.";
            }
            catch (Exception ex)
            {
                return $"Couldn't reach Spotify: {ex.Message}";
            }
        }

        public async Task<string> SearchAndPlayAsync(string query)
        {
            if (!IsConfigured) return "Spotify isn't connected.";
            if (string.IsNullOrWhiteSpace(query)) return "No search query given.";

            var searchUrl = $"https://api.spotify.com/v1/search?q={Uri.EscapeDataString(query)}&type=track&limit=1";
            using var searchReq = await AuthedRequestAsync(HttpMethod.Get, searchUrl);
            if (searchReq == null) return "Couldn't get a valid Spotify session - try reconnecting from Settings.";

            string trackUri, trackName, artists;
            try
            {
                var response = await _http.SendAsync(searchReq);
                var text = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode) return $"Spotify search error ({(int)response.StatusCode}): {text}";

                using var doc = JsonDocument.Parse(text);
                var items = doc.RootElement.GetProperty("tracks").GetProperty("items");
                if (items.GetArrayLength() == 0) return $"No tracks found for \"{query}\".";

                var track = items[0];
                trackUri = track.GetProperty("uri").GetString() ?? "";
                trackName = track.TryGetProperty("name", out var tn) ? tn.GetString() ?? "" : "";
                var names = new System.Collections.Generic.List<string>();
                if (track.TryGetProperty("artists", out var artistsEl))
                    foreach (var a in artistsEl.EnumerateArray())
                        if (a.TryGetProperty("name", out var an)) names.Add(an.GetString() ?? "");
                artists = string.Join(", ", names);
            }
            catch (Exception ex)
            {
                return $"Couldn't search Spotify: {ex.Message}";
            }

            if (string.IsNullOrEmpty(trackUri)) return $"Found a result for \"{query}\" but couldn't get its URI.";

            var playResult = await PlayUriAsync(trackUri);
            return playResult.Success
                ? $"Now playing \"{trackName}\" by {artists}."
                : $"Found \"{trackName}\" by {artists}, but couldn't start playback: {playResult.Error}";
        }

        private async Task<(bool Success, string? Error)> PlayUriAsync(string uri)
        {
            using var req = await AuthedRequestAsync(HttpMethod.Put, "https://api.spotify.com/v1/me/player/play");
            if (req == null) return (false, "no valid session");

            var body = JsonSerializer.Serialize(new { uris = new[] { uri } });
            req.Content = new StringContent(body, Encoding.UTF8, "application/json");

            return await SendPlaybackCommandAsync(req);
        }

        public async Task<string> PauseAsync()
        {
            using var req = await AuthedRequestAsync(HttpMethod.Put, "https://api.spotify.com/v1/me/player/pause");
            if (req == null) return "Couldn't get a valid Spotify session.";
            var (success, error) = await SendPlaybackCommandAsync(req);
            return success ? "Paused." : $"Couldn't pause: {error}";
        }

        public async Task<string> ResumeAsync()
        {
            using var req = await AuthedRequestAsync(HttpMethod.Put, "https://api.spotify.com/v1/me/player/play");
            if (req == null) return "Couldn't get a valid Spotify session.";
            var (success, error) = await SendPlaybackCommandAsync(req);
            return success ? "Resumed." : $"Couldn't resume: {error}";
        }

        public async Task<string> SkipNextAsync()
        {
            using var req = await AuthedRequestAsync(HttpMethod.Post, "https://api.spotify.com/v1/me/player/next");
            if (req == null) return "Couldn't get a valid Spotify session.";
            var (success, error) = await SendPlaybackCommandAsync(req);
            return success ? "Skipped to the next track." : $"Couldn't skip: {error}";
        }

        public async Task<string> SkipPreviousAsync()
        {
            using var req = await AuthedRequestAsync(HttpMethod.Post, "https://api.spotify.com/v1/me/player/previous");
            if (req == null) return "Couldn't get a valid Spotify session.";
            var (success, error) = await SendPlaybackCommandAsync(req);
            return success ? "Went back to the previous track." : $"Couldn't go back: {error}";
        }

        private async Task<(bool Success, string? Error)> SendPlaybackCommandAsync(HttpRequestMessage req)
        {
            try
            {
                var response = await _http.SendAsync(req);
                if (response.IsSuccessStatusCode) return (true, null);

                var text = await response.Content.ReadAsStringAsync();
                var hint = response.StatusCode == System.Net.HttpStatusCode.Forbidden
                    ? " (Spotify playback control needs an active Premium account and an open Spotify app playing on some device.)"
                    : response.StatusCode == System.Net.HttpStatusCode.NotFound
                        ? " (No active Spotify device found - open Spotify somewhere first.)"
                        : "";
                return (false, $"{(int)response.StatusCode} {text}{hint}");
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }
    }
}

using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ZoeyOS.App.Services
{
    public class SpotifyAuthResult
    {
        public bool Success { get; set; }
        public string? DisplayName { get; set; }
        public string? AccessToken { get; set; }
        public string? RefreshToken { get; set; }
        public string? Error { get; set; }
    }

    /// <summary>
    /// Spotify sign-in via Authorization Code + PKCE - the flow Spotify recommends for
    /// desktop/mobile apps specifically because it needs no client secret, just a Client ID.
    /// Uses a fixed loopback redirect (127.0.0.1:8888/callback) because Spotify requires an
    /// exact registered redirect URI, unlike some providers that allow any loopback port.
    /// </summary>
    public static class SpotifyAuthClient
    {
        public const string RedirectUri = "http://127.0.0.1:8888/callback";
        private const string Scopes = "user-read-currently-playing user-read-playback-state user-modify-playback-state";

        public static async Task<SpotifyAuthResult> ConnectAsync(string clientId)
        {
            if (string.IsNullOrWhiteSpace(clientId))
                return new SpotifyAuthResult { Success = false, Error = "Client ID is required." };

            var verifier = GenerateCodeVerifier();
            var challenge = GenerateCodeChallenge(verifier);
            var state = Guid.NewGuid().ToString("N");

            var authUrl = "https://accounts.spotify.com/authorize" +
                          $"?client_id={Uri.EscapeDataString(clientId)}" +
                          "&response_type=code" +
                          $"&redirect_uri={Uri.EscapeDataString(RedirectUri)}" +
                          $"&scope={Uri.EscapeDataString(Scopes)}" +
                          $"&state={state}" +
                          $"&code_challenge={challenge}" +
                          "&code_challenge_method=S256";

            using var listener = new HttpListener();
            listener.Prefixes.Add(RedirectUri.EndsWith("/") ? RedirectUri : RedirectUri + "/");
            try
            {
                listener.Start();
            }
            catch (Exception ex)
            {
                return new SpotifyAuthResult
                {
                    Success = false,
                    Error = $"Couldn't start local listener on port 8888 ({ex.Message}). " +
                            "Make sure nothing else is using that port and try again."
                };
            }

            try
            {
                Process.Start(new ProcessStartInfo { FileName = authUrl, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                listener.Stop();
                return new SpotifyAuthResult { Success = false, Error = $"Couldn't open the browser: {ex.Message}" };
            }

            HttpListenerContext ctx;
            try
            {
                var contextTask = listener.GetContextAsync();
                var timeoutTask = Task.Delay(TimeSpan.FromMinutes(3));
                var completed = await Task.WhenAny(contextTask, timeoutTask);
                if (completed == timeoutTask)
                {
                    listener.Stop();
                    return new SpotifyAuthResult { Success = false, Error = "Timed out waiting for sign-in (3 min)." };
                }
                ctx = await contextTask;
            }
            catch (Exception ex)
            {
                listener.Stop();
                return new SpotifyAuthResult { Success = false, Error = $"Listener error: {ex.Message}" };
            }

            var query = ctx.Request.QueryString;
            var code = query["code"];
            var returnedState = query["state"];
            var oauthError = query["error"];

            var success = oauthError == null && !string.IsNullOrEmpty(code) && returnedState == state;
            var html = success
                ? "<html><body style='font-family:sans-serif;padding:40px'><h2>Aurora connected to Spotify</h2>You can close this window and go back to Aurora.</body></html>"
                : "<html><body style='font-family:sans-serif;padding:40px'><h2>Sign-in didn't complete</h2>You can close this window and try again in Aurora.</body></html>";

            try
            {
                var buffer = Encoding.UTF8.GetBytes(html);
                ctx.Response.ContentType = "text/html";
                ctx.Response.ContentLength64 = buffer.Length;
                await ctx.Response.OutputStream.WriteAsync(buffer);
                ctx.Response.OutputStream.Close();
            }
            catch { /* the browser tab is just a courtesy */ }
            finally
            {
                listener.Stop();
            }

            if (oauthError != null)
                return new SpotifyAuthResult { Success = false, Error = $"Spotify returned an error: {oauthError}" };
            if (string.IsNullOrEmpty(code))
                return new SpotifyAuthResult { Success = false, Error = "No authorization code was returned." };
            if (returnedState != state)
                return new SpotifyAuthResult { Success = false, Error = "State mismatch - please try connecting again." };

            return await ExchangeCodeAsync(clientId, code, verifier);
        }

        private static async Task<SpotifyAuthResult> ExchangeCodeAsync(string clientId, string code, string verifier)
        {
            using var http = new HttpClient();
            var form = new System.Collections.Generic.Dictionary<string, string>
            {
                ["client_id"] = clientId,
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["redirect_uri"] = RedirectUri,
                ["code_verifier"] = verifier
            };

            string tokenText;
            HttpResponseMessage tokenResponse;
            try
            {
                tokenResponse = await http.PostAsync("https://accounts.spotify.com/api/token", new FormUrlEncodedContent(form));
                tokenText = await tokenResponse.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                return new SpotifyAuthResult { Success = false, Error = $"Token exchange failed: {ex.Message}" };
            }

            if (!tokenResponse.IsSuccessStatusCode)
                return new SpotifyAuthResult { Success = false, Error = $"Token exchange error: {tokenText}" };

            using var doc = JsonDocument.Parse(tokenText);
            var root = doc.RootElement;
            var accessToken = root.TryGetProperty("access_token", out var at) ? at.GetString() : null;
            var refreshToken = root.TryGetProperty("refresh_token", out var rt) ? rt.GetString() : null;

            if (string.IsNullOrEmpty(accessToken))
                return new SpotifyAuthResult { Success = false, Error = "No access token was returned." };

            string? displayName = null;
            try
            {
                using var profileReq = new HttpRequestMessage(HttpMethod.Get, "https://api.spotify.com/v1/me");
                profileReq.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
                var profileResponse = await http.SendAsync(profileReq);
                if (profileResponse.IsSuccessStatusCode)
                {
                    var profileText = await profileResponse.Content.ReadAsStringAsync();
                    using var profileDoc = JsonDocument.Parse(profileText);
                    displayName = profileDoc.RootElement.TryGetProperty("display_name", out var dn) ? dn.GetString() : null;
                }
            }
            catch { /* non-fatal */ }

            return new SpotifyAuthResult
            {
                Success = true,
                DisplayName = displayName,
                AccessToken = accessToken,
                RefreshToken = refreshToken
            };
        }

        /// <summary>Exchanges a stored refresh token for a new access token. No client secret needed - PKCE clients refresh with just the client_id.</summary>
        public static async Task<(bool Success, string? AccessToken, string? NewRefreshToken, string? Error)> RefreshAccessTokenAsync(string clientId, string refreshToken)
        {
            using var http = new HttpClient();
            var form = new System.Collections.Generic.Dictionary<string, string>
            {
                ["client_id"] = clientId,
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = refreshToken
            };

            try
            {
                var response = await http.PostAsync("https://accounts.spotify.com/api/token", new FormUrlEncodedContent(form));
                var text = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                    return (false, null, null, $"Refresh failed: {text}");

                using var doc = JsonDocument.Parse(text);
                var root = doc.RootElement;
                var accessToken = root.TryGetProperty("access_token", out var at) ? at.GetString() : null;
                // Spotify sometimes rotates the refresh token, sometimes doesn't - keep the old one if a new one isn't given.
                var newRefreshToken = root.TryGetProperty("refresh_token", out var rt) ? rt.GetString() : refreshToken;

                return string.IsNullOrEmpty(accessToken)
                    ? (false, null, null, "No access token returned on refresh.")
                    : (true, accessToken, newRefreshToken, null);
            }
            catch (Exception ex)
            {
                return (false, null, null, ex.Message);
            }
        }

        private static string GenerateCodeVerifier() => Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

        private static string GenerateCodeChallenge(string verifier)
        {
            var hash = SHA256.HashData(Encoding.ASCII.GetBytes(verifier));
            return Base64UrlEncode(hash);
        }

        private static string Base64UrlEncode(byte[] bytes) =>
            Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ZoeyOS.App.Services
{
    public class GoogleAuthResult
    {
        public bool Success { get; set; }
        public string? Email { get; set; }
        public string? RefreshToken { get; set; }
        public string? AccessToken { get; set; }
        public string? Error { get; set; }
    }

    /// <summary>
    /// Real Google OAuth 2.0 sign-in using the Authorization Code + PKCE flow with a
    /// loopback redirect - the standard pattern for installed desktop apps (the same
    /// one gcloud CLI and Google's own client libraries use). Opens the system browser,
    /// briefly listens on 127.0.0.1 for the redirect, then exchanges the code for tokens.
    ///
    /// Requires the user to have created their own OAuth client (Desktop app type) in
    /// Google Cloud Console - Aurora can't ship one on your behalf, since every installed
    /// app is supposed to register its own client with Google rather than share one.
    /// </summary>
    public static class GoogleAuthClient
    {
        private static readonly string[] Scopes =
        {
            "https://www.googleapis.com/auth/gmail.readonly",
            "https://www.googleapis.com/auth/drive.readonly",
            "openid",
            "email"
        };

        public static async Task<GoogleAuthResult> ConnectAsync(string clientId, string clientSecret)
        {
            if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
                return new GoogleAuthResult { Success = false, Error = "Client ID and Client Secret are both required." };

            var port = GetFreeLoopbackPort();
            var redirectUri = $"http://127.0.0.1:{port}/";
            var verifier = GenerateCodeVerifier();
            var challenge = GenerateCodeChallenge(verifier);
            var state = Guid.NewGuid().ToString("N");

            var scope = Uri.EscapeDataString(string.Join(" ", Scopes));
            var authUrl = "https://accounts.google.com/o/oauth2/v2/auth" +
                          $"?client_id={Uri.EscapeDataString(clientId)}" +
                          $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
                          "&response_type=code" +
                          $"&scope={scope}" +
                          $"&state={state}" +
                          "&access_type=offline" +
                          "&prompt=consent" +
                          $"&code_challenge={challenge}" +
                          "&code_challenge_method=S256";

            using var listener = new HttpListener();
            listener.Prefixes.Add(redirectUri);
            try
            {
                listener.Start();
            }
            catch (Exception ex)
            {
                return new GoogleAuthResult { Success = false, Error = $"Couldn't start local listener: {ex.Message}" };
            }

            try
            {
                Process.Start(new ProcessStartInfo { FileName = authUrl, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                listener.Stop();
                return new GoogleAuthResult { Success = false, Error = $"Couldn't open the browser: {ex.Message}" };
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
                    return new GoogleAuthResult { Success = false, Error = "Timed out waiting for sign-in (3 min)." };
                }
                ctx = await contextTask;
            }
            catch (Exception ex)
            {
                listener.Stop();
                return new GoogleAuthResult { Success = false, Error = $"Listener error: {ex.Message}" };
            }

            var query = ctx.Request.QueryString;
            var code = query["code"];
            var returnedState = query["state"];
            var oauthError = query["error"];

            var success = oauthError == null && !string.IsNullOrEmpty(code) && returnedState == state;
            var html = success
                ? "<html><body style='font-family:sans-serif;padding:40px'><h2>Aurora connected</h2>You can close this window and go back to Aurora.</body></html>"
                : "<html><body style='font-family:sans-serif;padding:40px'><h2>Sign-in didn't complete</h2>You can close this window and try again in Aurora.</body></html>";

            try
            {
                var buffer = Encoding.UTF8.GetBytes(html);
                ctx.Response.ContentType = "text/html";
                ctx.Response.ContentLength64 = buffer.Length;
                await ctx.Response.OutputStream.WriteAsync(buffer);
                ctx.Response.OutputStream.Close();
            }
            catch { /* the browser tab is just a courtesy - a failed write here doesn't matter */ }
            finally
            {
                listener.Stop();
            }

            if (oauthError != null)
                return new GoogleAuthResult { Success = false, Error = $"Google returned an error: {oauthError}" };
            if (string.IsNullOrEmpty(code))
                return new GoogleAuthResult { Success = false, Error = "No authorization code was returned." };
            if (returnedState != state)
                return new GoogleAuthResult { Success = false, Error = "State mismatch - please try connecting again." };

            return await ExchangeCodeAsync(clientId, clientSecret, code, redirectUri, verifier);
        }

        private static async Task<GoogleAuthResult> ExchangeCodeAsync(
            string clientId, string clientSecret, string code, string redirectUri, string verifier)
        {
            using var http = new HttpClient();
            var form = new Dictionary<string, string>
            {
                ["code"] = code,
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret,
                ["redirect_uri"] = redirectUri,
                ["grant_type"] = "authorization_code",
                ["code_verifier"] = verifier
            };

            string tokenText;
            HttpResponseMessage tokenResponse;
            try
            {
                tokenResponse = await http.PostAsync("https://oauth2.googleapis.com/token", new FormUrlEncodedContent(form));
                tokenText = await tokenResponse.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                return new GoogleAuthResult { Success = false, Error = $"Token exchange failed: {ex.Message}" };
            }

            if (!tokenResponse.IsSuccessStatusCode)
                return new GoogleAuthResult { Success = false, Error = $"Token exchange error: {tokenText}" };

            using var doc = JsonDocument.Parse(tokenText);
            var root = doc.RootElement;
            var accessToken = root.TryGetProperty("access_token", out var at) ? at.GetString() : null;
            var refreshToken = root.TryGetProperty("refresh_token", out var rt) ? rt.GetString() : null;

            if (string.IsNullOrEmpty(accessToken))
                return new GoogleAuthResult { Success = false, Error = "No access token was returned." };

            var email = await TryFetchEmailAsync(http, accessToken);

            return new GoogleAuthResult
            {
                Success = true,
                Email = email,
                AccessToken = accessToken,
                RefreshToken = refreshToken
            };
        }

        private static async Task<string?> TryFetchEmailAsync(HttpClient http, string accessToken)
        {
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, "https://www.googleapis.com/oauth2/v2/userinfo");
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                var response = await http.SendAsync(req);
                if (!response.IsSuccessStatusCode) return null;

                var text = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(text);
                return doc.RootElement.TryGetProperty("email", out var e) ? e.GetString() : null;
            }
            catch
            {
                // Non-fatal - the connection still succeeded even if we can't label it with an email.
                return null;
            }
        }

        private static int GetFreeLoopbackPort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
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

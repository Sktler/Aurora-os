using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

namespace ZoeyOS.App.Services
{
    public static class SystemTools
    {
        private static readonly MediaControlService Media = new();

        public static List<object> Definitions => new()
        {
            new { name = "get_weather", description = "Gets current real-time weather conditions for a place. Free, no setup needed.", input_schema = new { type = "object", properties = new { location = new { type = "string", description = "A place name." } }, required = new[] { "location" } } },
            new { name = "web_search", description = "Searches the web for a query.", input_schema = new { type = "object", properties = new { query = new { type = "string", description = "What to search for." } }, required = new[] { "query" } } },
            new { name = "get_now_playing", description = "Gets the music or other media currently playing on Windows, including the app, title, artist, and playback state.", input_schema = new { type = "object", properties = new { } } },
            new { name = "list_media_sessions", description = "Lists Windows media sessions registered by apps so Aurora can see what media players are available.", input_schema = new { type = "object", properties = new { } } },
            new { name = "media_play", description = "Starts playback in the current Windows media session. Optionally target an app such as Spotify or Windows Media Player.", input_schema = new { type = "object", properties = new { app = new { type = "string", description = "Optional app name or partial app identifier." } } } },
            new { name = "media_pause", description = "Pauses the current Windows media session. Optionally target an app.", input_schema = new { type = "object", properties = new { app = new { type = "string", description = "Optional app name or partial app identifier." } } } },
            new { name = "media_toggle_play_pause", description = "Toggles play/pause for the current Windows media session. Optionally target an app.", input_schema = new { type = "object", properties = new { app = new { type = "string", description = "Optional app name or partial app identifier." } } } },
            new { name = "media_next", description = "Skips to the next item in the current Windows media session. Optionally target an app.", input_schema = new { type = "object", properties = new { app = new { type = "string", description = "Optional app name or partial app identifier." } } } },
            new { name = "media_previous", description = "Goes to the previous item in the current Windows media session. Optionally target an app.", input_schema = new { type = "object", properties = new { app = new { type = "string", description = "Optional app name or partial app identifier." } } } },
            new { name = "jamendo_now_playing", description = "Gets what's currently playing from Aurora's Jamendo music player.", input_schema = new { type = "object", properties = new { } } },
            new { name = "jamendo_play", description = "Searches Jamendo's independent music catalog and plays a matching track directly in Aurora.", input_schema = new { type = "object", properties = new { query = new { type = "string", description = "Song, artist, genre, or music search." } }, required = new[] { "query" } } },
            new { name = "jamendo_pause", description = "Pauses Jamendo playback.", input_schema = new { type = "object", properties = new { } } },
            new { name = "jamendo_resume", description = "Resumes Jamendo playback.", input_schema = new { type = "object", properties = new { } } },
            new { name = "jamendo_skip_next", description = "Skips to the next track in the current Jamendo queue.", input_schema = new { type = "object", properties = new { } } },
            new { name = "jamendo_skip_previous", description = "Goes to the previous track in the current Jamendo queue.", input_schema = new { type = "object", properties = new { } } },
            new { name = "set_system_volume", description = "Sets the PC's master output volume (0-100).", input_schema = new { type = "object", properties = new { percent = new { type = "number", description = "Volume level, 0 to 100." } }, required = new[] { "percent" } } },
            new { name = "toggle_system_mute", description = "Mutes or unmutes the PC's system audio.", input_schema = new { type = "object", properties = new { mute = new { type = "boolean", description = "true to mute, false to unmute." } }, required = new[] { "mute" } } }
        };

        public static async Task<string> ExecuteAsync(string toolName, JsonElement input)
        {
            switch (toolName)
            {
                case "get_weather":
                    return await App.Weather.GetCurrentWeatherAsync(input.TryGetProperty("location", out var l) ? l.GetString() ?? "" : "");
                case "web_search":
                {
                    var query = input.TryGetProperty("query", out var q) ? q.GetString() ?? "" : "";
                    return await App.WebSearch.TryInstantAnswerAsync(query) ?? App.WebSearch.OpenSearchInBrowser(query);
                }
                case "get_now_playing":
                {
                    var info = await Media.GetNowPlayingAsync();
                    return info == null
                        ? "Nothing is currently playing in a Windows media session."
                        : $"App: {info.AppName}\nTitle: {info.Title}\nArtist: {info.Artist}\nPlayback: {info.PlaybackStatus}";
                }
                case "list_media_sessions":
                {
                    var sessions = await Media.ListSessionsAsync();
                    if (sessions.Count == 0) return "No Windows media sessions are currently registered.";
                    return string.Join("\n", sessions.ConvertAll(s => $"App: {s.AppName} | Title: {s.Title} | Artist: {s.Artist} | Playback: {s.PlaybackStatus}"));
                }
                case "media_play": return await ExecuteMediaControlAsync("play", input);
                case "media_pause": return await ExecuteMediaControlAsync("pause", input);
                case "media_toggle_play_pause": return await ExecuteMediaControlAsync("toggle", input);
                case "media_next": return await ExecuteMediaControlAsync("next", input);
                case "media_previous": return await ExecuteMediaControlAsync("previous", input);
                case "jamendo_now_playing": return App.Jamendo.GetCurrentlyPlaying();
                case "jamendo_play": return await App.Jamendo.SearchAndPlayAsync(input.TryGetProperty("query", out var jp) ? jp.GetString() ?? "" : "");
                case "jamendo_pause": return App.Jamendo.Pause();
                case "jamendo_resume": return App.Jamendo.Resume();
                case "jamendo_skip_next": return await App.Jamendo.PlayNextAsync();
                case "jamendo_skip_previous": return await App.Jamendo.PlayPreviousAsync();
                case "set_system_volume":
                {
                    var percent = input.TryGetProperty("percent", out var p) ? p.GetDouble() : -1;
                    if (percent < 0 || percent > 100) return "Give a volume between 0 and 100.";
                    SystemVolumeControl.SetVolume((float)(percent / 100.0));
                    return $"System volume set to {percent:0}%.";
                }
                case "toggle_system_mute":
                {
                    var mute = input.TryGetProperty("mute", out var m) && m.GetBoolean();
                    SystemVolumeControl.SetMute(mute);
                    return mute ? "System audio muted." : "System audio unmuted.";
                }
                default: return $"Unknown tool: {toolName}";
            }
        }

        private static async Task<string> ExecuteMediaControlAsync(string action, JsonElement input)
        {
            var appHint = input.TryGetProperty("app", out var app) ? app.GetString() : null;
            var result = await Media.ControlAsync(action, appHint);
            return result.Message;
        }
    }
}

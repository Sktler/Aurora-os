using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

namespace ZoeyOS.App.Services
{
    /// <summary>
    /// Tool definitions + execution for general-purpose companions (Aurora, Scout):
    /// real-time weather, web search, and Spotify. Kept separate from HomeTools, which
    /// is exclusive to the Home companion.
    /// </summary>
    public static class SystemTools
    {
        public static List<object> Definitions => new()
        {
            new
            {
                name = "get_weather",
                description = "Gets current real-time weather conditions for a place. Free, no setup needed.",
                input_schema = new
                {
                    type = "object",
                    properties = new
                    {
                        location = new { type = "string", description = "A place name, e.g. \"Chicago\" or \"Tokyo, Japan\"." }
                    },
                    required = new[] { "location" }
                }
            },
            new
            {
                name = "web_search",
                description = "Searches the web for a query. First tries a quick instant answer for simple facts; " +
                               "if that doesn't have a good answer, opens a full search results page in the user's browser instead.",
                input_schema = new
                {
                    type = "object",
                    properties = new
                    {
                        query = new { type = "string", description = "What to search for." }
                    },
                    required = new[] { "query" }
                }
            },
            new
            {
                name = "spotify_now_playing",
                description = "Gets what's currently playing on the user's Spotify, if connected.",
                input_schema = new { type = "object", properties = new { } }
            },
            new
            {
                name = "spotify_play",
                description = "Searches Spotify for a track and starts playing it. Requires Spotify Premium and an " +
                               "active device (Spotify open somewhere) to actually start playback.",
                input_schema = new
                {
                    type = "object",
                    properties = new
                    {
                        query = new { type = "string", description = "Song and/or artist to search for and play, e.g. \"Bohemian Rhapsody Queen\"." }
                    },
                    required = new[] { "query" }
                }
            },
            new
            {
                name = "spotify_pause",
                description = "Pauses Spotify playback. Requires Premium and an active device.",
                input_schema = new { type = "object", properties = new { } }
            },
            new
            {
                name = "spotify_resume",
                description = "Resumes Spotify playback. Requires Premium and an active device.",
                input_schema = new { type = "object", properties = new { } }
            },
            new
            {
                name = "spotify_skip_next",
                description = "Skips to the next track on Spotify. Requires Premium and an active device.",
                input_schema = new { type = "object", properties = new { } }
            },
            new
            {
                name = "spotify_skip_previous",
                description = "Goes back to the previous track on Spotify. Requires Premium and an active device.",
                input_schema = new { type = "object", properties = new { } }
            },
            new
            {
                name = "set_system_volume",
                description = "Sets the PC's master output volume (0-100).",
                input_schema = new
                {
                    type = "object",
                    properties = new
                    {
                        percent = new { type = "number", description = "Volume level, 0 to 100." }
                    },
                    required = new[] { "percent" }
                }
            },
            new
            {
                name = "toggle_system_mute",
                description = "Mutes or unmutes the PC's system audio.",
                input_schema = new
                {
                    type = "object",
                    properties = new
                    {
                        mute = new { type = "boolean", description = "true to mute, false to unmute." }
                    },
                    required = new[] { "mute" }
                }
            }
        };

        public static async Task<string> ExecuteAsync(string toolName, JsonElement input)
        {
            switch (toolName)
            {
                case "get_weather":
                {
                    var location = input.TryGetProperty("location", out var l) ? l.GetString() ?? "" : "";
                    return await App.Weather.GetCurrentWeatherAsync(location);
                }
                case "web_search":
                {
                    var query = input.TryGetProperty("query", out var q) ? q.GetString() ?? "" : "";
                    var instant = await App.WebSearch.TryInstantAnswerAsync(query);
                    return instant ?? App.WebSearch.OpenSearchInBrowser(query);
                }
                case "spotify_now_playing":
                    return await App.Spotify.GetCurrentlyPlayingAsync();
                case "spotify_play":
                {
                    var query = input.TryGetProperty("query", out var q) ? q.GetString() ?? "" : "";
                    return await App.Spotify.SearchAndPlayAsync(query);
                }
                case "spotify_pause":
                    return await App.Spotify.PauseAsync();
                case "spotify_resume":
                    return await App.Spotify.ResumeAsync();
                case "spotify_skip_next":
                    return await App.Spotify.SkipNextAsync();
                case "spotify_skip_previous":
                    return await App.Spotify.SkipPreviousAsync();
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
                default:
                    return $"Unknown tool: {toolName}";
            }
        }
    }
}

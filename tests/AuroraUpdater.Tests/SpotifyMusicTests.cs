using System.IO;
using Xunit;

namespace AuroraUpdater.Tests;

public sealed class SpotifyMusicTests
{
    [Fact]
    public void Spotify_client_surfaces_reconnectable_errors_for_auth_and_api_failures()
    {
        var repoRoot = FindRepositoryRoot();
        var code = File.ReadAllText(Path.Combine(repoRoot, "Aurora.App", "Services", "SpotifyClient.cs"));

        Assert.Contains("public string? LastError", code);
        Assert.Contains("Reconnect from Settings.", code);
        Assert.Contains("Couldn't read what's playing on Spotify", code);
        Assert.Contains("BuildApiError(\"control Spotify playback\"", code);
    }

    [Fact]
    public void Music_player_prefers_spotify_controls_and_clears_stale_track_state()
    {
        var repoRoot = FindRepositoryRoot();
        var code = File.ReadAllText(Path.Combine(repoRoot, "Aurora.App", "ViewModels", "MusicPlayerViewModel.cs"));

        Assert.Contains("private enum PlaybackSource", code);
        Assert.Contains("ApplySpotifyState(spotify)", code);
        Assert.Contains("ApplyIdleState()", code);
        Assert.Contains("TrySpotifyToggleAsync()", code);
        Assert.Contains("TrySpotifyCommandAsync(client => client.SkipNextAsync())", code);
        Assert.Contains("TrySpotifyCommandAsync(client => client.SkipPreviousAsync())", code);
        Assert.Contains("TrackName = \"\";", code);
        Assert.Contains("ArtistName = \"\";", code);
        Assert.Contains("SourceApp = \"\";", code);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Aurora.sln")) &&
                Directory.Exists(Path.Combine(directory.FullName, "Aurora.App")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the Aurora repository root.");
    }
}

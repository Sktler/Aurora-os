using System;
using System.Threading.Tasks;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Aurora.App.Services;

namespace Aurora.App.ViewModels
{
    public partial class MusicPlayerViewModel : ObservableObject
    {
        private enum PlaybackSource
        {
            None,
            Spotify,
            WindowsMedia
        }

        private readonly DispatcherTimer _pollTimer;
        private readonly MediaControlService _media = new();
        private PlaybackSource _playbackSource = PlaybackSource.None;

        [ObservableProperty] private bool _hasTrack;
        [ObservableProperty] private string _trackName = "";
        [ObservableProperty] private string _artistName = "";
        [ObservableProperty] private string _sourceApp = "";
        [ObservableProperty] private string _albumArtUrl = "";
        [ObservableProperty] private bool _isPlaying;
        [ObservableProperty] private bool _isBusy;
        [ObservableProperty] private string _statusText = Localizer.GetUiText("Checking what's playing...");

        // Spotify is initialized after MainWindow is shown, so this must remain safe
        // during dashboard construction and while the optional integration is offline.
        public bool IsConnected => _media.IsAvailable || App.Spotify?.IsConfigured == true;

        public MusicPlayerViewModel()
        {
            _pollTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _pollTimer.Tick += (_, _) => Refresh();
            _pollTimer.Start();
            Localizer.LanguageChanged += OnLanguageChanged;
            _ = RefreshAsync();
        }

        private void OnLanguageChanged(object? sender, EventArgs e) =>
            StatusText = Localizer.GetUiText(StatusText);

        private void Refresh() => _ = RefreshAsync();

        private async Task RefreshAsync()
        {
            // The dashboard can be constructed before optional integrations are ready.
            // Capture the current client and simply skip Spotify until it exists.
            var spotifyClient = App.Spotify;
            if (spotifyClient?.IsConfigured == true)
            {
                try
                {
                    var spotify = await spotifyClient.GetNowPlayingAsync();
                    if (spotify.Found)
                    {
                        ApplySpotifyState(spotify);
                        OnPropertyChanged(nameof(IsConnected));
                        return;
                    }

                    if (!string.IsNullOrWhiteSpace(spotifyClient.LastError))
                    {
                        ApplyIdleState();
                        _playbackSource = PlaybackSource.Spotify;
                        StatusText = spotifyClient.LastError;
                        return;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[Music] Spotify refresh failed: {ex}");
                    ApplyIdleState();
                    _playbackSource = PlaybackSource.Spotify;
                    StatusText = Localizer.GetUiText("Spotify couldn't be reached right now.");
                    return;
                }
            }

            if (!_media.IsAvailable)
            {
                ApplyIdleState();
                _playbackSource = PlaybackSource.None;
                StatusText = spotifyClient?.IsConfigured == true
                    ? "Nothing is currently playing."
                    : "Windows media controls aren't available on this PC.";
                return;
            }

            try
            {
                var info = await _media.GetNowPlayingAsync();
                if (info == null || string.IsNullOrWhiteSpace(info.Title))
                {
                    ApplyIdleState();
                    _playbackSource = PlaybackSource.None;
                    StatusText = Localizer.GetUiText("Nothing is currently playing.");
                    return;
                }

                HasTrack = true;
                TrackName = info.Title;
                ArtistName = info.Artist;
                SourceApp = info.AppName;
                AlbumArtUrl = "";
                IsPlaying = string.Equals(info.PlaybackStatus, "Playing", StringComparison.OrdinalIgnoreCase);
                StatusText = string.IsNullOrWhiteSpace(SourceApp) ? "Windows media" : SourceApp;
                _playbackSource = PlaybackSource.WindowsMedia;
                OnPropertyChanged(nameof(IsConnected));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Music] Windows media refresh failed: {ex}");
                ApplyIdleState();
                _playbackSource = PlaybackSource.None;
                StatusText = Localizer.GetUiText("Music controls unavailable.");
            }
        }

        [RelayCommand]
        private async Task TogglePlayPauseAsync()
        {
            if (IsBusy) return;
            IsBusy = true;
            try
            {
                if (await TrySpotifyToggleAsync())
                {
                    await RefreshAsync();
                    return;
                }

                await _media.ControlAsync("toggle");
                await RefreshAsync();
            }
            finally { IsBusy = false; }
        }

        [RelayCommand]
        private async Task SkipNextAsync()
        {
            if (IsBusy) return;
            IsBusy = true;
            try
            {
                if (await TrySpotifyCommandAsync(client => client.SkipNextAsync()))
                {
                    await RefreshAsync();
                    return;
                }

                await _media.ControlAsync("next");
                await Task.Delay(200);
                await RefreshAsync();
            }
            finally { IsBusy = false; }
        }

        [RelayCommand]
        private async Task SkipPreviousAsync()
        {
            if (IsBusy) return;
            IsBusy = true;
            try
            {
                if (await TrySpotifyCommandAsync(client => client.SkipPreviousAsync()))
                {
                    await RefreshAsync();
                    return;
                }

                await _media.ControlAsync("previous");
                await Task.Delay(200);
                await RefreshAsync();
            }
            finally { IsBusy = false; }
        }

        private void ApplySpotifyState(SpotifyNowPlaying spotify)
        {
            HasTrack = true;
            TrackName = spotify.TrackName;
            ArtistName = spotify.Artist;
            AlbumArtUrl = spotify.AlbumArtUrl;
            SourceApp = "Spotify";
            IsPlaying = spotify.IsPlaying;
            StatusText = spotify.IsPlaying ? "Playing on Spotify" : "Paused on Spotify";
            _playbackSource = PlaybackSource.Spotify;
        }

        private void ApplyIdleState()
        {
            HasTrack = false;
            TrackName = "";
            ArtistName = "";
            SourceApp = "";
            AlbumArtUrl = "";
            IsPlaying = false;
        }

        private async Task<bool> TrySpotifyToggleAsync()
        {
            var spotify = App.Spotify;
            if (spotify?.IsConfigured != true)
                return false;

            if (_playbackSource == PlaybackSource.WindowsMedia)
                return false;

            var message = IsPlaying
                ? await spotify.PauseAsync()
                : await spotify.ResumeAsync();
            StatusText = message;
            return true;
        }

        private async Task<bool> TrySpotifyCommandAsync(Func<SpotifyClient, Task<string>> command)
        {
            var spotify = App.Spotify;
            if (spotify?.IsConfigured != true)
                return false;

            if (_playbackSource == PlaybackSource.WindowsMedia)
                return false;

            StatusText = await command(spotify);
            return true;
        }
    }
}
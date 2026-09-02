using System;
using System.Threading.Tasks;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ZoeyOS.App.Services;

namespace ZoeyOS.App.ViewModels
{
    public partial class MusicPlayerViewModel : ObservableObject
    {
        private readonly DispatcherTimer _pollTimer;
        private readonly MediaControlService _media = new();

        [ObservableProperty] private bool _hasTrack;
        [ObservableProperty] private string _trackName = "";
        [ObservableProperty] private string _artistName = "";
        [ObservableProperty] private string _sourceApp = "";
        [ObservableProperty] private string _albumArtUrl = "";
        [ObservableProperty] private bool _isPlaying;
        [ObservableProperty] private bool _isBusy;
        [ObservableProperty] private string _statusText = "Checking what's playing...";

        public bool IsConnected => _media.IsAvailable || App.Spotify.IsConfigured;

        public MusicPlayerViewModel()
        {
            _pollTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _pollTimer.Tick += (_, _) => Refresh();
            _pollTimer.Start();
            _ = RefreshAsync();
        }

        private void Refresh() => _ = RefreshAsync();

        private async Task RefreshAsync()
        {
            // Prefer Spotify when connected so the dashboard can show real album artwork.
            if (App.Spotify.IsConfigured)
            {
                var spotify = await App.Spotify.GetNowPlayingAsync();
                if (spotify.Found)
                {
                    HasTrack = true;
                    TrackName = spotify.TrackName;
                    ArtistName = spotify.Artist;
                    AlbumArtUrl = spotify.AlbumArtUrl;
                    SourceApp = "Spotify";
                    IsPlaying = spotify.IsPlaying;
                    StatusText = spotify.IsPlaying ? "Playing on Spotify" : "Paused on Spotify";
                    OnPropertyChanged(nameof(IsConnected));
                    return;
                }
            }

            if (!_media.IsAvailable)
            {
                HasTrack = false;
                IsPlaying = false;
                AlbumArtUrl = "";
                StatusText = App.Spotify.IsConfigured ? "Nothing is currently playing." : "Windows media controls aren't available on this PC.";
                return;
            }

            var info = await _media.GetNowPlayingAsync();
            if (info == null || string.IsNullOrWhiteSpace(info.Title))
            {
                HasTrack = false;
                IsPlaying = false;
                SourceApp = "";
                AlbumArtUrl = "";
                StatusText = "Nothing is currently playing.";
                return;
            }

            HasTrack = true;
            TrackName = info.Title;
            ArtistName = info.Artist;
            SourceApp = info.AppName;
            AlbumArtUrl = "";
            IsPlaying = string.Equals(info.PlaybackStatus, "Playing", StringComparison.OrdinalIgnoreCase);
            StatusText = string.IsNullOrWhiteSpace(SourceApp) ? "Windows media" : SourceApp;
            OnPropertyChanged(nameof(IsConnected));
        }

        [RelayCommand]
        private async Task TogglePlayPauseAsync()
        {
            if (IsBusy) return;
            IsBusy = true;
            try
            {
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
            try { await _media.ControlAsync("next"); await Task.Delay(200); await RefreshAsync(); }
            finally { IsBusy = false; }
        }

        [RelayCommand]
        private async Task SkipPreviousAsync()
        {
            if (IsBusy) return;
            IsBusy = true;
            try { await _media.ControlAsync("previous"); await Task.Delay(200); await RefreshAsync(); }
            finally { IsBusy = false; }
        }
    }
}
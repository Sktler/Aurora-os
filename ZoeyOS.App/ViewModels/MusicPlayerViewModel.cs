using System;
using System.Threading.Tasks;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ZoeyOS.App.ViewModels
{
    /// <summary>
    /// Backs the in-app "Now playing" mini player, docked at the bottom of the sidebar.
    /// Talks to the same shared App.Spotify client the chat tools use, so playback started
    /// from a companion's chat shows up here too (and vice versa) - it's one connection,
    /// two ways to control it. Polls every 15s so it stays in sync even when playback
    /// changes elsewhere (phone, desktop Spotify app, etc.), not just from this app.
    /// </summary>
    public partial class MusicPlayerViewModel : ObservableObject
    {
        private readonly DispatcherTimer _pollTimer;

        [ObservableProperty] private bool _hasTrack;
        [ObservableProperty] private string _trackName = "";
        [ObservableProperty] private string _artistName = "";
        [ObservableProperty] private bool _isPlaying;
        [ObservableProperty] private bool _isBusy;
        [ObservableProperty] private string _statusText = "Checking Spotify...";

        public bool IsConnected => App.Settings.SpotifyConnected;

        public MusicPlayerViewModel()
        {
            _pollTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
            _pollTimer.Tick += async (_, _) => await RefreshAsync();
            _pollTimer.Start();
            _ = RefreshAsync();
        }

        [RelayCommand]
        private async Task RefreshAsync()
        {
            OnPropertyChanged(nameof(IsConnected));

            if (!App.Spotify.IsConfigured)
            {
                HasTrack = false;
                StatusText = "Not connected - set up Spotify in Settings.";
                return;
            }

            try
            {
                var info = await App.Spotify.GetNowPlayingAsync();
                HasTrack = info.Found;
                if (info.Found)
                {
                    TrackName = info.TrackName;
                    ArtistName = info.Artist;
                    IsPlaying = info.IsPlaying;
                    StatusText = "";
                }
                else
                {
                    StatusText = "Nothing is currently playing.";
                }
            }
            catch (Exception ex)
            {
                StatusText = $"Couldn't reach Spotify: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task TogglePlayPauseAsync()
        {
            if (IsBusy || !App.Spotify.IsConfigured) return;
            IsBusy = true;
            try
            {
                var result = IsPlaying ? await App.Spotify.PauseAsync() : await App.Spotify.ResumeAsync();
                // Optimistic flip so the button responds immediately - RefreshAsync below
                // corrects it if the command actually failed (e.g. no active device).
                IsPlaying = !IsPlaying;
                await RefreshAsync();
                if (result.StartsWith("Couldn't")) StatusText = result;
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task SkipNextAsync()
        {
            if (IsBusy || !App.Spotify.IsConfigured) return;
            IsBusy = true;
            try
            {
                await App.Spotify.SkipNextAsync();
                await Task.Delay(400); // give Spotify a beat to update before we poll it
                await RefreshAsync();
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task SkipPreviousAsync()
        {
            if (IsBusy || !App.Spotify.IsConfigured) return;
            IsBusy = true;
            try
            {
                await App.Spotify.SkipPreviousAsync();
                await Task.Delay(400);
                await RefreshAsync();
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}

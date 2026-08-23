using System;
using System.Threading.Tasks;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ZoeyOS.App.ViewModels
{
    public partial class MusicPlayerViewModel : ObservableObject
    {
        private readonly DispatcherTimer _pollTimer;
        [ObservableProperty] private bool _hasTrack;
        [ObservableProperty] private string _trackName = "";
        [ObservableProperty] private string _artistName = "";
        [ObservableProperty] private bool _isPlaying;
        [ObservableProperty] private bool _isBusy;
        [ObservableProperty] private string _statusText = "Checking Jamendo...";

        public bool IsConnected => App.Jamendo.IsConfigured;

        public MusicPlayerViewModel()
        {
            _pollTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            _pollTimer.Tick += (_, _) => Refresh();
            _pollTimer.Start();
            Refresh();
        }

        private void Refresh()
        {
            OnPropertyChanged(nameof(IsConnected));
            if (!App.Jamendo.IsConfigured)
            {
                HasTrack = false;
                StatusText = "Not connected - add a Jamendo Client ID in Settings.";
                return;
            }

            var info = App.Jamendo.GetNowPlaying();
            HasTrack = info.Found;
            IsPlaying = info.IsPlaying;
            if (info.Found)
            {
                TrackName = info.TrackName;
                ArtistName = info.Artist;
                StatusText = "";
            }
            else StatusText = "Nothing is currently playing.";
        }

        [RelayCommand]
        private void TogglePlayPause()
        {
            if (IsBusy || !App.Jamendo.IsConfigured) return;
            IsBusy = true;
            try
            {
                if (IsPlaying) App.Jamendo.Pause(); else App.Jamendo.Resume();
                Refresh();
            }
            finally { IsBusy = false; }
        }

        [RelayCommand]
        private async Task SkipNextAsync()
        {
            if (IsBusy || !App.Jamendo.IsConfigured) return;
            IsBusy = true;
            try { await App.Jamendo.PlayNextAsync(); await Task.Delay(200); Refresh(); }
            finally { IsBusy = false; }
        }

        [RelayCommand]
        private async Task SkipPreviousAsync()
        {
            if (IsBusy || !App.Jamendo.IsConfigured) return;
            IsBusy = true;
            try { await App.Jamendo.PlayPreviousAsync(); await Task.Delay(200); Refresh(); }
            finally { IsBusy = false; }
        }
    }
}

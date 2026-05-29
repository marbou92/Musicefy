using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Musicefy.Core.Interfaces;
using Musicefy.Core.Models;
using Musicefy.Core.Services;

namespace Musicefy.ViewModels
{
    public class ArtistViewModel : ViewModelBase
    {
        private readonly IAudioPlayer _playback;
        private readonly ArtistAlbumService _artistAlbumService;

        private string _artistName;
        public string ArtistName
        {
            get => _artistName;
            set => SetProperty(ref _artistName, value);
        }

        private string _coverPath;
        public string CoverPath
        {
            get => _coverPath;
            set => SetProperty(ref _coverPath, value);
        }

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        private bool _isAlbumsTab;
        public bool IsAlbumsTab
        {
            get => _isAlbumsTab;
            set { SetProperty(ref _isAlbumsTab, value); OnPropertyChanged(nameof(IsTracksTab)); }
        }

        public bool IsTracksTab => !_isAlbumsTab;

        public ObservableCollection<MusicFile> Tracks { get; } = new ObservableCollection<MusicFile>();
        public ObservableCollection<AlbumInfo> Albums { get; } = new ObservableCollection<AlbumInfo>();

        public ICommand PlayAllCommand { get; }
        public ICommand PlayTrackCommand { get; }
        public ICommand AlbumClickCommand { get; }
        public ICommand SwitchToAlbumsCommand { get; }
        public ICommand SwitchToTracksCommand { get; }

        public event Action<AlbumInfo> RequestNavigateToAlbum;

        public ArtistViewModel(IAudioPlayer playback, ArtistAlbumService artistAlbumService)
        {
            _playback = playback;
            _artistAlbumService = artistAlbumService;

            PlayAllCommand = new RelayCommand(_ => ExecutePlayAll());
            PlayTrackCommand = new RelayCommand(p => { if (p is MusicFile t) _playback.PlayTrack(t); });
            AlbumClickCommand = new RelayCommand(p => { if (p is AlbumInfo a) RequestNavigateToAlbum?.Invoke(a); });
            SwitchToAlbumsCommand = new RelayCommand(_ => IsAlbumsTab = true);
            SwitchToTracksCommand = new RelayCommand(_ => IsAlbumsTab = false);
        }

        public async Task LoadAsync(string artistName)
        {
            ArtistName = artistName;
            IsLoading = true;
            Tracks.Clear();
            Albums.Clear();

            try
            {
                var artist = await _artistAlbumService.GetArtistDetailAsync(artistName);
                if (artist != null)
                {
                    CoverPath = artist.CoverPath;
                    foreach (var track in artist.Tracks)
                        Tracks.Add(track);
                    foreach (var album in artist.Albums)
                        Albums.Add(album);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ArtistViewModel] Load failed: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void ExecutePlayAll()
        {
            if (Tracks.Count > 0)
                _playback.SetQueue(Tracks, startPlaying: true);
        }
    }
}

using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;
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

        public int SongsCount => Tracks.Count > 15 ? 15 : Tracks.Count;
        public string SongsCountText => Tracks.Count > 15 ? "15+" : Tracks.Count.ToString();

        public int AlbumsCountVal => Albums.Count;
        public string AlbumsCountText => Albums.Count.ToString();

        private Brush _backgroundGradient;
        public Brush BackgroundGradient
        {
            get => _backgroundGradient;
            set => SetProperty(ref _backgroundGradient, value);
        }

        public ObservableCollection<MusicFile> Tracks { get; } = new ObservableCollection<MusicFile>();
        public ObservableCollection<AlbumInfo> Albums { get; } = new ObservableCollection<AlbumInfo>();

        public ICommand PlayAllCommand { get; }
        public ICommand PlayTrackCommand { get; }
        public ICommand AlbumClickCommand { get; }
        public ICommand SwitchToAlbumsCommand { get; }
        public ICommand SwitchToTracksCommand { get; }
        public ICommand SubscribeCommand { get; }
        public ICommand RadioCommand { get; }
        public ICommand ShufflePlayCommand { get; }
        public ICommand TrackMoreCommand { get; }

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
            SubscribeCommand = new RelayCommand(_ => System.Windows.MessageBox.Show("Subscribe feature coming soon", "Coming Soon"));
            RadioCommand = new RelayCommand(_ => System.Windows.MessageBox.Show("Radio feature coming soon", "Coming Soon"));
            ShufflePlayCommand = new RelayCommand(_ => ExecuteShufflePlay());
            TrackMoreCommand = new RelayCommand(p =>
            {
                if (p is MusicFile t)
                    System.Windows.MessageBox.Show($"{t.Title} - {t.Artist}", "Track Options");
            });
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
                OnPropertyChanged(nameof(SongsCount));
                OnPropertyChanged(nameof(SongsCountText));
                OnPropertyChanged(nameof(AlbumsCountVal));
                OnPropertyChanged(nameof(AlbumsCountText));
            }
        }

        private void ExecutePlayAll()
        {
            if (Tracks.Count > 0)
                _playback.SetQueue(Tracks, startPlaying: true);
        }

        private void ExecuteShufflePlay()
        {
            if (Tracks.Count > 0)
            {
                _playback.ShuffleQueue();
                _playback.SetQueue(Tracks, startPlaying: true);
            }
        }
    }
}

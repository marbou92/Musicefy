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
    public class AlbumViewModel : ViewModelBase
    {
        private readonly IAudioPlayer _playback;
        private readonly ArtistAlbumService _artistAlbumService;

        private string _albumName;
        public string AlbumName
        {
            get => _albumName;
            set => SetProperty(ref _albumName, value);
        }

        private string _artistName;
        public string ArtistName
        {
            get => _artistName;
            set => SetProperty(ref _artistName, value);
        }

        private int _year;
        public int Year
        {
            get => _year;
            set => SetProperty(ref _year, value);
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

        private Brush _backgroundGradient;
        public Brush BackgroundGradient
        {
            get => _backgroundGradient;
            set => SetProperty(ref _backgroundGradient, value);
        }

        private bool _isFavourited;
        public bool IsFavourited
        {
            get => _isFavourited;
            set => SetProperty(ref _isFavourited, value);
        }

        private TimeSpan _totalDuration;
        public TimeSpan TotalDuration
        {
            get => _totalDuration;
            private set => SetProperty(ref _totalDuration, value);
        }

        public string TotalDurationText
        {
            get
            {
                if (TotalDuration.TotalHours >= 1)
                    return $"{(int)TotalDuration.TotalHours}:{TotalDuration.Minutes:D2}:{TotalDuration.Seconds:D2}";
                return $"{TotalDuration.Minutes}:{TotalDuration.Seconds:D2}";
            }
        }

        public string SongsCountText => $"{Tracks.Count} songs";

        public string YearText => Year > 0 ? Year.ToString() : "";

        public ObservableCollection<MusicFile> Tracks { get; } = new ObservableCollection<MusicFile>();

        public ICommand PlayAllCommand { get; }
        public ICommand PlayTrackCommand { get; }
        public ICommand FavouriteCommand { get; }
        public ICommand DownloadCommand { get; }
        public ICommand MoreCommand { get; }
        public ICommand ShuffleAlbumCommand { get; }

        public event Action<string> RequestNavigateToArtist;

        public AlbumViewModel(IAudioPlayer playback, ArtistAlbumService artistAlbumService)
        {
            _playback = playback;
            _artistAlbumService = artistAlbumService;

            PlayAllCommand = new RelayCommand(_ => ExecutePlayAll());
            PlayTrackCommand = new RelayCommand(p => { if (p is MusicFile t) _playback.PlayTrack(t); });
            FavouriteCommand = new RelayCommand(_ => IsFavourited = !IsFavourited);
            DownloadCommand = new RelayCommand(_ => System.Windows.MessageBox.Show("Download feature coming soon", "Coming Soon"));
            MoreCommand = new RelayCommand(p =>
            {
                if (p is MusicFile t)
                    System.Windows.MessageBox.Show($"{t.Title} - {t.Artist}", "Track Options");
            });
            ShuffleAlbumCommand = new RelayCommand(_ => ExecuteShuffleAlbum());
        }

        public async Task LoadAsync(string albumName, string artistName = null)
        {
            AlbumName = albumName;
            ArtistName = artistName;
            IsLoading = true;
            Tracks.Clear();

            try
            {
                var album = await _artistAlbumService.GetAlbumDetailAsync(albumName, artistName);
                if (album != null)
                {
                    CoverPath = album.CoverPath;
                    Year = album.Year;
                    ArtistName = album.Artist;
                    foreach (var track in album.Tracks)
                        Tracks.Add(track);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AlbumViewModel] Load failed: {ex.Message}");
            }
            finally
            {
                TotalDuration = TimeSpan.FromTicks(Tracks.Sum(t => t.Duration.Ticks));
                IsLoading = false;
                OnPropertyChanged(nameof(TotalDurationText));
                OnPropertyChanged(nameof(SongsCountText));
                OnPropertyChanged(nameof(YearText));
            }
        }

        private void ExecutePlayAll()
        {
            if (Tracks.Count > 0)
                _playback.SetQueue(Tracks, startPlaying: true);
        }

        private void ExecuteShuffleAlbum()
        {
            if (Tracks.Count > 0)
            {
                _playback.ShuffleQueue();
                _playback.SetQueue(Tracks, startPlaying: true);
            }
        }
    }
}

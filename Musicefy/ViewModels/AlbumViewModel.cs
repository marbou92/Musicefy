using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;
using Musicefy.Core.Interfaces;
using Musicefy.Core.Models;
using Musicefy.Core.Services;
using static Musicefy.Core.SourceTypes;

namespace Musicefy.ViewModels
{
    public class AlbumViewModel : ViewModelBase
    {
        private readonly IAudioPlayer _playback;
        private readonly ArtistAlbumService _artistAlbumService;
        private readonly IStreamingSourceManager _sourceManager;

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

        /// <summary>
        /// The stable album ID (YouTube browse ID or generated local ID).
        /// Enables YouTube Music browsing and persistent navigation.
        /// </summary>
        private string _albumId;
        public string AlbumId
        {
            get => _albumId;
            set => SetProperty(ref _albumId, value);
        }

        /// <summary>
        /// Whether this album is from a YouTube source.
        /// Determines whether YouTube browse is available.
        /// </summary>
        private bool _isYouTubeAlbum;
        public bool IsYouTubeAlbum
        {
            get => _isYouTubeAlbum;
            set => SetProperty(ref _isYouTubeAlbum, value);
        }

        /// <summary>
        /// The YouTube artist channel ID for navigating from album → artist.
        /// Populated during YouTube album browse for seamless navigation.
        /// </summary>
        private string _artistYouTubeId;
        public string ArtistYouTubeId
        {
            get => _artistYouTubeId;
            set => SetProperty(ref _artistYouTubeId, value);
        }

        public ObservableCollection<MusicFile> Tracks { get; } = new ObservableCollection<MusicFile>();

        public ICommand PlayAllCommand { get; }
        public ICommand PlayTrackCommand { get; }
        public ICommand FavouriteCommand { get; }
        public ICommand DownloadCommand { get; }
        public ICommand MoreCommand { get; }
        public ICommand ShuffleAlbumCommand { get; }

#pragma warning disable CS0067 // Event is subscribed to by AlbumView; will be used for track-level artist navigation
        public event Action<ArtistInfo> RequestNavigateToArtist;
#pragma warning restore CS0067

        public AlbumViewModel(IAudioPlayer playback, ArtistAlbumService artistAlbumService, IStreamingSourceManager sourceManager)
        {
            _playback = playback;
            _artistAlbumService = artistAlbumService;
            _sourceManager = sourceManager;

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

        /// <summary>
        /// Load album by name (legacy path — local/subsonic sources).
        /// </summary>
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
                    ApplyAlbumInfo(album);
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

        /// <summary>
        /// Load album from a full <see cref="AlbumInfo"/> object.
        /// If the album has a YouTube browse ID (MPRE...), browses YouTube Music
        /// for the complete track list with metadata. Otherwise falls back
        /// to name-based lookup via ArtistAlbumService.
        /// Inspired by Echo Music's two-step album fetch (list → detail).
        /// </summary>
        public async Task LoadAsync(AlbumInfo albumInfo)
        {
            if (albumInfo == null) return;

            IsLoading = true;
            Tracks.Clear();

            try
            {
                // If we have a YouTube album browse ID, browse YouTube Music directly
                if (!string.IsNullOrEmpty(albumInfo.YouTubeAlbumId))
                {
                    var ytAlbum = await _artistAlbumService.GetAlbumByYouTubeIdAsync(
                        albumInfo.YouTubeAlbumId,
                        albumInfo.Name,
                        albumInfo.Artist);

                    if (ytAlbum != null)
                    {
                        ApplyAlbumInfo(ytAlbum);
                        IsYouTubeAlbum = true;
                        return;
                    }
                }

                // If the AlbumInfo already has tracks (from search results), use them directly
                if (albumInfo.Tracks?.Count > 0)
                {
                    ApplyAlbumInfo(albumInfo);
                    return;
                }

                // Fall back to name-based lookup
                if (!string.IsNullOrEmpty(albumInfo.Name))
                {
                    var album = await _artistAlbumService.GetAlbumDetailAsync(albumInfo.Name, albumInfo.Artist);
                    if (album != null)
                    {
                        // Preserve the original YouTube IDs if the name lookup lost them
                        if (string.IsNullOrEmpty(album.Id) && !string.IsNullOrEmpty(albumInfo.Id))
                            album.Id = albumInfo.Id;
                        if (string.IsNullOrEmpty(album.YouTubeAlbumId) && !string.IsNullOrEmpty(albumInfo.YouTubeAlbumId))
                            album.YouTubeAlbumId = albumInfo.YouTubeAlbumId;

                        ApplyAlbumInfo(album);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AlbumViewModel] LoadAsync(AlbumInfo) failed: {ex.Message}");
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

        /// <summary>
        /// Apply an <see cref="AlbumInfo"/> object to the ViewModel's bindable properties.
        /// Centralizes the mapping logic between the model and the ViewModel.
        /// </summary>
        private void ApplyAlbumInfo(AlbumInfo album)
        {
            AlbumName = album.Name;
            AlbumId = album.Id;
            ArtistName = album.Artist;
            Year = album.Year;
            CoverPath = album.CoverPath;
            IsYouTubeAlbum = album.SourceType == YouTube
                             || !string.IsNullOrEmpty(album.YouTubeAlbumId);

            // Capture artist YouTube ID from album tracks for artist navigation
            // Phase 2: Prefer AlbumInfo.ArtistId, then fall back to track ArtistBrowseId
            ArtistYouTubeId = album.ArtistId
                              ?? album.Tracks?
                                     .FirstOrDefault(t => !string.IsNullOrEmpty(t.ArtistBrowseId))?.ArtistBrowseId;

            Tracks.Clear();
            foreach (var track in album.Tracks)
                Tracks.Add(track);
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

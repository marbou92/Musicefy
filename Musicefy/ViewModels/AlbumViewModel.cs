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
        private readonly ILibraryService _libraryService;

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

        /// <summary>
        /// Whether the user has favourited/liked this album.
        /// This is the track-level favourite toggle (not persisted to Albums table).
        /// Kept for UI compatibility with the heart button.
        /// </summary>
        private bool _isFavourited;
        public bool IsFavourited
        {
            get => _isFavourited;
            set => SetProperty(ref _isFavourited, value);
        }

        /// <summary>
        /// Whether the user has saved this album to their library.
        /// Persisted in the Albums table via ILibraryService.
        /// This is the album-level save (separate from track favourite).
        /// </summary>
        private bool _isSaved;
        public bool IsSaved
        {
            get => _isSaved;
            set => SetProperty(ref _isSaved, value);
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

        /// <summary>
        /// Album description (from YouTube Music or local metadata).
        /// </summary>
        private string _description;
        public string Description
        {
            get => _description;
            set => SetProperty(ref _description, value);
        }

        /// <summary>
        /// Genre of the album.
        /// </summary>
        private string _genre;
        public string Genre
        {
            get => _genre;
            set => SetProperty(ref _genre, value);
        }

        public ObservableCollection<MusicFile> Tracks { get; } = new ObservableCollection<MusicFile>();

        public ICommand PlayAllCommand { get; }
        public ICommand PlayTrackCommand { get; }
        public ICommand FavouriteCommand { get; }
        public ICommand SaveAlbumCommand { get; }
        public ICommand DownloadCommand { get; }
        public ICommand MoreCommand { get; }
        public ICommand ShuffleAlbumCommand { get; }

#pragma warning disable CS0067 // Event is subscribed to by AlbumView; will be used for track-level artist navigation
        public event Action<ArtistInfo> RequestNavigateToArtist;
#pragma warning restore CS0067

        public AlbumViewModel(IAudioPlayer playback, ArtistAlbumService artistAlbumService,
            IStreamingSourceManager sourceManager, ILibraryService libraryService)
        {
            _playback = playback;
            _artistAlbumService = artistAlbumService;
            _sourceManager = sourceManager;
            _libraryService = libraryService;

            PlayAllCommand = new RelayCommand(_ => ExecutePlayAll());
            PlayTrackCommand = new RelayCommand(p => { if (p is MusicFile t) _playback.PlayTrack(t); });
            FavouriteCommand = new RelayCommand(_ => IsFavourited = !IsFavourited);
            SaveAlbumCommand = new RelayCommand(async _ => await ExecuteToggleSaveAlbumAsync());
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
                        // Merge IsSaved from the passed-in albumInfo (if it was loaded from DB)
                        if (albumInfo.IsSaved)
                            ytAlbum.IsSaved = true;

                        ApplyAlbumInfo(ytAlbum);
                        IsYouTubeAlbum = true;

                        // Phase 3: Auto-persist the album when browsed from YouTube
                        await AutoSaveAlbumAsync(ytAlbum);
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
                        if (albumInfo.IsSaved)
                            album.IsSaved = true;

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
        /// After applying, checks the database for the current save state.
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
            IsSaved = album.IsSaved;
            Description = album.Description;
            Genre = album.Genre;

            // Capture artist YouTube ID from album tracks for artist navigation
            // Phase 2: Prefer AlbumInfo.ArtistId, then fall back to track ArtistBrowseId
            ArtistYouTubeId = album.ArtistId
                              ?? album.Tracks?
                                     .FirstOrDefault(t => !string.IsNullOrEmpty(t.ArtistBrowseId))?.ArtistBrowseId;

            Tracks.Clear();
            foreach (var track in album.Tracks)
                Tracks.Add(track);

            // Check persisted save state from database if we have an ID
            if (!string.IsNullOrEmpty(album.Id))
            {
                _ = CheckSaveStateAsync(album.Id);
            }
        }

        /// <summary>
        /// Check the database for the current save state of this album.
        /// Updates IsSaved if the database has a different value.
        /// </summary>
        private async Task CheckSaveStateAsync(string albumId)
        {
            try
            {
                var persisted = await _libraryService.GetAlbumAsync(albumId);
                if (persisted != null)
                {
                    IsSaved = persisted.IsSaved;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AlbumViewModel] CheckSaveState failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Toggle the saved state of this album.
        /// Persists the change to the Albums table via ILibraryService.
        /// </summary>
        private async Task ExecuteToggleSaveAlbumAsync()
        {
            if (string.IsNullOrEmpty(AlbumId)) return;

            var albumInfo = BuildCurrentAlbumInfo();

            try
            {
                await _libraryService.ToggleSaveAlbumAsync(albumInfo);
                IsSaved = !IsSaved;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AlbumViewModel] ToggleSave failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Auto-persist the album when browsed from YouTube.
        /// Sets LastBrowsedAt for cache invalidation.
        /// Preserves the existing IsSaved state from the database.
        /// </summary>
        private async Task AutoSaveAlbumAsync(AlbumInfo album)
        {
            try
            {
                // Check if we already have this album persisted
                var existing = await _libraryService.GetAlbumAsync(album.Id);
                if (existing != null)
                {
                    // Preserve IsSaved from existing record
                    album.IsSaved = existing.IsSaved;
                    // Preserve Description if the browse didn't provide it
                    if (string.IsNullOrEmpty(album.Description) && !string.IsNullOrEmpty(existing.Description))
                        album.Description = existing.Description;
                }

                album.LastBrowsedAt = DateTime.UtcNow;
                await _libraryService.SaveAlbumAsync(album);

                // Update IsSaved from persisted state
                IsSaved = album.IsSaved;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AlbumViewModel] AutoSave failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Build an <see cref="AlbumInfo"/> from the current ViewModel state.
        /// Used for persistence operations (save/unsave, auto-save).
        /// </summary>
        private AlbumInfo BuildCurrentAlbumInfo()
        {
            return new AlbumInfo
            {
                Id = AlbumId,
                Name = AlbumName,
                Artist = ArtistName,
                ArtistId = ArtistYouTubeId,
                Year = Year,
                CoverPath = CoverPath,
                SourceType = IsYouTubeAlbum ? YouTube : null,
                YouTubeAlbumId = IsYouTubeAlbum ? AlbumId : null,
                Description = Description,
                Genre = Genre,
                IsSaved = IsSaved,
                TrackCount = Tracks.Count,
                LastBrowsedAt = DateTime.UtcNow
            };
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

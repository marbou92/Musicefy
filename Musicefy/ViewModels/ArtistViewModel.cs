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
    public class ArtistViewModel : ViewModelBase
    {
        private readonly IAudioPlayer _playback;
        private readonly ArtistAlbumService _artistAlbumService;
        private readonly IStreamingSourceManager _sourceManager;
        private readonly ILibraryService _libraryService;

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

        /// <summary>
        /// The stable artist ID (YouTube channel ID or generated local ID).
        /// Enables YouTube Music browsing and persistent navigation.
        /// </summary>
        private string _artistId;
        public string ArtistId
        {
            get => _artistId;
            set => SetProperty(ref _artistId, value);
        }

        /// <summary>
        /// Whether this artist is from a YouTube source.
        /// Determines whether YouTube browse is available.
        /// </summary>
        private bool _isYouTubeArtist;
        public bool IsYouTubeArtist
        {
            get => _isYouTubeArtist;
            set => SetProperty(ref _isYouTubeArtist, value);
        }

        /// <summary>
        /// Whether the user has followed/subscribed to this artist.
        /// Persisted in the Artists table via ILibraryService.
        /// </summary>
        private bool _isFollowed;
        public bool IsFollowed
        {
            get => _isFollowed;
            set => SetProperty(ref _isFollowed, value);
        }

        /// <summary>
        /// Artist biography / description (from YouTube Music or local metadata).
        /// Populated during YouTube browse and persisted in the Artists table.
        /// </summary>
        private string _description;
        public string Description
        {
            get => _description;
            set => SetProperty(ref _description, value);
        }

        /// <summary>
        /// Number of subscribers (for YouTube Music artists).
        /// </summary>
        private long? _subscriberCount;
        public long? SubscriberCount
        {
            get => _subscriberCount;
            set { SetProperty(ref _subscriberCount, value); OnPropertyChanged(nameof(SubscriberText)); }
        }

        /// <summary>
        /// Display text for subscriber count (e.g. "1.2M subscribers").
        /// </summary>
        public string SubscriberText
        {
            get
            {
                if (SubscriberCount == null || SubscriberCount <= 0) return "";
                var count = SubscriberCount.Value;
                if (count >= 1_000_000) return $"{count / 1_000_000.0:F1}M subscribers";
                if (count >= 1_000) return $"{count / 1_000.0:F1}K subscribers";
                return $"{count} subscribers";
            }
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

        public ArtistViewModel(IAudioPlayer playback, ArtistAlbumService artistAlbumService,
            IStreamingSourceManager sourceManager, ILibraryService libraryService)
        {
            _playback = playback;
            _artistAlbumService = artistAlbumService;
            _sourceManager = sourceManager;
            _libraryService = libraryService;

            PlayAllCommand = new RelayCommand(_ => ExecutePlayAll());
            PlayTrackCommand = new RelayCommand(p => { if (p is MusicFile t) _playback.PlayTrack(t); });
            AlbumClickCommand = new RelayCommand(p => { if (p is AlbumInfo a) RequestNavigateToAlbum?.Invoke(a); });
            SwitchToAlbumsCommand = new RelayCommand(_ => IsAlbumsTab = true);
            SwitchToTracksCommand = new RelayCommand(_ => IsAlbumsTab = false);
            SubscribeCommand = new RelayCommand(async _ => await ExecuteToggleFollowAsync());
            RadioCommand = new RelayCommand(_ => System.Windows.MessageBox.Show("Radio feature coming soon", "Coming Soon"));
            ShufflePlayCommand = new RelayCommand(_ => ExecuteShufflePlay());
            TrackMoreCommand = new RelayCommand(p =>
            {
                if (p is MusicFile t)
                    System.Windows.MessageBox.Show($"{t.Title} - {t.Artist}", "Track Options");
            });
        }

        /// <summary>
        /// Load artist by name (legacy path — local/subsonic sources).
        /// </summary>
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
                    ApplyArtistInfo(artist);
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

        /// <summary>
        /// Load artist from a full <see cref="ArtistInfo"/> object.
        /// If the artist has a YouTube channel ID, browses YouTube Music
        /// for rich data (top tracks + album list). Otherwise falls back
        /// to name-based lookup via ArtistAlbumService.
        /// Inspired by Echo Music's first-class artist entity model.
        /// </summary>
        public async Task LoadAsync(ArtistInfo artistInfo)
        {
            if (artistInfo == null) return;

            IsLoading = true;
            Tracks.Clear();
            Albums.Clear();

            try
            {
                // If we have a YouTube channel ID, browse YouTube Music directly
                if (!string.IsNullOrEmpty(artistInfo.YouTubeChannelId))
                {
                    var ytArtist = await _artistAlbumService.GetArtistByYouTubeIdAsync(
                        artistInfo.YouTubeChannelId,
                        artistInfo.Name);

                    if (ytArtist != null)
                    {
                        // Merge IsFollowed from the passed-in artistInfo (if it was loaded from DB)
                        if (artistInfo.IsFollowed)
                            ytArtist.IsFollowed = true;

                        ApplyArtistInfo(ytArtist);
                        IsYouTubeArtist = true;

                        // Phase 3: Auto-persist the artist when browsed from YouTube
                        await AutoSaveArtistAsync(ytArtist);
                        return;
                    }
                }

                // If the ArtistInfo already has tracks (from search results), use them directly
                if (artistInfo.Tracks?.Count > 0)
                {
                    ApplyArtistInfo(artistInfo);
                    return;
                }

                // Fall back to name-based lookup
                if (!string.IsNullOrEmpty(artistInfo.Name))
                {
                    var artist = await _artistAlbumService.GetArtistDetailAsync(artistInfo.Name);
                    if (artist != null)
                    {
                        // Preserve the original YouTube IDs if the name lookup lost them
                        if (string.IsNullOrEmpty(artist.Id) && !string.IsNullOrEmpty(artistInfo.Id))
                            artist.Id = artistInfo.Id;
                        if (string.IsNullOrEmpty(artist.YouTubeChannelId) && !string.IsNullOrEmpty(artistInfo.YouTubeChannelId))
                            artist.YouTubeChannelId = artistInfo.YouTubeChannelId;
                        if (artistInfo.IsFollowed)
                            artist.IsFollowed = true;

                        ApplyArtistInfo(artist);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ArtistViewModel] LoadAsync(ArtistInfo) failed: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
                OnPropertyChanged(nameof(SongsCount));
                OnPropertyChanged(nameof(SongsCountText));
                OnPropertyChanged(nameof(AlbumsCountVal));
                OnPropertyChanged(nameof(AlbumsCountText));
                OnPropertyChanged(nameof(SubscriberText));
            }
        }

        /// <summary>
        /// Apply an <see cref="ArtistInfo"/> object to the ViewModel's bindable properties.
        /// Centralizes the mapping logic between the model and the ViewModel.
        /// After applying, checks the database for the current follow state.
        /// </summary>
        private void ApplyArtistInfo(ArtistInfo artist)
        {
            ArtistName = artist.Name;
            ArtistId = artist.Id;
            CoverPath = artist.CoverPath;
            IsYouTubeArtist = artist.SourceType == YouTube
                              || !string.IsNullOrEmpty(artist.YouTubeChannelId);
            IsFollowed = artist.IsFollowed;
            Description = artist.Description;
            SubscriberCount = artist.SubscriberCount;

            Tracks.Clear();
            foreach (var track in artist.Tracks)
                Tracks.Add(track);

            Albums.Clear();
            foreach (var album in artist.Albums)
                Albums.Add(album);

            // Check persisted follow state from database if we have an ID
            if (!string.IsNullOrEmpty(artist.Id))
            {
                _ = CheckFollowStateAsync(artist.Id);
            }
        }

        /// <summary>
        /// Check the database for the current follow state of this artist.
        /// Updates IsFollowed if the database has a different value.
        /// </summary>
        private async Task CheckFollowStateAsync(string artistId)
        {
            try
            {
                var persisted = await _libraryService.GetArtistAsync(artistId);
                if (persisted != null)
                {
                    IsFollowed = persisted.IsFollowed;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ArtistViewModel] CheckFollowState failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Toggle the follow/subscribed state of this artist.
        /// Persists the change to the Artists table via ILibraryService.
        /// </summary>
        private async Task ExecuteToggleFollowAsync()
        {
            if (string.IsNullOrEmpty(ArtistId)) return;

            var artistInfo = BuildCurrentArtistInfo();

            try
            {
                await _libraryService.ToggleFollowArtistAsync(artistInfo);
                IsFollowed = !IsFollowed;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ArtistViewModel] ToggleFollow failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Auto-persist the artist when browsed from YouTube.
        /// Sets LastBrowsedAt for cache invalidation.
        /// Preserves the existing IsFollowed state from the database.
        /// </summary>
        private async Task AutoSaveArtistAsync(ArtistInfo artist)
        {
            try
            {
                // Check if we already have this artist persisted
                var existing = await _libraryService.GetArtistAsync(artist.Id);
                if (existing != null)
                {
                    // Preserve IsFollowed from existing record
                    artist.IsFollowed = existing.IsFollowed;
                    // Preserve Description/SubscriberCount if the browse didn't provide them
                    if (string.IsNullOrEmpty(artist.Description) && !string.IsNullOrEmpty(existing.Description))
                        artist.Description = existing.Description;
                    if ((artist.SubscriberCount == null || artist.SubscriberCount <= 0) && existing.SubscriberCount > 0)
                        artist.SubscriberCount = existing.SubscriberCount;
                }

                artist.LastBrowsedAt = DateTime.UtcNow;
                await _libraryService.SaveArtistAsync(artist);

                // Update IsFollowed from persisted state
                IsFollowed = artist.IsFollowed;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ArtistViewModel] AutoSave failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Build an <see cref="ArtistInfo"/> from the current ViewModel state.
        /// Used for persistence operations (follow/unfollow, auto-save).
        /// </summary>
        private ArtistInfo BuildCurrentArtistInfo()
        {
            return new ArtistInfo
            {
                Id = ArtistId,
                Name = ArtistName,
                CoverPath = CoverPath,
                SourceType = IsYouTubeArtist ? YouTube : null,
                YouTubeChannelId = IsYouTubeArtist ? ArtistId : null,
                Description = Description,
                SubscriberCount = SubscriberCount,
                IsFollowed = IsFollowed,
                LastBrowsedAt = DateTime.UtcNow
            };
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

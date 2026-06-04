using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;
using Musicefy.Core.Interfaces;
using Musicefy.Core.Models;
using static Musicefy.Core.SourceTypes;

namespace Musicefy.ViewModels
{
    /// <summary>
    /// ViewModel for the Playlist detail view.
    /// Phase 5: Playlists & Collection Management.
    /// Inspired by Echo Music's playlist detail screen with full CRUD,
    /// reorder, and track management capabilities.
    /// </summary>
    public class PlaylistViewModel : ViewModelBase
    {
        private readonly IAudioPlayer _playback;
        private readonly ILibraryService _libraryService;

        private string _playlistId;
        public string PlaylistId
        {
            get => _playlistId;
            set => SetProperty(ref _playlistId, value);
        }

        private string _playlistName;
        public string PlaylistName
        {
            get => _playlistName;
            set => SetProperty(ref _playlistName, value);
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

        private string _description;
        public string Description
        {
            get => _description;
            set => SetProperty(ref _description, value);
        }

        private string _youTubePlaylistId;
        public string YouTubePlaylistId
        {
            get => _youTubePlaylistId;
            set => SetProperty(ref _youTubePlaylistId, value);
        }

        private string _sourceType;
        public string SourceType
        {
            get => _sourceType;
            set => SetProperty(ref _sourceType, value);
        }

        private DateTime _createdAt;
        public DateTime CreatedAt
        {
            get => _createdAt;
            set => SetProperty(ref _createdAt, value);
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

        public string SongsCountText => Tracks.Count == 1 ? "1 song" : $"{Tracks.Count} songs";

        public string CreatedAtText => CreatedAt != DateTime.MinValue
            ? $"Created {CreatedAt:MMM d, yyyy}"
            : "";

        public ObservableCollection<MusicFile> Tracks { get; } = new ObservableCollection<MusicFile>();

        public ICommand PlayAllCommand { get; }
        public ICommand PlayTrackCommand { get; }
        public ICommand ShuffleCommand { get; }
        public ICommand RemoveTrackCommand { get; }
        public ICommand DeletePlaylistCommand { get; }
        public ICommand RenamePlaylistCommand { get; }
        public ICommand MoreCommand { get; }

        public event Action RequestGoBack;
        public event Action PlaylistDeleted;

        public PlaylistViewModel(IAudioPlayer playback, ILibraryService libraryService)
        {
            _playback = playback;
            _libraryService = libraryService;

            PlayAllCommand = new RelayCommand(_ => ExecutePlayAll());
            PlayTrackCommand = new RelayCommand(p => { if (p is MusicFile t) _playback.PlayTrack(t); });
            ShuffleCommand = new RelayCommand(_ => ExecuteShuffle());
            RemoveTrackCommand = new RelayCommand(async p => await ExecuteRemoveTrackAsync(p));
            DeletePlaylistCommand = new RelayCommand(async _ => await ExecuteDeletePlaylistAsync());
            RenamePlaylistCommand = new RelayCommand(_ => ExecuteRenamePlaylist());
            MoreCommand = new RelayCommand(p =>
            {
                if (p is MusicFile t)
                    System.Windows.MessageBox.Show($"{t.Title} - {t.Artist}", "Track Options");
            });
        }

        /// <summary>
        /// Load a playlist by its ID from the library database.
        /// </summary>
        public async Task LoadAsync(string playlistId)
        {
            if (string.IsNullOrEmpty(playlistId)) return;

            PlaylistId = playlistId;
            IsLoading = true;
            Tracks.Clear();

            try
            {
                var playlist = await _libraryService.GetPlaylistAsync(playlistId);
                if (playlist != null)
                {
                    ApplyPlaylistInfo(playlist);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PlaylistViewModel] Load failed: {ex.Message}");
            }
            finally
            {
                TotalDuration = TimeSpan.FromTicks(Tracks.Sum(t => t.Duration.Ticks));
                IsLoading = false;
                OnPropertyChanged(nameof(TotalDurationText));
                OnPropertyChanged(nameof(SongsCountText));
                OnPropertyChanged(nameof(CreatedAtText));
            }
        }

        /// <summary>
        /// Load from an existing PlaylistInfo object (e.g., from YouTube playlist import).
        /// If the playlist has a YouTubePlaylistId and tracks, saves them to the local library.
        /// </summary>
        public async Task LoadAsync(PlaylistInfo playlistInfo)
        {
            if (playlistInfo == null) return;

            IsLoading = true;
            Tracks.Clear();

            try
            {
                // If we already have a playlist ID, load from DB
                if (!string.IsNullOrEmpty(playlistInfo.Id))
                {
                    var existing = await _libraryService.GetPlaylistAsync(playlistInfo.Id);
                    if (existing != null)
                    {
                        ApplyPlaylistInfo(existing);
                        return;
                    }
                }

                // Otherwise apply the passed-in info directly (YouTube import scenario)
                ApplyPlaylistInfo(playlistInfo);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PlaylistViewModel] LoadAsync(PlaylistInfo) failed: {ex.Message}");
            }
            finally
            {
                TotalDuration = TimeSpan.FromTicks(Tracks.Sum(t => t.Duration.Ticks));
                IsLoading = false;
                OnPropertyChanged(nameof(TotalDurationText));
                OnPropertyChanged(nameof(SongsCountText));
                OnPropertyChanged(nameof(CreatedAtText));
            }
        }

        /// <summary>
        /// Load a YouTube playlist by fetching its tracks and creating a local playlist.
        /// This is used when navigating from a YouTube playlist URL or search result.
        /// </summary>
        public async Task LoadYouTubePlaylistAsync(string youTubePlaylistId, IStreamingSourceManager sourceManager)
        {
            if (string.IsNullOrEmpty(youTubePlaylistId) || sourceManager == null) return;

            IsLoading = true;
            Tracks.Clear();

            try
            {
                // Check if we already have this playlist saved
                var allPlaylists = await _libraryService.GetAllPlaylistsAsync();
                var existingPlaylist = allPlaylists.FirstOrDefault(
                    p => p.YouTubePlaylistId == youTubePlaylistId);

                if (existingPlaylist != null)
                {
                    // Load from the local database
                    var playlist = await _libraryService.GetPlaylistAsync(existingPlaylist.Id);
                    if (playlist != null)
                    {
                        ApplyPlaylistInfo(playlist);
                        return;
                    }
                }

                // Fetch from YouTube
                var ytSources = sourceManager.Sources
                    .Where(s => s.IsConnected && s.Type == YouTube)
                    .ToList();

                foreach (var ytSource in ytSources)
                {
                    var ytSession = sourceManager.GetYouTubeSession(ytSource.Id);
                    if (ytSession == null) continue;

                    try
                    {
                        var playlistTracks = await ytSession.GetPlaylistAsync(youTubePlaylistId, 200);
                        if (playlistTracks != null && playlistTracks.Count > 0)
                        {
                            // Create a new local playlist for this YouTube playlist
                            var name = $"YouTube Playlist";
                            // Try to find a title from the first track's album or a generic name
                            if (playlistTracks[0] != null && !string.IsNullOrEmpty(playlistTracks[0].Album)
                                && playlistTracks[0].Album != "Unknown Album")
                            {
                                name = playlistTracks[0].Album;
                            }

                            var id = await _libraryService.CreatePlaylistAsync(name);

                            // Update with YouTube metadata
                            await _libraryService.UpdatePlaylistAsync(new PlaylistInfo
                            {
                                Id = id,
                                Name = name,
                                YouTubePlaylistId = youTubePlaylistId,
                                SourceType = YouTube,
                                CoverPath = playlistTracks[0]?.CoverPath
                            });

                            // Add all tracks
                            var trackFilePaths = playlistTracks
                                .Where(t => !string.IsNullOrEmpty(t.FilePath))
                                .Select(t => t.FilePath)
                                .ToList();
                            await _libraryService.AddTracksToPlaylistAsync(id, trackFilePaths);

                            // Load the newly created playlist
                            var loaded = await _libraryService.GetPlaylistAsync(id);
                            if (loaded != null)
                            {
                                ApplyPlaylistInfo(loaded);
                            }
                            return;
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"[PlaylistViewModel] YouTube playlist fetch failed: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PlaylistViewModel] LoadYouTubePlaylist failed: {ex.Message}");
            }
            finally
            {
                TotalDuration = TimeSpan.FromTicks(Tracks.Sum(t => t.Duration.Ticks));
                IsLoading = false;
                OnPropertyChanged(nameof(TotalDurationText));
                OnPropertyChanged(nameof(SongsCountText));
                OnPropertyChanged(nameof(CreatedAtText));
            }
        }

        private void ApplyPlaylistInfo(PlaylistInfo playlist)
        {
            PlaylistId = playlist.Id;
            PlaylistName = playlist.Name;
            CoverPath = playlist.CoverPath;
            Description = playlist.Description;
            YouTubePlaylistId = playlist.YouTubePlaylistId;
            SourceType = playlist.SourceType;
            CreatedAt = playlist.CreatedAt;

            Tracks.Clear();
            foreach (var track in playlist.Tracks)
                Tracks.Add(track);
        }

        private void ExecutePlayAll()
        {
            if (Tracks.Count > 0)
                _playback.SetQueue(Tracks, startPlaying: true);
        }

        private void ExecuteShuffle()
        {
            if (Tracks.Count > 0)
            {
                _playback.ShuffleQueue();
                _playback.SetQueue(Tracks, startPlaying: true);
            }
        }

        private async Task ExecuteRemoveTrackAsync(object parameter)
        {
            if (!(parameter is MusicFile track)) return;
            if (string.IsNullOrEmpty(PlaylistId)) return;

            try
            {
                await _libraryService.RemoveTrackFromPlaylistAsync(PlaylistId, track.FilePath);
                Tracks.Remove(track);
                TotalDuration = TimeSpan.FromTicks(Tracks.Sum(t => t.Duration.Ticks));
                OnPropertyChanged(nameof(TotalDurationText));
                OnPropertyChanged(nameof(SongsCountText));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PlaylistViewModel] RemoveTrack failed: {ex.Message}");
            }
        }

        private async Task ExecuteDeletePlaylistAsync()
        {
            if (string.IsNullOrEmpty(PlaylistId)) return;

            var result = System.Windows.MessageBox.Show(
                $"Delete playlist \"{PlaylistName}\"? This cannot be undone.",
                "Delete Playlist",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning);

            if (result != System.Windows.MessageBoxResult.Yes) return;

            try
            {
                await _libraryService.DeletePlaylistAsync(PlaylistId);
                PlaylistDeleted?.Invoke();
                RequestGoBack?.Invoke();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PlaylistViewModel] DeletePlaylist failed: {ex.Message}");
            }
        }

        private void ExecuteRenamePlaylist()
        {
            if (string.IsNullOrEmpty(PlaylistId)) return;

            var dialog = new Musicefy.Views.RenamePlaylistWindow(PlaylistName)
            {
                Owner = System.Windows.Application.Current?.MainWindow
            };

            if (dialog.ShowDialog() == true)
            {
                var newName = dialog.ResultPlaylistName;
                if (!string.IsNullOrWhiteSpace(newName) && newName != PlaylistName)
                {
                    PlaylistName = newName;
                    _ = _libraryService.RenamePlaylistAsync(PlaylistId, newName);
                }
            }
        }
    }
}

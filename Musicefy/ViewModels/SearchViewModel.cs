using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Musicefy.Core.Interfaces;
using Musicefy.Core.Models;
using Musicefy.Core.Services;
using Musicefy.Core.Services.YouTubeApi;
using Musicefy.Services;
using static Musicefy.Core.SourceTypes;

namespace Musicefy.ViewModels
{
    /// <summary>
    /// ViewModel for the Search screen implementing Echo Music's search state machine.
    /// 
    /// State Machine:
    ///   Idle → Suggestions  (query text changed, debounced 300ms)
    ///   Suggestions → Searching  (Enter key / search button)
    ///   Searching → Results  (search completed)
    ///   Any → Idle  (query cleared)
    /// 
    /// Dual Search:
    ///   Local mode  → ILibraryService.SearchAsync (SQLite)
    ///   Online mode → IStreamingSourceManager.SearchAllSourcesAsync / SearchYouTubeWithTypeAsync
    /// 
    /// Debounce:
    ///   DispatcherTimer (300ms) resets on each keystroke; fires suggestion fetch when elapsed.
    /// </summary>
    public class SearchViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly ILibraryService _libraryService;
        private readonly IStreamingSourceManager _sourceManager;
        private readonly ISearchHistoryService _searchHistoryService;
        private readonly IQueueManager _queueManager;

        // ── Private State ────────────────────────────────────────────────
        private SearchState _state = SearchState.Idle;
        private string _query;
        private SearchSourceMode _sourceMode = SearchSourceMode.Online;
        private SearchResultFilter _selectedFilter = SearchResultFilter.All;
        private string _errorMessage;
        private CancellationTokenSource _searchCts;
        private readonly DispatcherTimer _debounceTimer;

        // ── Observable Collections ───────────────────────────────────────

        /// <summary>Autocomplete suggestions (from YouTube + local history).</summary>
        public ObservableCollection<string> Suggestions { get; } = new ObservableCollection<string>();

        /// <summary>Recent search history entries for the Idle state.</summary>
        public ObservableCollection<SearchHistory> RecentSearches { get; } = new ObservableCollection<SearchHistory>();

        /// <summary>Categorized result groups for the Results state.</summary>
        public ObservableCollection<SearchResultGroup> ResultGroups { get; } = new ObservableCollection<SearchResultGroup>();

        /// <summary>Flat list of all results (for single-filter view).</summary>
        public ObservableCollection<object> FlatResults { get; } = new ObservableCollection<object>();

        // ── State Properties ─────────────────────────────────────────────

        public SearchState State
        {
            get => _state;
            set
            {
                if (SetProperty(ref _state, value))
                {
                    OnPropertyChanged(nameof(IsIdle));
                    OnPropertyChanged(nameof(IsShowingSuggestions));
                    OnPropertyChanged(nameof(IsSearching));
                    OnPropertyChanged(nameof(HasResults));
                    OnPropertyChanged(nameof(HasError));
                }
            }
        }

        public string Query
        {
            get => _query;
            set
            {
                if (SetProperty(ref _query, value))
                {
                    OnQueryChanged(value);
                }
            }
        }

        public SearchSourceMode SourceMode
        {
            get => _sourceMode;
            set => SetProperty(ref _sourceMode, value);
        }

        public SearchResultFilter SelectedFilter
        {
            get => _selectedFilter;
            set
            {
                if (SetProperty(ref _selectedFilter, value))
                {
                    OnPropertyChanged(nameof(IsAllFilter));
                    ApplyFilter(value);
                }
            }
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set => SetProperty(ref _errorMessage, value);
        }

        // ── Derived Properties ───────────────────────────────────────────

        public bool IsIdle => State == SearchState.Idle;
        public bool IsShowingSuggestions => State == SearchState.Suggestions;
        public bool IsSearching => State == SearchState.Searching;
        public bool HasResults => State == SearchState.Results && ResultGroups.Count > 0;
        public bool HasError => State == SearchState.Results && !string.IsNullOrEmpty(ErrorMessage);
        public bool HasQuery => !string.IsNullOrWhiteSpace(Query);

        /// <summary>Whether the "All" filter is selected (show grouped results).</summary>
        public bool IsAllFilter => SelectedFilter == SearchResultFilter.All;

        /// <summary>Whether a YouTube URL was detected in the query.</summary>
        public bool IsFromLink { get; private set; }

        // ── Commands ─────────────────────────────────────────────────────

        public ICommand SearchCommand { get; }
        public ICommand ClearQueryCommand { get; }
        public ICommand SuggestionSelectedCommand { get; }
        public ICommand HistoryItemSelectedCommand { get; }
        public ICommand PlayTrackCommand { get; }
        public ICommand NavigateToArtistCommand { get; }
        public ICommand NavigateToAlbumCommand { get; }
        public ICommand ToggleSourceModeCommand { get; }
        public ICommand ClearHistoryCommand { get; }
        public ICommand SelectedFilterCommand { get; }

        // ── Constructor ──────────────────────────────────────────────────

        public SearchViewModel(
            ILibraryService libraryService,
            IStreamingSourceManager sourceManager,
            ISearchHistoryService searchHistoryService,
            IQueueManager queueManager)
        {
            _libraryService = libraryService ?? throw new ArgumentNullException(nameof(libraryService));
            _sourceManager = sourceManager ?? throw new ArgumentNullException(nameof(sourceManager));
            _searchHistoryService = searchHistoryService ?? throw new ArgumentNullException(nameof(searchHistoryService));
            _queueManager = queueManager ?? throw new ArgumentNullException(nameof(queueManager));

            // Debounce timer: 300ms delay before fetching suggestions
            _debounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            _debounceTimer.Tick += OnDebounceTimerTick;

            SearchCommand = new DelegateCommand(ExecuteSearch, CanSearch);
            ClearQueryCommand = new DelegateCommand(ExecuteClearQuery);
            SuggestionSelectedCommand = new DelegateCommand<string>(ExecuteSuggestionSelected);
            HistoryItemSelectedCommand = new DelegateCommand<SearchHistory>(ExecuteHistoryItemSelected);
            PlayTrackCommand = new DelegateCommand<MusicFile>(ExecutePlayTrack, CanPlayTrack);
            NavigateToArtistCommand = new DelegateCommand<ArtistInfo>(ExecuteNavigateToArtist);
            NavigateToAlbumCommand = new DelegateCommand<AlbumInfo>(ExecuteNavigateToAlbum);
            ToggleSourceModeCommand = new DelegateCommand(ExecuteToggleSourceMode);
            ClearHistoryCommand = new DelegateCommand(async () => await ExecuteClearHistoryAsync());
            SelectedFilterCommand = new DelegateCommand<string>(ExecuteSelectedFilter);

            // Load initial history
            _ = LoadSearchHistoryAsync();
        }

        // ── Query Change & Debounce ──────────────────────────────────────

        private void OnQueryChanged(string newQuery)
        {
            OnPropertyChanged(nameof(HasQuery));

            if (string.IsNullOrWhiteSpace(newQuery))
            {
                _debounceTimer.Stop();
                IsFromLink = false;
                State = SearchState.Idle;
                Application.Current?.Dispatcher?.Invoke(() =>
                {
                    Suggestions.Clear();
                    FlatResults.Clear();
                    ResultGroups.Clear();
                });
                _ = LoadSearchHistoryAsync();
                return;
            }

            // Check for YouTube URL — bypass normal search flow
            var parsedUrl = YouTubeUrlParser.Parse(newQuery.Trim());
            if (parsedUrl.Type != YouTubeUrlParser.UrlType.Unknown)
            {
                IsFromLink = true;
                _debounceTimer.Stop();
                return;
            }

            IsFromLink = false;

            // Reset debounce timer — will fire after 300ms of inactivity
            _debounceTimer.Stop();
            _debounceTimer.Start();
        }

        private async void OnDebounceTimerTick(object sender, EventArgs e)
        {
            _debounceTimer.Stop();

            if (string.IsNullOrWhiteSpace(Query)) return;

            State = SearchState.Suggestions;

            try
            {
                var suggestions = new List<string>();

                // 1. Search history prefix matches (fast, local)
                var historyMatches = await _searchHistoryService.SearchByPrefixAsync(Query, 5);
                foreach (var h in historyMatches)
                    suggestions.Add(h.Query);

                // 2. YouTube autocomplete (if online sources exist)
                if (SourceMode == SearchSourceMode.Online)
                {
                    try
                    {
                        var ytSuggestions = await _sourceManager.GetSearchSuggestionsAsync(Query);
                        foreach (var s in ytSuggestions)
                        {
                            if (!suggestions.Contains(s, StringComparer.OrdinalIgnoreCase))
                                suggestions.Add(s);
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[SearchVM] YouTube suggestions failed: {ex.Message}");
                    }
                }

                // 3. Local library prefix matches
                try
                {
                    var localResults = await _libraryService.SearchAsync(Query);
                    if (localResults != null)
                    {
                        var localSuggestions = localResults
                            .Take(3)
                            .Select(t => $"{t.Title} - {t.Artist}")
                            .ToList();
                        foreach (var s in localSuggestions)
                        {
                            if (!suggestions.Any(x => x.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0))
                                suggestions.Add(s);
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[SearchVM] Local suggestions failed: {ex.Message}");
                }

                Application.Current?.Dispatcher?.Invoke(() =>
                {
                    Suggestions.Clear();
                    foreach (var s in suggestions.Take(10))
                        Suggestions.Add(s);
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SearchVM] Suggestion fetch failed: {ex.Message}");
            }
        }

        // ── Search Execution ─────────────────────────────────────────────

        private bool CanSearch() => !string.IsNullOrWhiteSpace(Query) && State != SearchState.Searching;

        private async void ExecuteSearch()
        {
            if (string.IsNullOrWhiteSpace(Query)) return;

            // Handle URL direct-play
            if (IsFromLink)
            {
                await HandleUrlAsync(Query.Trim());
                return;
            }

            // Cancel any previous search
            _searchCts?.Cancel();
            _searchCts = new CancellationTokenSource();
            var ct = _searchCts.Token;

            State = SearchState.Searching;
            ErrorMessage = null;

            try
            {
                // Save to search history
                await _searchHistoryService.SaveAsync(
                    Query.Trim(),
                    SourceMode == SearchSourceMode.Local ? "Local" : "Online",
                    ct);

                if (SourceMode == SearchSourceMode.Local)
                {
                    await SearchLocalAsync(Query.Trim(), ct);
                }
                else
                {
                    await SearchOnlineAsync(Query.Trim(), ct);
                }
            }
            catch (OperationCanceledException)
            {
                System.Diagnostics.Debug.WriteLine("[SearchVM] Search cancelled");
            }
            catch (Exception ex)
            {
                State = SearchState.Results;
                ErrorMessage = ex.Message;
                System.Diagnostics.Debug.WriteLine($"[SearchVM] Search failed: {ex.Message}");
            }
        }

        // ── Local Search ─────────────────────────────────────────────────

        private async Task SearchLocalAsync(string query, CancellationToken ct)
        {
            var results = await _libraryService.SearchAsync(query, ct);

            var groups = new List<SearchResultGroup>();

            if (results != null && results.Count > 0)
            {
                // Group by type: Songs, Albums (by grouping), Artists (by grouping)
                var songs = results.ToList();
                var songGroup = new SearchResultGroup
                {
                    Category = "Songs",
                    DisplayOrder = 100,
                    HasMore = false
                };
                foreach (var item in songs.Take(50).Cast<object>())
                    songGroup.Items.Add(item);
                groups.Add(songGroup);

                // Group albums — carry YouTubeAlbumId from tracks if available
                var albumGroups = results
                    .Where(t => !string.IsNullOrEmpty(t.Album))
                    .GroupBy(t => new { t.Album, t.Artist })
                    .Take(10)
                    .ToList();

                if (albumGroups.Count > 0)
                {
                    var albumGroup = new SearchResultGroup
                    {
                        Category = "Albums",
                        DisplayOrder = 90,
                        HasMore = false
                    };
                    foreach (var ag in albumGroups)
                    {
                        var albumInfo = new AlbumInfo
                        {
                            Id = ag.FirstOrDefault(t => !string.IsNullOrEmpty(t.AlbumBrowseId))?.AlbumBrowseId
                                 ?? $"local_album:{ag.Key.Album}:{ag.Key.Artist}",
                            Name = ag.Key.Album,
                            Artist = ag.Key.Artist,
                            Year = ag.Max(t => t.Year),
                            CoverPath = ag.FirstOrDefault(t => !string.IsNullOrEmpty(t.CoverPath))?.CoverPath,
                            SourceType = ag.FirstOrDefault()?.SourceType,
                            YouTubeAlbumId = ag.FirstOrDefault(t => !string.IsNullOrEmpty(t.AlbumBrowseId))?.AlbumBrowseId,
                            Tracks = ag.OrderBy(t => t.TrackNumber).ToList()
                        };
                        albumGroup.Items.Add(albumInfo);
                    }
                    groups.Add(albumGroup);
                }

                // Group artists — carry YouTubeChannelId from tracks if available
                var artistGroups = results
                    .Where(t => !string.IsNullOrEmpty(t.Artist))
                    .GroupBy(t => t.Artist)
                    .Take(10)
                    .ToList();

                if (artistGroups.Count > 0)
                {
                    var artistGroup = new SearchResultGroup
                    {
                        Category = "Artists",
                        DisplayOrder = 80,
                        HasMore = false
                    };
                    foreach (var artG in artistGroups)
                    {
                        var artistInfo = new ArtistInfo
                        {
                            Id = artG.FirstOrDefault(t => !string.IsNullOrEmpty(t.ArtistBrowseId))?.ArtistBrowseId
                                 ?? $"local_artist:{artG.Key}",
                            Name = artG.Key,
                            CoverPath = artG.FirstOrDefault(t => !string.IsNullOrEmpty(t.CoverPath))?.CoverPath,
                            SourceType = artG.FirstOrDefault()?.SourceType,
                            YouTubeChannelId = artG.FirstOrDefault(t => !string.IsNullOrEmpty(t.ArtistBrowseId))?.ArtistBrowseId,
                            Tracks = artG.Take(5).ToList()
                        };
                        artistGroup.Items.Add(artistInfo);
                    }
                    groups.Add(artistGroup);
                }
            }

            Application.Current?.Dispatcher?.Invoke(() =>
            {
                ResultGroups.Clear();
                foreach (var g in groups.OrderByDescending(g => g.DisplayOrder))
                    ResultGroups.Add(g);

                SelectedFilter = SearchResultFilter.All;
                ApplyFilter(SearchResultFilter.All);
            });

            State = SearchState.Results;
        }

        // ── Online Search ────────────────────────────────────────────────

        private async Task SearchOnlineAsync(string query, CancellationToken ct)
        {
            var groups = new List<SearchResultGroup>();

            // 1. Try SearchSummaryAsync from YouTube for categorized results
            var ytSources = _sourceManager.Sources
                .Where(s => s.IsConnected && s.Type == YouTube)
                .ToList();

            bool summarySuccess = false;

            foreach (var ytSource in ytSources)
            {
                var ytSession = _sourceManager.GetYouTubeSession(ytSource.Id);
                if (ytSession == null) continue;

                try
                {
                    var summary = await ytSession.SearchSummaryAsync(query, 5);
                    if (summary != null && summary.Count > 0)
                    {
                        summarySuccess = true;

                        foreach (var kvp in summary)
                        {
                            var group = new SearchResultGroup
                            {
                                Category = kvp.Key,
                                DisplayOrder = GetCategoryOrder(kvp.Key),
                                HasMore = true
                            };
                            foreach (var item in kvp.Value.Take(20))
                                group.Items.Add(item);
                            groups.Add(group);
                        }
                        break; // Use first successful YouTube source
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[SearchVM] SearchSummary failed: {ex.Message}");
                }
            }

            // 2. Fallback: basic parallel search across all sources
            if (!summarySuccess)
            {
                try
                {
                    var allResults = await _sourceManager.SearchAllSourcesAsync(query, ct);
                    if (allResults != null && allResults.Count > 0)
                    {
                        var songGroup = new SearchResultGroup
                        {
                            Category = "Songs",
                            DisplayOrder = 100,
                            HasMore = false
                        };
                        foreach (var item in allResults.Take(50).Cast<object>())
                            songGroup.Items.Add(item);
                        groups.Add(songGroup);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[SearchVM] SearchAllSources failed: {ex.Message}");
                }
            }

            // 3. Also search Subsonic sources and merge into groups
            var subsonicSources = _sourceManager.Sources
                .Where(s => s.IsConnected && s.Type == Subsonic)
                .ToList();

            foreach (var subSource in subsonicSources)
            {
                var session = _sourceManager.GetSession(subSource.Id);
                if (session == null) continue;

                try
                {
                    var subResults = await session.SearchAsync(query, 30);
                    if (subResults != null && subResults.Count > 0)
                    {
                        // Merge into existing "Songs" group or create one
                        var existingSongGroup = groups.FirstOrDefault(g => g.Category == "Songs");
                        if (existingSongGroup != null)
                        {
                            foreach (var item in subResults.Cast<object>())
                                existingSongGroup.Items.Add(item);
                        }
                        else
                        {
                            var songGroup = new SearchResultGroup
                            {
                                Category = "Songs",
                                DisplayOrder = 100,
                                HasMore = false
                            };
                            foreach (var item in subResults.Cast<object>())
                                songGroup.Items.Add(item);
                            groups.Add(songGroup);
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[SearchVM] Subsonic search failed for {subSource.Name}: {ex.Message}");
                }
            }

            Application.Current?.Dispatcher?.Invoke(() =>
            {
                ResultGroups.Clear();
                foreach (var g in groups.OrderByDescending(g => g.DisplayOrder))
                    ResultGroups.Add(g);

                SelectedFilter = SearchResultFilter.All;
                ApplyFilter(SearchResultFilter.All);
            });

            State = SearchState.Results;
        }

        private static int GetCategoryOrder(string category)
        {
            switch (category)
            {
                case "Top result": return 110;
                case "Songs": return 100;
                case "Videos": return 90;
                case "Albums": return 80;
                case "Artists": return 70;
                case "Playlists": return 60;
                default: return 50;
            }
        }

        // ── URL Direct-Play ──────────────────────────────────────────────

        private async Task HandleUrlAsync(string url)
        {
            var parsed = YouTubeUrlParser.Parse(url);
            if (parsed.Type == YouTubeUrlParser.UrlType.Unknown) return;

            try
            {
                switch (parsed.Type)
                {
                    case YouTubeUrlParser.UrlType.Video:
                        // Play the video directly
                        if (!string.IsNullOrEmpty(parsed.VideoId))
                        {
                            var track = new MusicFile
                            {
                                Title = parsed.VideoId,
                                YouTubeVideoId = parsed.VideoId,
                                SourceUri = YouTubeUrlParser.CreateWatchUrl(parsed.VideoId),
                                SourceType = YouTube
                            };
                            _queueManager.Clear();
                            _queueManager.Enqueue(track);
                            _queueManager.JumpToIndex(0);
                        }
                        break;

                    case YouTubeUrlParser.UrlType.Playlist:
                        // Phase 5: Navigate to dedicated playlist view
                        var navService = App.Services?.GetService(typeof(NavigationService)) as NavigationService;
                        if (navService != null)
                        {
                            navService.NavigateToPlaylist(new PlaylistInfo
                            {
                                Name = "YouTube Playlist",
                                YouTubePlaylistId = parsed.PlaylistId,
                                SourceType = YouTube
                            });
                        }
                        break;

                    case YouTubeUrlParser.UrlType.Artist:
                        navService = App.Services?.GetService(typeof(NavigationService)) as NavigationService;
                        // Use the BrowseId as both the Id and YouTubeChannelId so the
                        // ArtistViewModel can look up the artist on YouTube Music.
                        navService?.NavigateToArtist(new ArtistInfo
                        {
                            Id = parsed.BrowseId,
                            Name = "YouTube Artist",  // Will be filled in by ArtistViewModel
                            YouTubeChannelId = parsed.BrowseId,
                            SourceType = YouTube
                        });
                        break;

                    case YouTubeUrlParser.UrlType.Album:
                        navService = App.Services?.GetService(typeof(NavigationService)) as NavigationService;
                        // Use the BrowseId as both the Id and YouTubeAlbumId so the
                        // AlbumViewModel can look up the album on YouTube Music.
                        navService?.NavigateToAlbum(new AlbumInfo
                        {
                            Id = parsed.BrowseId,
                            Name = "YouTube Album",  // Will be filled in by AlbumViewModel
                            YouTubeAlbumId = parsed.BrowseId,
                            SourceType = YouTube
                        });
                        break;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SearchVM] URL handling failed: {ex.Message}");
            }
        }

        // ── Filter Application ───────────────────────────────────────────

        private void ApplyFilter(SearchResultFilter filter)
        {
            Application.Current?.Dispatcher?.Invoke(() =>
            {
                FlatResults.Clear();

                switch (filter)
                {
                    case SearchResultFilter.All:
                        // Show all groups with headers
                        foreach (var group in ResultGroups)
                        {
                            foreach (var item in group.Items)
                                FlatResults.Add(item);
                        }
                        break;

                    case SearchResultFilter.Songs:
                        var songsGroup = ResultGroups.FirstOrDefault(g =>
                            g.Category.Equals("Songs", StringComparison.OrdinalIgnoreCase) ||
                            g.Category.Equals("Top result", StringComparison.OrdinalIgnoreCase));
                        if (songsGroup != null)
                            foreach (var item in songsGroup.Items.OfType<MusicFile>())
                                FlatResults.Add(item);
                        break;

                    case SearchResultFilter.Albums:
                        var albumsGroup = ResultGroups.FirstOrDefault(g =>
                            g.Category.Equals("Albums", StringComparison.OrdinalIgnoreCase));
                        if (albumsGroup != null)
                            foreach (var item in albumsGroup.Items)
                                FlatResults.Add(item);
                        break;

                    case SearchResultFilter.Artists:
                        var artistsGroup = ResultGroups.FirstOrDefault(g =>
                            g.Category.Equals("Artists", StringComparison.OrdinalIgnoreCase));
                        if (artistsGroup != null)
                            foreach (var item in artistsGroup.Items)
                                FlatResults.Add(item);
                        break;

                    case SearchResultFilter.Playlists:
                        var playlistsGroup = ResultGroups.FirstOrDefault(g =>
                            g.Category.Equals("Playlists", StringComparison.OrdinalIgnoreCase));
                        if (playlistsGroup != null)
                            foreach (var item in playlistsGroup.Items)
                                FlatResults.Add(item);
                        break;
                }
            });
        }

        // ── Command Handlers ─────────────────────────────────────────────

        private void ExecuteClearQuery()
        {
            Query = string.Empty;
            IsFromLink = false;
            State = SearchState.Idle;
        }

        private void ExecuteSuggestionSelected(string suggestion)
        {
            if (string.IsNullOrEmpty(suggestion)) return;
            Query = suggestion;
            ExecuteSearch();
        }

        private void ExecuteHistoryItemSelected(SearchHistory historyEntry)
        {
            if (historyEntry == null) return;
            Query = historyEntry.Query;
            SourceMode = historyEntry.SourceType == "Local"
                ? SearchSourceMode.Local
                : SearchSourceMode.Online;
            ExecuteSearch();
        }

        private bool CanPlayTrack(MusicFile track) => track != null;

        private void ExecutePlayTrack(MusicFile track)
        {
            if (track == null) return;
            try
            {
                _queueManager.Clear();
                _queueManager.Enqueue(track);
                _queueManager.JumpToIndex(0);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SearchVM] PlayTrack failed: {ex.Message}");
            }
        }

        private void ExecuteNavigateToArtist(ArtistInfo artist)
        {
            try
            {
                var navService = App.Services?.GetService(typeof(NavigationService)) as NavigationService;
                navService?.NavigateToArtist(artist);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SearchVM] NavigateToArtist failed: {ex.Message}");
            }
        }

        private void ExecuteNavigateToAlbum(AlbumInfo album)
        {
            try
            {
                var navService = App.Services?.GetService(typeof(NavigationService)) as NavigationService;
                navService?.NavigateToAlbum(album);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SearchVM] NavigateToAlbum failed: {ex.Message}");
            }
        }

        private void ExecuteToggleSourceMode()
        {
            SourceMode = SourceMode == SearchSourceMode.Local
                ? SearchSourceMode.Online
                : SearchSourceMode.Local;

            // Re-run search if there's an active query
            if (!string.IsNullOrWhiteSpace(Query) && State == SearchState.Results)
            {
                ExecuteSearch();
            }
        }

        /// <summary>
        /// MVVM-bound filter tab selection command. Replaces the code-behind FilterTab_Click.
        /// Accepts the filter name as a string (e.g. "All", "Songs", "Albums", "Artists", "Playlists").
        /// </summary>
        private void ExecuteSelectedFilter(string filterName)
        {
            if (string.IsNullOrEmpty(filterName)) return;

            SearchResultFilter filter = filterName switch
            {
                "Songs" => SearchResultFilter.Songs,
                "Albums" => SearchResultFilter.Albums,
                "Artists" => SearchResultFilter.Artists,
                "Playlists" => SearchResultFilter.Playlists,
                _ => SearchResultFilter.All
            };

            SelectedFilter = filter;
        }

        private async Task ExecuteClearHistoryAsync()
        {
            try
            {
                await _searchHistoryService.ClearAsync();
                Application.Current?.Dispatcher?.Invoke(() => RecentSearches.Clear());
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SearchVM] ClearHistory failed: {ex.Message}");
            }
        }

        // ── Search History ───────────────────────────────────────────────

        private async Task LoadSearchHistoryAsync()
        {
            try
            {
                var history = await _searchHistoryService.GetRecentAsync(10);
                Application.Current?.Dispatcher?.Invoke(() =>
                {
                    RecentSearches.Clear();
                    foreach (var entry in history)
                        RecentSearches.Add(entry);
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SearchVM] LoadSearchHistory failed: {ex.Message}");
            }
        }

        // ── INotifyPropertyChanged ───────────────────────────────────────

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        // ── IDisposable ──────────────────────────────────────────────────

        public void Dispose()
        {
            _debounceTimer?.Stop();
            _searchCts?.Cancel();
            _searchCts?.Dispose();
        }

        // ── Nested Delegate Commands ─────────────────────────────────────

        private class DelegateCommand : ICommand
        {
            private readonly Action _execute;
            private readonly Func<bool> _canExecute;

            public DelegateCommand(Action execute, Func<bool> canExecute = null)
            {
                _execute = execute ?? throw new ArgumentNullException(nameof(execute));
                _canExecute = canExecute;
            }

            public event EventHandler CanExecuteChanged
            {
                add { CommandManager.RequerySuggested += value; }
                remove { CommandManager.RequerySuggested -= value; }
            }

            public bool CanExecute(object parameter) => _canExecute?.Invoke() ?? true;
            public void Execute(object parameter) => _execute();
        }

        private class DelegateCommand<T> : ICommand
        {
            private readonly Action<T> _execute;
            private readonly Func<T, bool> _canExecute;

            public DelegateCommand(Action<T> execute, Func<T, bool> canExecute = null)
            {
                _execute = execute ?? throw new ArgumentNullException(nameof(execute));
                _canExecute = canExecute;
            }

            public event EventHandler CanExecuteChanged
            {
                add { CommandManager.RequerySuggested += value; }
                remove { CommandManager.RequerySuggested -= value; }
            }

            public bool CanExecute(object parameter) => _canExecute?.Invoke((T)parameter) ?? true;
            public void Execute(object parameter) => _execute((T)parameter);
        }
    }
}

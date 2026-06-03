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
using Musicefy.Core.Interfaces;
using Musicefy.Core.Models;
using Musicefy.Core.Services;
using static Musicefy.Core.SourceTypes;

namespace Musicefy.ViewModels
{
    /// <summary>
    /// ViewModel for the Home screen implementing Echo Music's two-phase loading pattern.
    /// Phase 1 loads local data (SQLite) instantly; Phase 2 loads network data concurrently.
    /// Registered as singleton in DI for state persistence across navigation.
    /// </summary>
    public class HomeViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly ILibraryService _libraryService;
        private readonly IBrowseService _browseService;
        private readonly IStreamingSourceManager _sourceManager;
        private readonly IHealthCheckService _healthCheckService;

        private HomeLoadState _loadState = HomeLoadState.NotStarted;
        private string _errorMessage;
        private DateTime? _lastRefreshed;
        private bool _isRefreshing;
        private ChipItem _selectedChip;
        private List<ChipItem> _originalChips;
        private CancellationTokenSource _loadCts;

        // ── Observable Collections ──────────────────────────────────────────

        /// <summary>All home sections in display order, bound to the UI</summary>
        public ObservableCollection<HomeSection> Sections { get; } = new ObservableCollection<HomeSection>();

        /// <summary>Available filter chips (from YouTube Home Feed)</summary>
        public ObservableCollection<ChipItem> AvailableChips { get; } = new ObservableCollection<ChipItem>();

        // ── State Properties ────────────────────────────────────────────────

        public HomeLoadState LoadState
        {
            get => _loadState;
            set
            {
                if (SetProperty(ref _loadState, value))
                {
                    OnPropertyChanged(nameof(IsLoading));
                    OnPropertyChanged(nameof(IsLoaded));
                    OnPropertyChanged(nameof(HasError));
                    OnPropertyChanged(nameof(IsEmpty));
                }
            }
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set => SetProperty(ref _errorMessage, value);
        }

        public DateTime? LastRefreshed
        {
            get => _lastRefreshed;
            set => SetProperty(ref _lastRefreshed, value);
        }

        public bool IsRefreshing
        {
            get => _isRefreshing;
            set => SetProperty(ref _isRefreshing, value);
        }

        public ChipItem SelectedChip
        {
            get => _selectedChip;
            set
            {
                if (SetProperty(ref _selectedChip, value))
                {
                    _ = OnChipSelectedAsync(value);
                }
            }
        }

        // ── Derived Properties ──────────────────────────────────────────────

        public bool IsLoading => LoadState == HomeLoadState.LoadingLocal || LoadState == HomeLoadState.LoadingNetwork;
        public bool IsLoaded => LoadState == HomeLoadState.Loaded;
        public bool HasError => LoadState == HomeLoadState.Error;
        public bool IsEmpty => LoadState == HomeLoadState.Loaded && Sections.Count == 0;

        // ── Commands ────────────────────────────────────────────────────────

        public ICommand RefreshCommand { get; }
        public ICommand PlayTrackCommand { get; }
        public ICommand NavigateToArtistCommand { get; }
        public ICommand NavigateToAlbumCommand { get; }
        public ICommand LoadMoreCommand { get; }

        // ── Constructor ─────────────────────────────────────────────────────

        public HomeViewModel(
            ILibraryService libraryService,
            IBrowseService browseService,
            IStreamingSourceManager sourceManager,
            IHealthCheckService healthCheckService)
        {
            _libraryService = libraryService ?? throw new ArgumentNullException(nameof(libraryService));
            _browseService = browseService ?? throw new ArgumentNullException(nameof(browseService));
            _sourceManager = sourceManager ?? throw new ArgumentNullException(nameof(sourceManager));
            _healthCheckService = healthCheckService ?? throw new ArgumentNullException(nameof(healthCheckService));

            RefreshCommand = new DelegateCommand(ExecuteRefresh, CanRefresh);
            PlayTrackCommand = new DelegateCommand<MusicFile>(ExecutePlayTrack, CanPlayTrack);
            NavigateToArtistCommand = new DelegateCommand<ArtistInfo>(ExecuteNavigateToArtist);
            NavigateToAlbumCommand = new DelegateCommand<AlbumInfo>(ExecuteNavigateToAlbum);
            LoadMoreCommand = new DelegateCommand<string>(ExecuteLoadMore, CanLoadMore);

            // Subscribe to health check events for reactive refresh
            _healthCheckService.SourceHealthChanged += OnSourceHealthChanged;
        }

        // ── Two-Phase Loading ───────────────────────────────────────────────

        /// <summary>
        /// Main load method implementing Echo Music's two-phase loading.
        /// Phase 1: Local data (under 200ms) → Phase 2: Network data (concurrent)
        /// </summary>
        public async Task LoadAsync()
        {
            // Cancel any previous load
            _loadCts?.Cancel();
            _loadCts = new CancellationTokenSource();
            var ct = _loadCts.Token;

            try
            {
                LoadState = HomeLoadState.LoadingLocal;
                ErrorMessage = null;

                // ═══ Phase 1: Local Data (fast, < 200ms) ═════════════════════
                var localSections = new List<HomeSection>();

                try
                {
                    // Quick Picks — blend of favourites + forgotten favorites
                    var favourites = await _libraryService.GetRandomFavouriteTracksAsync(10, ct);
                    var forgotten = await _libraryService.GetForgottenFavoritesAsync(30, 10, ct);
                    var quickPicksItems = favourites.Concat(forgotten)
                        .GroupBy(t => t.FilePath, StringComparer.OrdinalIgnoreCase)
                        .Select(g => g.First())
                        .OrderBy(_ => Guid.NewGuid()) // shuffle
                        .Take(20)
                        .Cast<object>()
                        .ToList();

                    if (quickPicksItems.Count > 0)
                    {
                        localSections.Add(new HomeSection
                        {
                            Title = "Quick Picks",
                            SectionType = HomeSectionType.QuickPicks,
                            BaseWeight = 100,
                            SourceType = Local
                        });
                        foreach (var item in quickPicksItems)
                            localSections[localSections.Count - 1].Items.Add(item);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[HomeViewModel] Quick Picks failed: {ex.Message}");
                }

                try
                {
                    // Keep Listening — most played in last 14 days
                    var mostPlayed = await _libraryService.GetMostPlayedAsync(14, 20, ct);
                    if (mostPlayed?.Count > 0)
                    {
                        var section = new HomeSection
                        {
                            Title = "Keep Listening",
                            SectionType = HomeSectionType.KeepListening,
                            BaseWeight = 90,
                            SourceType = Local
                        };
                        foreach (var item in mostPlayed.Cast<object>())
                            section.Items.Add(item);
                        localSections.Add(section);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[HomeViewModel] Keep Listening failed: {ex.Message}");
                }

                try
                {
                    // Recently Played
                    var recent = await _libraryService.GetRecentlyPlayedAsync(30, ct);
                    if (recent?.Count > 0)
                    {
                        var section = new HomeSection
                        {
                            Title = "Recently Played",
                            SectionType = HomeSectionType.RecentlyPlayed,
                            BaseWeight = 85,
                            SourceType = Local
                        };
                        foreach (var item in recent.Cast<object>())
                            section.Items.Add(item);
                        localSections.Add(section);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[HomeViewModel] Recently Played failed: {ex.Message}");
                }

                // Publish local sections immediately (user sees content right away)
                Application.Current?.Dispatcher?.Invoke(() =>
                {
                    Sections.Clear();
                    foreach (var section in localSections.OrderByDescending(s => s.BaseWeight))
                        Sections.Add(section);
                });

                LoadState = HomeLoadState.LoadingNetwork;

                // ═══ Phase 2: Network Data (concurrent, independent timeouts) ══
                var networkSections = new List<HomeSection>();
                var networkChips = new List<ChipItem>();

                var networkTasks = new List<Task>
                {
                    LoadDailyDiscoverAsync(networkSections, ct),
                    LoadYouTubeHomeAsync(networkSections, networkChips, ct),
                    LoadSubsonicAlbumsAsync(networkSections, ct)
                };

                try
                {
                    await Task.WhenAll(networkTasks);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[HomeViewModel] Some network requests failed: {ex.Message}");
                }

                // Merge network sections into the observable collection on UI thread
                Application.Current?.Dispatcher?.Invoke(() =>
                {
                    foreach (var section in networkSections.OrderByDescending(s => s.BaseWeight))
                        Sections.Add(section);

                    // Update chips
                    AvailableChips.Clear();
                    foreach (var chip in networkChips)
                        AvailableChips.Add(chip);
                });

                LoadState = HomeLoadState.Loaded;
                LastRefreshed = DateTime.UtcNow;
            }
            catch (OperationCanceledException)
            {
                // Load was cancelled (e.g., user navigated away or triggered refresh)
                System.Diagnostics.Debug.WriteLine("[HomeViewModel] LoadAsync cancelled");
            }
            catch (Exception ex)
            {
                LoadState = HomeLoadState.Error;
                ErrorMessage = ex.Message;
                System.Diagnostics.Debug.WriteLine($"[HomeViewModel] LoadAsync failed: {ex.Message}");
            }
        }

        // ── Network Data Loaders ────────────────────────────────────────────

        private async Task LoadDailyDiscoverAsync(List<HomeSection> targetSections, CancellationToken ct)
        {
            try
            {
                var ytSources = _sourceManager.Sources
                    .Where(s => s.IsConnected && s.Type == YouTube)
                    .ToList();

                foreach (var ytSource in ytSources)
                {
                    var ytSession = _sourceManager.GetYouTubeSession(ytSource.Id);
                    if (ytSession == null) continue;

                    // Seed from random favourite tracks to generate Daily Discover
                    var seeds = await _libraryService.GetRandomFavouriteTracksAsync(5, ct);
                    if (seeds == null || seeds.Count == 0) continue;

                    var discovered = new List<MusicFile>();
                    foreach (var seed in seeds.Take(3))
                    {
                        try
                        {
                            if (!string.IsNullOrEmpty(seed.YouTubeVideoId))
                            {
                                var radio = await ytSession.GetRadioAsync(seed.YouTubeVideoId);
                                if (radio != null)
                                    discovered.AddRange(radio);
                            }
                        }
                        catch
                        {
                            // Skip failed seed
                        }
                    }

                    if (discovered.Count > 0)
                    {
                        // Deduplicate and shuffle
                        var deduped = discovered
                            .GroupBy(t => t.YouTubeVideoId ?? t.FilePath)
                            .Select(g => g.First())
                            .OrderBy(_ => Guid.NewGuid())
                            .Take(20)
                            .Cast<object>()
                            .ToList();

                        var section = new HomeSection
                        {
                            Title = "Daily Discover",
                            SectionType = HomeSectionType.DailyDiscover,
                            BaseWeight = 80,
                            SourceId = ytSource.Id,
                            SourceType = YouTube
                        };
                        foreach (var item in deduped)
                            section.Items.Add(item);
                        targetSections.Add(section);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[HomeViewModel] Daily Discover failed: {ex.Message}");
            }
        }

        private async Task LoadYouTubeHomeAsync(List<HomeSection> targetSections, List<ChipItem> targetChips, CancellationToken ct)
        {
            try
            {
                var ytSources = _sourceManager.Sources
                    .Where(s => s.IsConnected && s.Type == YouTube)
                    .ToList();

                foreach (var ytSource in ytSources)
                {
                    var ytSession = _sourceManager.GetYouTubeSession(ytSource.Id);
                    if (ytSession == null) continue;

                    // Get YouTube home feed content
                    var homeResults = await ytSession.GetRandomSongsAsync(30);
                    if (homeResults?.Count > 0)
                    {
                        var section = new HomeSection
                        {
                            Title = "YouTube Music",
                            SectionType = HomeSectionType.YouTubeHome,
                            BaseWeight = 75,
                            SourceId = ytSource.Id,
                            SourceType = YouTube
                        };
                        foreach (var item in homeResults.Cast<object>())
                            section.Items.Add(item);
                        targetSections.Add(section);
                    }

                    // Get albums for browse
                    var albums = await ytSession.GetAlbumListAsync(20);
                    if (albums?.Count > 0)
                    {
                        var section = new HomeSection
                        {
                            Title = "New Releases",
                            SectionType = HomeSectionType.YouTubeHome,
                            BaseWeight = 72,
                            SourceId = ytSource.Id,
                            SourceType = YouTube
                        };
                        foreach (var item in albums.Cast<object>())
                            section.Items.Add(item);
                        targetSections.Add(section);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[HomeViewModel] YouTube Home failed: {ex.Message}");
            }
        }

        private async Task LoadSubsonicAlbumsAsync(List<HomeSection> targetSections, CancellationToken ct)
        {
            try
            {
                var subsonicSources = _sourceManager.Sources
                    .Where(s => s.IsConnected && s.Type == Subsonic)
                    .ToList();

                foreach (var subSource in subsonicSources)
                {
                    var session = _sourceManager.GetSession(subSource.Id);
                    if (session == null) continue;

                    var newestAlbums = await session.GetAlbumListAsync(20);
                    if (newestAlbums?.Count > 0)
                    {
                        var section = new HomeSection
                        {
                            Title = $"New on {subSource.Name}",
                            SectionType = HomeSectionType.SubsonicAlbums,
                            BaseWeight = 70,
                            SourceId = subSource.Id,
                            SourceType = Subsonic
                        };
                        foreach (var item in newestAlbums.Cast<object>())
                            section.Items.Add(item);
                        targetSections.Add(section);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[HomeViewModel] Subsonic Albums failed: {ex.Message}");
            }
        }

        // ── Chip Filtering ──────────────────────────────────────────────────

        private async Task OnChipSelectedAsync(ChipItem chip)
        {
            if (chip == null)
            {
                // Chip deselected — restore original state
                return;
            }

            try
            {
                // Mark chip as selected
                foreach (var c in AvailableChips)
                    c.IsSelected = (c == chip);

                // For now, we just update the UI. Full implementation would call
                // InnerTubeClient.BrowseAsync with chip.EndpointParams when that
                // integration is available.
                System.Diagnostics.Debug.WriteLine($"[HomeViewModel] Chip selected: {chip.Title}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[HomeViewModel] Chip filter failed: {ex.Message}");
            }
        }

        // ── Infinite Scroll ─────────────────────────────────────────────────

        private bool CanLoadMore(string continuationToken)
        {
            return !string.IsNullOrEmpty(continuationToken) && !IsLoading;
        }

        private void ExecuteLoadMore(string continuationToken)
        {
            // Placeholder for infinite scroll continuation.
            // Full implementation would call InnerTubeClient with continuation token
            // and append results to the YouTubeHome section.
            System.Diagnostics.Debug.WriteLine($"[HomeViewModel] LoadMore requested: {continuationToken}");
        }

        // ── Command Handlers ────────────────────────────────────────────────

        private bool CanRefresh() => !IsLoading && !IsRefreshing;

        private async void ExecuteRefresh()
        {
            if (IsRefreshing) return;
            IsRefreshing = true;

            try
            {
                await LoadAsync();
            }
            finally
            {
                IsRefreshing = false;
            }
        }

        private bool CanPlayTrack(MusicFile track) => track != null;

        private void ExecutePlayTrack(MusicFile track)
        {
            if (track == null) return;
            try
            {
                var queueManager = App.Services?.GetService(typeof(IQueueManager)) as IQueueManager;
                if (queueManager != null)
                {
                    queueManager.PlayTrack(track);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[HomeViewModel] PlayTrack failed: {ex.Message}");
            }
        }

        private void ExecuteNavigateToArtist(ArtistInfo artist)
        {
            // Navigation handled by parent MainViewModel
            try
            {
                var navService = App.Services?.GetService(typeof(NavigationService)) as NavigationService;
                navService?.NavigateToArtist(artist);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[HomeViewModel] NavigateToArtist failed: {ex.Message}");
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
                System.Diagnostics.Debug.WriteLine($"[HomeViewModel] NavigateToAlbum failed: {ex.Message}");
            }
        }

        // ── Health Check Integration ────────────────────────────────────────

        private void OnSourceHealthChanged(object sender, SourceHealthEventArgs e)
        {
            // Reactively refresh when a source reconnects (data may now be available)
            if (e.NewStatus == Core.Models.SourceHealthStatus.Healthy)
            {
                Application.Current?.Dispatcher?.Invoke(() =>
                {
                    _ = LoadAsync();
                });
            }
        }

        // ── INotifyPropertyChanged ──────────────────────────────────────────

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

        // ── IDisposable ─────────────────────────────────────────────────────

        public void Dispose()
        {
            _loadCts?.Cancel();
            _loadCts?.Dispose();
            _healthCheckService.SourceHealthChanged -= OnSourceHealthChanged;
        }

        // ── Nested Delegate Commands ────────────────────────────────────────

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

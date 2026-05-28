using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Musicefy.Core.Interfaces;
using Musicefy.Core.Models;
using Musicefy.Core.Services;
using Musicefy.Properties;
using Musicefy.Services;
using static Musicefy.Core.SourceTypes;

namespace Musicefy.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly ILibraryService _libraryService;
        private readonly IStreamingSourceManager _sourceManager;
        private readonly IAudioPlayer _playback;

        public ObservableCollection<ChartCard> BrowseCharts { get; }
        public ObservableCollection<TrackCard> QuickPicks { get; }
        public ObservableCollection<TrackCard> RecentlyPlayed { get; }
        public ObservableCollection<TrackCard> FilteredQuickPicks { get; }
        public ObservableCollection<TrackCard> TopMusicVideos { get; }
        public ObservableCollection<MoodChip> MoodFilterChips { get; }

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set { _isLoading = value; OnPropertyChanged(); }
        }

        private bool _isEmpty = true;
        public bool IsEmpty
        {
            get => _isEmpty;
            set { _isEmpty = value; OnPropertyChanged(); }
        }

        private string _emptyMessage = "No music found. Add a library folder or connect a streaming source.";
        public string EmptyMessage
        {
            get => _emptyMessage;
            set { _emptyMessage = value; OnPropertyChanged(); }
        }

        private TrackCard _heroTrack;
        public TrackCard HeroTrack
        {
            get => _heroTrack;
            set { SetProperty(ref _heroTrack, value); OnPropertyChanged(nameof(HasHeroTrack)); }
        }

        public bool HasHeroTrack => HeroTrack != null;

        private string _selectedMoodFilter = "All";
        public string SelectedMoodFilter
        {
            get => _selectedMoodFilter;
            set
            {
                if (SetProperty(ref _selectedMoodFilter, value))
                    ApplyMoodFilter(value);
            }
        }

        private Color _dominantColor = Color.FromRgb(60, 140, 231);
        public Color DominantColor
        {
            get => _dominantColor;
            set { SetProperty(ref _dominantColor, value); UpdateHomeGradient(); }
        }

        private Color _vibrantColor = Color.FromRgb(60, 140, 231);
        public Color VibrantColor
        {
            get => _vibrantColor;
            set { SetProperty(ref _vibrantColor, value); }
        }

        private Color _mutedColor = Color.FromRgb(80, 100, 140);
        public Color MutedColor
        {
            get => _mutedColor;
            set { SetProperty(ref _mutedColor, value); }
        }

        private LinearGradientBrush _homeGradient;
        public LinearGradientBrush HomeGradient
        {
            get => _homeGradient;
            private set { SetProperty(ref _homeGradient, value); }
        }

        private CancellationTokenSource _reloadCts;

        public MainViewModel(ILibraryService libraryService, IStreamingSourceManager sourceManager, IAudioPlayer playback)
        {
            _libraryService = libraryService;
            _sourceManager = sourceManager;
            _playback = playback;

            BrowseCharts = new ObservableCollection<ChartCard>();
            QuickPicks = new ObservableCollection<TrackCard>();
            RecentlyPlayed = new ObservableCollection<TrackCard>();
            FilteredQuickPicks = new ObservableCollection<TrackCard>();
            TopMusicVideos = new ObservableCollection<TrackCard>();

            MoodFilterChips = new ObservableCollection<MoodChip>
            {
                new MoodChip { Name = "All", IsSelected = true, Keywords = "" },
                new MoodChip { Name = "Relax", Keywords = "chill,ambient,lofi,jazz,acoustic,soft" },
                new MoodChip { Name = "Feel Good", Keywords = "pop,upbeat,happy,dance,funk" },
                new MoodChip { Name = "Commute", Keywords = "rock,indie,alternative,electronic" },
                new MoodChip { Name = "Energize", Keywords = "electronic,dance,edm,rock,metal,hip-hop" },
            };
            foreach (var chip in MoodFilterChips)
                chip.PropertyChanged += OnMoodChipChanged;

            _playback.TrackChanged += OnPlaybackTrackChanged;

            UpdateHomeGradient();
        }

        public async Task ReloadAsync()
        {
            try { _reloadCts?.Cancel(); } catch (ObjectDisposedException) { }
            _reloadCts?.Dispose();
            _reloadCts = new CancellationTokenSource();
            var token = _reloadCts.Token;

            IsLoading = true;
            try
            {
                BrowseCharts.Clear();
                QuickPicks.Clear();
                RecentlyPlayed.Clear();
                FilteredQuickPicks.Clear();
                TopMusicVideos.Clear();

                var quickPicksTask = LoadQuickPicksAsync(token);
                var chartsTask = LoadBrowseChartsAsync(token);
                var videosTask = LoadTopMusicVideosAsync(token);
                var recentTask = LoadRecentlyPlayedAsync(token);

                await Task.WhenAll(quickPicksTask, chartsTask, videosTask, recentTask);

                IsEmpty = QuickPicks.Count == 0 && BrowseCharts.Count == 0 && TopMusicVideos.Count == 0;
                if (IsEmpty)
                {
                    bool hasSources = _sourceManager.Sources.Any(s => s.IsConnected);
                    bool hasLibraryTracks = (await _libraryService.GetAllTracksAsync(token))?.Count > 0;
                    if (hasLibraryTracks)
                        EmptyMessage = "All sources disabled in Discover settings. Enable some to see music here.";
                    else if (!hasSources)
                        EmptyMessage = "No music sources configured. Go to Sources settings to add a library folder or streaming service.";
                    else
                        EmptyMessage = "No tracks available from your current sources.";
                }

                ApplyMoodFilter(_selectedMoodFilter);
            }
            catch (OperationCanceledException) { }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task LoadRecentlyPlayedAsync(CancellationToken token)
        {
            if (token.IsCancellationRequested) return;
            if (!Settings.Default.DiscoverLibrary) return;

            var recent = await _libraryService.GetHistoryTracksAsync(10, token);
            if (token.IsCancellationRequested) return;
            foreach (var track in (recent ?? Enumerable.Empty<MusicFile>()).Take(8))
            {
                var card = CreateTrackCard(track);
                RecentlyPlayed.Add(card);
                if (HeroTrack == null)
                    HeroTrack = card;
            }
        }

        private void OnPlaybackTrackChanged(MusicFile track)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                if (track == null || string.IsNullOrEmpty(track.CoverPath))
                {
                    DominantColor = Color.FromRgb(60, 140, 231);
                    VibrantColor = Color.FromRgb(60, 140, 231);
                    MutedColor = Color.FromRgb(80, 100, 140);
                    return;
                }

                BitmapImage cover = null;
                if (System.IO.File.Exists(track.CoverPath))
                {
                    try
                    {
                        cover = new BitmapImage(new Uri(track.CoverPath, UriKind.Absolute));
                    }
                    catch { }
                }

                if (cover != null)
                {
                    try
                    {
                        var colors = ColorExtractor.Extract(cover);
                        DominantColor = colors.Primary;
                        VibrantColor = colors.Vibrant;
                        MutedColor = colors.Muted;
                    }
                    catch { }
                }
            });
        }

        private void OnMoodChipChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MoodChip.IsSelected) && sender is MoodChip chip && chip.IsSelected)
            {
                foreach (var other in MoodFilterChips)
                {
                    if (other != chip)
                        other.IsSelected = false;
                }
                SelectedMoodFilter = chip.Name;
                ApplyMoodFilter(chip.Keywords);
            }
        }

        private void ApplyMoodFilter(string keywords)
        {
            FilteredQuickPicks.Clear();
            var source = QuickPicks.ToList();

            if (string.IsNullOrEmpty(keywords))
            {
                foreach (var item in source)
                    FilteredQuickPicks.Add(item);
                return;
            }

            var kw = keywords.Split(',').Select(k => k.Trim().ToLower()).ToHashSet();
            foreach (var item in source)
            {
                if (item.SourceTrack != null)
                {
                    var title = item.Title?.ToLower() ?? "";
                    var artist = item.Artist?.ToLower() ?? "";
                    var genre = item.SourceTrack.Genre?.ToLower() ?? "";
                    if (kw.Any(k => title.Contains(k) || artist.Contains(k) || genre.Contains(k)))
                        FilteredQuickPicks.Add(item);
                }
                else
                {
                    FilteredQuickPicks.Add(item);
                }
            }
        }

        private void UpdateHomeGradient()
        {
            HomeGradient = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0, 1),
                GradientStops = new GradientStopCollection
                {
                    new GradientStop(DominantColor, 0.0),
                    new GradientStop(DominantColor, 0.15)
                    {
                        IsFrozen = false
                    },
                    new GradientStop(Color.FromRgb(24, 24, 24), 1.0)
                }
            };
        }

        private async Task LoadQuickPicksAsync(CancellationToken token)
        {
            if (token.IsCancellationRequested) return;
            if (!Settings.Default.DiscoverLibrary) return;

            var recent = await _libraryService.GetHistoryTracksAsync(10, token);
            if (token.IsCancellationRequested) return;
            foreach (var track in (recent ?? Enumerable.Empty<MusicFile>()).Take(8))
                QuickPicks.Add(CreateTrackCard(track));

            if (QuickPicks.Count > 0) return;

            var favs = await _libraryService.GetFavouriteTracksAsync(token);
            foreach (var track in favs.Take(8))
                QuickPicks.Add(CreateTrackCard(track));

            if (QuickPicks.Count > 0) return;

            var all = await _libraryService.GetAllTracksAsync(token);
            foreach (var track in all.OrderBy(_ => Guid.NewGuid()).Take(8))
                QuickPicks.Add(CreateTrackCard(track));
        }

        private async Task LoadBrowseChartsAsync(CancellationToken token)
        {
            if (token.IsCancellationRequested) return;

            var tasks = _sourceManager.Sources
                .Where(s => s.IsConnected && IsSourceEnabledForDiscover(s.Type))
                .Select(s => LoadChartsFromSourceAsync(s, token))
                .ToArray();

            await Task.WhenAll(tasks);
        }

        private async Task LoadChartsFromSourceAsync(StreamingSource source, CancellationToken token)
        {
            if (token.IsCancellationRequested) return;

            var session = _sourceManager.GetSession(source.Id);
            if (session == null) return;

            try
            {
                var songs = await session.GetRandomSongsAsync(6);
                foreach (var song in songs)
                {
                    BrowseCharts.Add(new ChartCard
                    {
                        Title = song.Title,
                        Subtitle = song.Artist,
                        CoverPath = song.CoverPath,
                        Cover = LoadCoverImage(song.CoverPath)
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainViewModel] LoadChartsFromSourceAsync failed: {ex.Message}");
            }
        }

        private async Task LoadTopMusicVideosAsync(CancellationToken token)
        {
            if (token.IsCancellationRequested) return;

            var tasks = _sourceManager.Sources
                .Where(s => s.IsConnected && IsSourceEnabledForDiscover(s.Type))
                .Select(s => LoadVideosFromSourceAsync(s, token))
                .ToArray();

            await Task.WhenAll(tasks);
        }

        private async Task LoadVideosFromSourceAsync(StreamingSource source, CancellationToken token)
        {
            if (token.IsCancellationRequested) return;

            var session = _sourceManager.GetSession(source.Id);
            if (session == null) return;

            try
            {
                var videos = await session.SearchAsync("music", 10);
                foreach (var video in videos.Take(8))
                {
                    TopMusicVideos.Add(new TrackCard
                    {
                        Title = video.Title,
                        Artist = video.Artist,
                        CoverPath = video.CoverPath,
                        Cover = LoadCoverImage(video.CoverPath),
                        SourceTrack = video
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainViewModel] LoadVideosFromSourceAsync failed: {ex.Message}");
            }
        }

        private bool IsSourceEnabledForDiscover(string sourceType)
        {
            return GetEnabledDiscoverSources().Contains(sourceType);
        }

        private System.Collections.Generic.HashSet<string> GetEnabledDiscoverSources()
        {
            var enabled = new System.Collections.Generic.HashSet<string>();
            if (Settings.Default.DiscoverLibrary) enabled.Add(Local);
            if (Settings.Default.DiscoverYouTube) enabled.Add(YouTube);
            if (Settings.Default.DiscoverSubsonic) enabled.Add(Subsonic);

            var extraJson = Settings.Default.DiscoverExtraSources;
            if (!string.IsNullOrEmpty(extraJson))
            {
                try
                {
                    var extra = Newtonsoft.Json.JsonConvert.DeserializeObject<System.Collections.Generic.List<string>>(extraJson);
                    if (extra != null)
                        foreach (var s in extra) enabled.Add(s);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[MainViewModel] Failed to deserialize DiscoverExtraSources: {ex.Message}");
                }
            }

            return enabled;
        }

        private TrackCard CreateTrackCard(MusicFile track)
        {
            return new TrackCard
            {
                Title = track.Title,
                Artist = track.Artist,
                CoverPath = track.CoverPath,
                Cover = LoadCoverImage(track.CoverPath),
                SourceTrack = track
            };
        }

        private BitmapImage LoadCoverImage(string path)
        {
            if (string.IsNullOrEmpty(path))
                return DefaultCover();

            // Subsonic-style cover ID: "sourceId:cover:artId"
            if (path == null || path.Contains(":cover:"))
            {
                _ = LoadStreamingCoverAsync(path).ContinueWith(t =>
                {
                    if (t.Exception != null)
                        System.Diagnostics.Debug.WriteLine($"[MainViewModel] Cover load failed: {t.Exception}");
                }, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.FromCurrentSynchronizationContext());
                return DefaultCover();
            }

            try
            {
                var uri = path.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                    ? new Uri(path)
                    : new Uri(path, UriKind.Absolute);
                var img = new BitmapImage();
                img.BeginInit();
                img.CacheOption = BitmapCacheOption.OnLoad;
                img.UriSource = uri;
                img.EndInit();
                return img;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainViewModel] LoadCoverImage failed: {ex.Message}");
                return DefaultCover();
            }
        }

        private async Task<BitmapImage> LoadStreamingCoverAsync(string coverPath)
        {
            try
            {
                var bytes = await _sourceManager.ResolveCoverArtAsync(coverPath);
                if (bytes == null || bytes.Length == 0)
                    return DefaultCover();

                var img = new BitmapImage();
                using (var ms = new System.IO.MemoryStream(bytes))
                {
                    img.BeginInit();
                    img.CacheOption = BitmapCacheOption.OnLoad;
                    img.StreamSource = ms;
                    img.EndInit();
                }
                img.Freeze();
                return img;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainViewModel] LoadStreamingCoverAsync failed: {ex.Message}");
                return DefaultCover();
            }
        }

        private static BitmapImage DefaultCover()
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.DecodePixelWidth = 1;
            bmp.DecodePixelHeight = 1;
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }

    }

    public class CategoryItem { public string Name { get; set; } }
    public class ChartCard { public string Title { get; set; } public string Subtitle { get; set; } public string CoverPath { get; set; } public BitmapImage Cover { get; set; } }
    public class TrackCard { public string Title { get; set; } public string Artist { get; set; } public string CoverPath { get; set; } public BitmapImage Cover { get; set; } public MusicFile SourceTrack { get; set; } }

    public class MoodChip : ViewModelBase
    {
        private string _name;
        public string Name
        {
            get => _name;
            set { SetProperty(ref _name, value); }
        }

        private string _keywords;
        public string Keywords
        {
            get => _keywords;
            set { SetProperty(ref _keywords, value); }
        }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set { SetProperty(ref _isSelected, value); }
        }
    }
}

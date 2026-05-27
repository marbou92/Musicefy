using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using Musicefy.Core.Interfaces;
using Musicefy.Core.Models;
using Musicefy.Properties;
using Musicefy.Services;
using static Musicefy.Core.SourceTypes;

namespace Musicefy.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly ILibraryService _libraryService;
        private readonly IStreamingSourceManager _sourceManager;
        private readonly PlaybackService _playback;

        public ObservableCollection<ChartCard> BrowseCharts { get; }
        public ObservableCollection<TrackCard> QuickPicks { get; }
        public ObservableCollection<TrackCard> TopMusicVideos { get; }

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

        private CancellationTokenSource _reloadCts;

        public MainViewModel(ILibraryService libraryService, IStreamingSourceManager sourceManager, PlaybackService playback)
        {
            _libraryService = libraryService;
            _sourceManager = sourceManager;
            _playback = playback;

            BrowseCharts = new ObservableCollection<ChartCard>();
            QuickPicks = new ObservableCollection<TrackCard>();
            TopMusicVideos = new ObservableCollection<TrackCard>();
        }

        public async Task ReloadAsync()
        {
            _reloadCts?.Cancel();
            _reloadCts = new CancellationTokenSource();
            var token = _reloadCts.Token;

            IsLoading = true;
            try
            {
                BrowseCharts.Clear();
                QuickPicks.Clear();
                TopMusicVideos.Clear();

                var quickPicksTask = LoadQuickPicksAsync(token);
                var chartsTask = LoadBrowseChartsAsync(token);
                var videosTask = LoadTopMusicVideosAsync(token);

                await Task.WhenAll(quickPicksTask, chartsTask, videosTask);

                IsEmpty = QuickPicks.Count == 0 && BrowseCharts.Count == 0 && TopMusicVideos.Count == 0;
                if (IsEmpty)
                {
                    bool hasSources = _sourceManager.Sources.Any(s => s.IsConnected);
                    bool hasLibraryTracks = (await _libraryService.GetAllTracksAsync(token)).Count > 0;
                    if (hasLibraryTracks)
                        EmptyMessage = "All sources disabled in Discover settings. Enable some to see music here.";
                    else if (!hasSources)
                        EmptyMessage = "No music sources configured. Go to Sources settings to add a library folder or streaming service.";
                    else
                        EmptyMessage = "No tracks available from your current sources.";
                }
            }
            catch (OperationCanceledException) { }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task LoadQuickPicksAsync(CancellationToken token)
        {
            if (token.IsCancellationRequested) return;
            if (!Settings.Default.DiscoverLibrary) return;

            var recent = await _libraryService.GetHistoryTracksAsync(10);
            if (token.IsCancellationRequested) return;
            foreach (var track in recent.Take(8))
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
            catch { }
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
            catch { }
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
                catch { }
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
            if (path.Contains(":cover:"))
            {
                _ = LoadStreamingCoverAsync(path).ContinueWith(t =>
                {
                    if (t.Exception != null)
                        System.Diagnostics.Debug.WriteLine($"Cover load failed: {t.Exception}");
                }, TaskContinuationOptions.OnlyOnFaulted);
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
            catch
            {
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
            catch
            {
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

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class CategoryItem { public string Name { get; set; } }
    public class ChartCard { public string Title { get; set; } public string Subtitle { get; set; } public string CoverPath { get; set; } public BitmapImage Cover { get; set; } }
    public class TrackCard { public string Title { get; set; } public string Artist { get; set; } public string CoverPath { get; set; } public BitmapImage Cover { get; set; } public MusicFile SourceTrack { get; set; } }
}

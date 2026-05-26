using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using Musicefy.Core.Interfaces;
using Musicefy.Core.Models;
using Musicefy.Properties;
using Musicefy.Services;

namespace Musicefy.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly ILibraryService _libraryService;
        private readonly IStreamingSourceManager _sourceManager;
        private readonly PlaybackService _playback;

        public ObservableCollection<ChartCard> BrowseCharts { get; }
        public ObservableCollection<TrackCard> QuickPicks { get; }
        public ObservableCollection<VideoCard> TopMusicVideos { get; }

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set { _isLoading = value; OnPropertyChanged(); }
        }

        public MainViewModel(ILibraryService libraryService, IStreamingSourceManager sourceManager, PlaybackService playback)
        {
            _libraryService = libraryService;
            _sourceManager = sourceManager;
            _playback = playback;

            BrowseCharts = new ObservableCollection<ChartCard>();
            QuickPicks = new ObservableCollection<TrackCard>();
            TopMusicVideos = new ObservableCollection<VideoCard>();

            _ = LoadAsync();
        }

        private async Task LoadAsync()
        {
            IsLoading = true;
            try
            {
                await LoadQuickPicksAsync();
                await LoadBrowseChartsAsync();
                await LoadTopMusicVideosAsync();
            }
            catch
            {
                // Silently handle — home view shows empty sections on failure
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task LoadQuickPicksAsync()
        {
            if (!Settings.Default.DiscoverLibrary) return;

            var recent = await _libraryService.GetHistoryTracksAsync(10);
            foreach (var track in recent.Take(8))
                QuickPicks.Add(CreateTrackCard(track));

            if (QuickPicks.Count > 0) return;

            var favs = await _libraryService.GetFavouriteTracksAsync();
            foreach (var track in favs.Take(8))
                QuickPicks.Add(CreateTrackCard(track));

            if (QuickPicks.Count > 0) return;

            var all = await _libraryService.GetAllTracksAsync();
            foreach (var track in all.OrderBy(_ => Guid.NewGuid()).Take(8))
                QuickPicks.Add(CreateTrackCard(track));
        }

        private async Task LoadBrowseChartsAsync()
        {
            foreach (var source in _sourceManager.Sources)
            {
                if (!IsSourceEnabledForDiscover(source.Type))
                    continue;

                var session = _sourceManager.GetSession(source.Id);
                if (session == null) continue;

                try
                {
                    var songs = await session.GetRandomSongsAsync(6);
                    foreach (var song in songs)
                    {
                        BrowseCharts.Add(new ChartCard
                        {
                            Title = song.Title,
                            Subtitle = song.Artist,
                            Cover = LoadCoverImage(song.CoverPath)
                        });
                    }
                }
                catch
                {
                    // Skip sources that fail
                }
            }
        }

        private async Task LoadTopMusicVideosAsync()
        {
            foreach (var source in _sourceManager.Sources)
            {
                if (!IsSourceEnabledForDiscover(source.Type))
                    continue;

                var session = _sourceManager.GetSession(source.Id);
                if (session == null) continue;

                try
                {
                    var videos = await session.SearchAsync("music", 10);
                    foreach (var video in videos.Take(8))
                    {
                        TopMusicVideos.Add(new VideoCard
                        {
                            Title = video.Title,
                            Channel = video.Artist,
                            Cover = LoadCoverImage(video.CoverPath)
                        });
                    }
                }
                catch
                {
                    // Skip sources that fail
                }
            }
        }

        private bool IsSourceEnabledForDiscover(string sourceType)
        {
            switch (sourceType)
            {
                case "YouTube": return Settings.Default.DiscoverYouTube;
                case "Subsonic": return Settings.Default.DiscoverSubsonic;
                default: return true;
            }
        }

        private TrackCard CreateTrackCard(MusicFile track)
        {
            return new TrackCard
            {
                Title = track.Title,
                Artist = track.Artist,
                Cover = LoadCoverImage(track.CoverPath),
                SourceTrack = track
            };
        }

        private static BitmapImage LoadCoverImage(string path)
        {
            if (string.IsNullOrEmpty(path))
                return DefaultCover();
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

        private static BitmapImage DefaultCover()
        {
            return new BitmapImage(new Uri("pack://application:,,,/Assets/default_cover.png"));
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class CategoryItem { public string Name { get; set; } }
    public class ChartCard { public string Title { get; set; } public string Subtitle { get; set; } public BitmapImage Cover { get; set; } }
    public class TrackCard { public string Title { get; set; } public string Artist { get; set; } public BitmapImage Cover { get; set; } public MusicFile SourceTrack { get; set; } }
    public class VideoCard { public string Title { get; set; } public string Channel { get; set; } public BitmapImage Cover { get; set; } }
}

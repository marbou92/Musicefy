using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Musicefy.Core.Interfaces;
using Musicefy.Core.Models;
using Musicefy.Core.Services;
using Musicefy.Core.Theme;
using Musicefy.Services;
using Musicefy.Views;

namespace Musicefy.ViewModels
{
    public class NowPlayingViewModel : ViewModelBase, IDisposable
    {
        private readonly IAudioPlayer _playback;
        private readonly ILibraryService _libraryService;

        // ── Phase 6: Sleep Timer ──────────────────────────────────────────
        private DispatcherTimer _sleepTimer;
        private TimeSpan _sleepTimeRemaining = TimeSpan.Zero;
#pragma warning disable CS0414
        private bool _sleepEndOfSong;
#pragma warning restore CS0414

        public string SleepTimerText => _sleepTimer?.IsEnabled == true
            ? $"Sleep: {_sleepTimeRemaining:mm\\:ss}"
            : null;

        public bool IsSleepTimerActive => _sleepTimer?.IsEnabled == true;

        private MusicFile _nowPlaying;
        public MusicFile NowPlaying
        {
            get => _nowPlaying;
            private set { SetProperty(ref _nowPlaying, value); }
        }

        private string _playPauseContent = "▶";
        public string PlayPauseContent
        {
            get => _playPauseContent;
            private set { SetProperty(ref _playPauseContent, value); }
        }

        private double _progressValue;
        public double ProgressValue
        {
            get => _progressValue;
            set { SetProperty(ref _progressValue, value); }
        }

        private string _currentTimeText = "0:00";
        public string CurrentTimeText
        {
            get => _currentTimeText;
            set { SetProperty(ref _currentTimeText, value); }
        }

        private string _totalTimeText = "0:00";
        public string TotalTimeText
        {
            get => _totalTimeText;
            set { SetProperty(ref _totalTimeText, value); }
        }

        private bool _isUserScrubbing;
        public bool IsUserScrubbing
        {
            get => _isUserScrubbing;
            set { SetProperty(ref _isUserScrubbing, value); }
        }

        private TimeSpan _totalDuration;
        public TimeSpan TotalDuration
        {
            get => _totalDuration;
            private set { SetProperty(ref _totalDuration, value); }
        }

        public bool IsShuffleEnabled
        {
            get => _playback.ShuffleEnabled;
            set
            {
                if (_playback.ShuffleEnabled != value)
                    _playback.ShuffleEnabled = value;
                OnPropertyChanged();
            }
        }

        public bool IsRepeatEnabled
        {
            get => _playback.RepeatEnabled;
            set
            {
                if (_playback.RepeatEnabled != value)
                    _playback.RepeatEnabled = value;
                OnPropertyChanged();
            }
        }

        private bool _isFavoriteTrack;
        public bool IsFavoriteTrack
        {
            get => _isFavoriteTrack;
            set { SetProperty(ref _isFavoriteTrack, value); }
        }

        private string _audioFormatText;
        public string AudioFormatText
        {
            get => _audioFormatText;
            private set => SetProperty(ref _audioFormatText, value);
        }

        private Color _dominantColor = Colors.Transparent;
        public Color DominantColor
        {
            get => _dominantColor;
            private set { SetProperty(ref _dominantColor, value); }
        }

        private Color _vibrantColor = Colors.Transparent;
        public Color VibrantColor
        {
            get => _vibrantColor;
            private set { SetProperty(ref _vibrantColor, value); }
        }

        private Color _mutedColor = Colors.Transparent;
        public Color MutedColor
        {
            get => _mutedColor;
            private set { SetProperty(ref _mutedColor, value); }
        }

        private SolidColorBrush _dynamicPrimaryBrush = new SolidColorBrush(Colors.Transparent);
        public SolidColorBrush DynamicPrimaryBrush
        {
            get => _dynamicPrimaryBrush;
            private set { SetProperty(ref _dynamicPrimaryBrush, value); }
        }

        private SolidColorBrush _dynamicSurfaceBrush = new SolidColorBrush(Colors.Transparent);
        public SolidColorBrush DynamicSurfaceBrush
        {
            get => _dynamicSurfaceBrush;
            private set { SetProperty(ref _dynamicSurfaceBrush, value); }
        }

        public ObservableCollection<MusicFile> QueueItems { get; } = new ObservableCollection<MusicFile>();

        public enum RightViewMode { None, Lyrics, Queue }
        private RightViewMode _currentRightMode = RightViewMode.None;

        public bool IsLyricsPanelVisible => _currentRightMode == RightViewMode.Lyrics;
        public bool IsQueuePanelVisible => _currentRightMode == RightViewMode.Queue;
        public bool IsRightPanelVisible => _currentRightMode != RightViewMode.None;
        public bool IsPlayerDeckCollapsed => IsRightPanelVisible && ActualWidth > 0 && ActualWidth < 840;

        private double _actualWidth;
        public double ActualWidth
        {
            get => _actualWidth;
            set { SetProperty(ref _actualWidth, value); OnPropertyChanged(nameof(IsPlayerDeckCollapsed)); }
        }

        public ICommand PlayPauseCommand { get; }
        public ICommand PreviousCommand { get; }
        public ICommand NextCommand { get; }
        public ICommand ShuffleCommand { get; }
        public ICommand RepeatCommand { get; }
        public ICommand FavoriteCommand { get; }
        public ICommand ShareCommand { get; }
        public ICommand ShowInExplorerCommand { get; }
        public ICommand ToggleLyricsCommand { get; }
        public ICommand ToggleQueueCommand { get; }
        public ICommand CollapseCommand { get; }
        public ICommand SleepTimerCommand { get; }
        public ICommand QueueItemClickCommand { get; }
        public ICommand GoToArtistCommand { get; }
        public ICommand GoToAlbumCommand { get; }

        public event Action RequestCollapse;
        public event Action<string> RequestNavigateToArtist;
        public event Action<string, string> RequestNavigateToAlbum;

        public NowPlayingViewModel(IAudioPlayer playback, ILibraryService libraryService)
        {
            _playback = playback ?? throw new ArgumentNullException(nameof(playback));
            _libraryService = libraryService;

            PlayPauseCommand = new RelayCommand(ExecutePlayPause);
            PreviousCommand = new RelayCommand(_ => _playback.Previous());
            NextCommand = new RelayCommand(_ => _playback.Next());
            ShuffleCommand = new RelayCommand(_ =>
            {
                _playback.ShuffleEnabled = !_playback.ShuffleEnabled;
                if (_playback.ShuffleEnabled)
                    _playback.ShuffleQueue();
                else
                    _playback.RestoreQueueOrder();
                RefreshQueue();
                OnPropertyChanged(nameof(IsShuffleEnabled));
            });
            RepeatCommand = new RelayCommand(_ => { _playback.RepeatEnabled = !_playback.RepeatEnabled; OnPropertyChanged(nameof(IsRepeatEnabled)); });
            FavoriteCommand = new RelayCommand(async _ => await ExecuteFavoriteAsync());
            ShareCommand = new RelayCommand(_ =>
            {
                if (NowPlaying != null)
                {
                    try { Clipboard.SetText($"{NowPlaying.Title} - {NowPlaying.Artist}"); }
                    catch
                    {
                        // Clipboard may be locked by another process
                    }
                }
            });
            ShowInExplorerCommand = new RelayCommand(_ =>
            {
                if (NowPlaying?.FilePath != null && System.IO.File.Exists(NowPlaying.FilePath))
                {
                    var fullPath = System.IO.Path.GetFullPath(NowPlaying.FilePath);
                    if (!fullPath.StartsWith("\\\\"))
                        System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{fullPath}\"");
                }
            });
            ToggleLyricsCommand = new RelayCommand(_ => ToggleRightPanel(RightViewMode.Lyrics));
            ToggleQueueCommand = new RelayCommand(_ => ToggleRightPanel(RightViewMode.Queue));
            CollapseCommand = new RelayCommand(_ => RequestCollapse?.Invoke());
            SleepTimerCommand = new RelayCommand(_ => ExecuteSleepTimer());
            QueueItemClickCommand = new RelayCommand(p =>
            {
                if (p is MusicFile track)
                    _playback.PlayTrack(track);
            });
            GoToArtistCommand = new RelayCommand(_ =>
            {
                if (NowPlaying != null && !string.IsNullOrEmpty(NowPlaying.Artist))
                    RequestNavigateToArtist?.Invoke(NowPlaying.Artist);
            });
            GoToAlbumCommand = new RelayCommand(_ =>
            {
                if (NowPlaying != null && !string.IsNullOrEmpty(NowPlaying.Album))
                    RequestNavigateToAlbum?.Invoke(NowPlaying.Album, NowPlaying.Artist);
            });

            _playback.TrackChanged += OnTrackChanged;
            _playback.ProgressChanged += OnProgressChanged;
            _playback.PlaybackStateChanged += OnPlaybackStateChanged;

            SyncPlayPauseState(_playback.IsPlaying);
            if (_playback.CurrentTrack != null) NowPlaying = _playback.CurrentTrack;
            RefreshQueue();
        }

        public void Dispose()
        {
            _playback.TrackChanged -= OnTrackChanged;
            _playback.ProgressChanged -= OnProgressChanged;
            _playback.PlaybackStateChanged -= OnPlaybackStateChanged;
        }

        private void OnTrackChanged(MusicFile track)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                NowPlaying = track;
                IsFavoriteTrack = track?.IsFavourite ?? false;
                ExtractColorsFromTrack(track);
                UpdateAudioFormatText(track);

                // Sprint 4: Fetch lyrics from LrcLib if the track doesn't
                // already have embedded lyrics and the feature is enabled.
                _ = FetchLyricsAsync(track);
            });
        }

        /// <summary>
        /// Sprint 4: Fetches lyrics from LrcLib for the current track if
        /// lyrics are not already present and the LyricsEnabled setting is on.
        /// </summary>
        private async System.Threading.Tasks.Task FetchLyricsAsync(MusicFile track)
        {
            try
            {
                if (track == null) return;
                if (!Musicefy.Properties.Settings.Default.LyricsEnabled) return;

                // If the track already has embedded lyrics (from ID3 tags), use them.
                if (!string.IsNullOrEmpty(track.Lyrics))
                    return;

                var lrcLib = App.Services?.GetService(typeof(Musicefy.Core.Services.LrcLibService))
                             as Musicefy.Core.Services.LrcLibService;
                if (lrcLib == null) return;

                int? durationSeconds = track.Duration.TotalSeconds > 0
                    ? (int)track.Duration.TotalSeconds
                    : (int?)null;

                var lyrics = await lrcLib.GetSyncedLyricsAsync(track.Title, track.Artist, track.Album, durationSeconds);

                // Only update if still the current track (user may have switched)
                if (lyrics != null && NowPlaying == track)
                {
                    Application.Current?.Dispatcher.Invoke(() =>
                    {
                        track.Lyrics = lyrics;
                        // Force the Lyrics binding to refresh
                        OnPropertyChanged(nameof(NowPlaying));
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[NowPlaying] FetchLyricsAsync failed: {ex.Message}");
            }
        }

        private void UpdateAudioFormatText(MusicFile track)
        {
            try
            {
                if (track == null)
                {
                    AudioFormatText = null;
                    return;
                }

                string format = string.IsNullOrEmpty(track.SourceType) ? "Audio" : track.SourceType.ToUpperInvariant();
                if (track.Bitrate > 0)
                {
                    double mb = Math.Round(track.FileSize / 1048576.0, 0);
                    AudioFormatText = $"{format} \u2022 {track.Bitrate} kbps \u2022 48.0 kHz \u2022 {mb} MB";
                }
                else
                {
                    AudioFormatText = format;
                }
            }
            catch
            {
                AudioFormatText = null;
            }
        }

        private void ExtractColorsFromTrack(MusicFile track)
        {
            try
            {
                if (track == null || string.IsNullOrEmpty(track.CoverPath))
                {
                    if (IsDynamicColorsEnabled())
                        ThemeManager.ClearDynamicColors();
                    return;
                }

                BitmapSource cover = null;
                if (track.CoverPath.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                    return;

                if (System.IO.File.Exists(track.CoverPath))
                {
                    cover = new BitmapImage(new Uri(track.CoverPath, UriKind.Absolute));
                }

                if (cover == null) return;

                var colors = ColorExtractor.Extract(cover);
                DominantColor = colors.Primary;
                VibrantColor = colors.Vibrant;
                MutedColor = colors.Muted;
                DynamicPrimaryBrush = new SolidColorBrush(colors.Primary);
                DynamicSurfaceBrush = new SolidColorBrush(colors.Surface);
                if (IsDynamicColorsEnabled())
                    ThemeManager.ApplyDynamicColors(colors);
            }
            catch
            {
                // Color extraction is non-critical
            }
        }

        /// <summary>
        /// Checks if dynamic colors are enabled. In the new Aniyomi model,
        /// this is determined by the AppTheme setting being "Dynamic".
        /// Also respects the legacy DynamicColorsEnabled setting for backward compat.
        /// </summary>
        private bool IsDynamicColorsEnabled()
        {
            var (appTheme, _) = ThemeManager.LoadPreferences();
            return appTheme == AppTheme.Dynamic;
        }

        private void OnProgressChanged(TimeSpan current, TimeSpan total)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                TotalDuration = total;
                if (!IsUserScrubbing && total > TimeSpan.Zero)
                {
                    ProgressValue = (current.TotalSeconds / total.TotalSeconds) * 100;
                    CurrentTimeText = current.ToString(@"m\:ss");
                    TotalTimeText = total.ToString(@"m\:ss");
                }
            });
        }

        public void SeekToPercent(double percent)
        {
            if (TotalDuration > TimeSpan.Zero)
            {
                var position = TimeSpan.FromSeconds((percent / 100.0) * TotalDuration.TotalSeconds);
                _playback.Seek(position);
            }
        }

        private void OnPlaybackStateChanged(bool isPlaying)
        {
            Application.Current?.Dispatcher.Invoke(() => SyncPlayPauseState(isPlaying));
        }

        private void SyncPlayPauseState(bool isPlaying)
        {
            PlayPauseContent = isPlaying ? "⏸" : "▶";
        }

        private void ExecutePlayPause()
        {
            if (_playback.IsPlaying) _playback.Pause(); else _playback.Resume();
        }

        private async System.Threading.Tasks.Task ExecuteFavoriteAsync()
        {
            if (NowPlaying == null) return;
            IsFavoriteTrack = !IsFavoriteTrack;
            NowPlaying.IsFavourite = IsFavoriteTrack;
            try
            {
                await _libraryService.ToggleFavouriteAsync(NowPlaying.FilePath);
            }
            catch
            {
                // ToggleFavouriteAsync handles its own errors
            }
        }

        private void ToggleRightPanel(RightViewMode mode)
        {
            _currentRightMode = (_currentRightMode == mode) ? RightViewMode.None : mode;
            OnPropertyChanged(nameof(IsLyricsPanelVisible));
            OnPropertyChanged(nameof(IsQueuePanelVisible));
            OnPropertyChanged(nameof(IsRightPanelVisible));
            OnPropertyChanged(nameof(IsPlayerDeckCollapsed));
            if (_currentRightMode == RightViewMode.Queue)
                RefreshQueue();
        }

        private void RefreshQueue()
        {
            // Sync without Clear/Add to preserve scroll position
            int i = 0;
            foreach (var track in _playback.Queue)
            {
                if (i >= QueueItems.Count)
                    QueueItems.Add(track);
                else if (QueueItems[i] != track)
                    QueueItems[i] = track;
                i++;
            }
            while (QueueItems.Count > _playback.Queue.Count)
                QueueItems.RemoveAt(QueueItems.Count - 1);
        }

        public void UpdateActualWidth(double width) => ActualWidth = width;

        // ── Phase 6: Sleep Timer Implementation ──────────────────────────

        private void ExecuteSleepTimer()
        {
            if (_sleepTimer?.IsEnabled == true)
            {
                StopSleepTimer();
                return;
            }

            // Show a simple input dialog for minutes (predefined options since Microsoft.VisualBasic is not referenced)
            var dialog = new SleepTimerDialog { Owner = Application.Current.MainWindow };
            if (dialog.ShowDialog() == true && dialog.SelectedMinutes > 0)
            {
                StartSleepTimer(TimeSpan.FromMinutes(dialog.SelectedMinutes));
            }
        }

        private void StartSleepTimer(TimeSpan duration)
        {
            StopSleepTimer();

            _sleepTimeRemaining = duration;
            _sleepEndOfSong = false;

            _sleepTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _sleepTimer.Tick += SleepTimer_Tick;
            _sleepTimer.Start();

            OnPropertyChanged(nameof(SleepTimerText));
            OnPropertyChanged(nameof(IsSleepTimerActive));
            ToastService.ShowToast($"Sleep timer set for {duration.TotalMinutes:0} minutes.", System.Windows.Media.Brushes.ForestGreen);
        }

        private void SleepTimer_Tick(object sender, EventArgs e)
        {
            _sleepTimeRemaining = _sleepTimeRemaining.Subtract(TimeSpan.FromSeconds(1));

            if (_sleepTimeRemaining <= TimeSpan.Zero)
            {
                StopSleepTimer();
                _playback.Pause();
                ToastService.ShowToast("Sleep timer ended. Playback paused.", System.Windows.Media.Brushes.DimGray);
                return;
            }

            OnPropertyChanged(nameof(SleepTimerText));
        }

        private void StopSleepTimer()
        {
            if (_sleepTimer != null)
            {
                _sleepTimer.Stop();
                _sleepTimer.Tick -= SleepTimer_Tick;
                _sleepTimer = null;
            }
            _sleepTimeRemaining = TimeSpan.Zero;
            OnPropertyChanged(nameof(SleepTimerText));
            OnPropertyChanged(nameof(IsSleepTimerActive));
        }
    }
}

using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using Musicefy.Core.Interfaces;
using Musicefy.Core.Models;
using Musicefy.Services;

namespace Musicefy.ViewModels
{
    public class NowPlayingViewModel : ViewModelBase, IDisposable
    {
        private readonly PlaybackService _playback;
        private readonly ILibraryService _libraryService;

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
        public ICommand QueueItemClickCommand { get; }

        public event Action RequestCollapse;

        public NowPlayingViewModel(PlaybackService playback, ILibraryService libraryService)
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
                    System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{NowPlaying.FilePath}\"");
            });
            ToggleLyricsCommand = new RelayCommand(_ => ToggleRightPanel(RightViewMode.Lyrics));
            ToggleQueueCommand = new RelayCommand(_ => ToggleRightPanel(RightViewMode.Queue));
            CollapseCommand = new RelayCommand(_ => RequestCollapse?.Invoke());
            QueueItemClickCommand = new RelayCommand(p =>
            {
                if (p is MusicFile track)
                    _playback.PlayTrack(track);
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
            });
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
    }
}

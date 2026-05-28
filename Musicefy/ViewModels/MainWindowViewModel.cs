using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Musicefy.Core.Interfaces;
using Musicefy.Core.Models;
using Musicefy.Services;

namespace Musicefy.ViewModels
{
    /// <summary>
    /// Central ViewModel for the main window shell. Owns navigation state,
    /// mini-player visibility, and coordinates child ViewModels.
    /// </summary>
    public class MainWindowViewModel : ViewModelBase, IDisposable
    {
        private readonly IAudioPlayer _playback;
        private readonly NavigationService _navigationService;

        // ── Navigation state ────────────────────────────────────────────
        private UserControl _currentPage;
        public UserControl CurrentPage
        {
            get => _currentPage;
            set { SetProperty(ref _currentPage, value); }
        }

        private int _selectedSidebarIndex;
        public int SelectedSidebarIndex
        {
            get => _selectedSidebarIndex;
            set
            {
                if (SetProperty(ref _selectedSidebarIndex, value))
                    NavigateToPage(value);
            }
        }

        // ── Mini-player state ───────────────────────────────────────────
        private MusicFile _nowPlaying;
        public MusicFile NowPlaying
        {
            get => _nowPlaying;
            set { SetProperty(ref _nowPlaying, value); OnPropertyChanged(nameof(IsMiniPlayerVisible)); }
        }

        public bool IsMiniPlayerVisible => NowPlaying != null && !IsFullPanelOpen;

        private string _miniPlayContent = "▶";
        public string MiniPlayContent
        {
            get => _miniPlayContent;
            private set { SetProperty(ref _miniPlayContent, value); }
        }

        private bool _isFullPanelOpen;
        public bool IsFullPanelOpen
        {
            get => _isFullPanelOpen;
            set { SetProperty(ref _isFullPanelOpen, value); OnPropertyChanged(nameof(IsMiniPlayerVisible)); }
        }

        // ── Commands ────────────────────────────────────────────────────
        public ICommand MiniPlayCommand { get; }
        public ICommand MiniPreviousCommand { get; }
        public ICommand MiniNextCommand { get; }
        public ICommand DismissMiniPlayerCommand { get; }
        public ICommand ShowNowPlayingCommand { get; }

        // ── Playback service accessor (for child VMs) ───────────────────
        public IAudioPlayer Playback => _playback;

        public MainWindowViewModel(IAudioPlayer playback, NavigationService navigationService)
        {
            _playback = playback ?? throw new ArgumentNullException(nameof(playback));
            _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));

            // Commands
            MiniPlayCommand = new RelayCommand(ExecuteMiniPlay);
            MiniPreviousCommand = new RelayCommand(_ => _playback.Previous());
            MiniNextCommand = new RelayCommand(_ => _playback.Next());
            ShowNowPlayingCommand = new RelayCommand(_ => IsFullPanelOpen = true);

            // Wire playback events
            _playback.TrackChanged += OnTrackChanged;
            _playback.PlaybackStateChanged += OnPlaybackStateChanged;

            // Navigate to home on startup
            SelectedSidebarIndex = 0;
        }

        public void NavigateToPage(int index)
        {
            CurrentPage = _navigationService.GetPage(index);
        }

        private void OnTrackChanged(MusicFile track)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                NowPlaying = track;
            });
        }

        private void OnPlaybackStateChanged(bool isPlaying)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                MiniPlayContent = isPlaying ? "⏸" : "▶";
            });
        }

        private void ExecuteMiniPlay()
        {
            if (_playback.IsPlaying) _playback.Pause(); else _playback.Resume();
        }

        public void Dispose()
        {
            _playback.TrackChanged -= OnTrackChanged;
            _playback.PlaybackStateChanged -= OnPlaybackStateChanged;
        }
    }
}

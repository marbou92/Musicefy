using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Musicefy.Core.Models;
using Musicefy.Core.Services;
using Musicefy.Views;
using Musicefy.Services;
using NAudio.Wave;
using IOFile = System.IO.File;
using TagLibFile = TagLib.File;

namespace Musicefy
{
    public partial class MainWindow : Window
    {
        private IWavePlayer _waveOut;
        private AudioFileReader _audioFile;
        private DispatcherTimer _timer;

        private StreamingSourceManager _sourceManager;
        private PlaylistManager _playlistManager;

        private ObservableCollection<MusicFile> _allTracks = new ObservableCollection<MusicFile>();
        private ObservableCollection<MusicFile> _queue = new ObservableCollection<MusicFile>();

        // New path collections
        private ObservableCollection<MusicFile> _favourites = new ObservableCollection<MusicFile>();
        private ObservableCollection<MusicFile> _downloads = new ObservableCollection<MusicFile>();
        private ObservableCollection<MusicFile> _history = new ObservableCollection<MusicFile>();

        private int _currentQueueIndex = -1;
        private bool _isInitialized = false;

        public MainWindow()
        {
            InitializeComponent();
            InitializeApp();
            RefreshSources();
            BindPaths();
        }

        private void InitializeApp()
        {
            _sourceManager = new StreamingSourceManager();
            _playlistManager = new PlaylistManager();

            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _timer.Tick += Timer_Tick;

            VolumeSlider.Value = 70;
            _isInitialized = true;
        }

        private void BindPaths()
        {
            FavouritesList.ItemsSource = _favourites;
            DownloadsList.ItemsSource = _downloads;
            HistoryList.ItemsSource = _history;

            HistoryEmpty.Visibility = _history.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            DownloadsEmpty.Visibility = _downloads.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            FavouritesEmpty.Visibility = _favourites.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        // … keep all your existing playback, queue, search, source, shuffle/repeat logic here …
        // Example: PlayTrack, LoadTracksFromFolder, VolumeSlider_ValueChanged, etc.
    }
}

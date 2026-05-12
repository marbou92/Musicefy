using System;
using System.Collections.Generic;
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

        private int _currentQueueIndex = -1;

        public MainWindow()
        {
            InitializeComponent();
            InitializeApp();
            RefreshSources();
        }

        private void InitializeApp()
        {
            _sourceManager = new StreamingSourceManager();
            _playlistManager = new PlaylistManager();

            TracksList.ItemsSource = _allTracks;
            QueueList.ItemsSource = _queue;

            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _timer.Tick += Timer_Tick;

            VolumeSlider.Value = 70;
        }

        private void RefreshSources()
        {
            SourcesListBox.Items.Clear();
            var sources = _sourceManager.GetAllSources();

            foreach (var source in sources)
                SourcesListBox.Items.Add(source);

            NoSourcesHint.Visibility = sources.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void AddSourceButton_Click(object sender, RoutedEventArgs e)
        {
            var win = new AddSourceWindow(_sourceManager) { Owner = this };
            if (win.ShowDialog() == true)
            {
                RefreshSources();
                MessageBox.Show("Source added!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void SourcesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SourcesListBox.SelectedItem == null) return;
            dynamic source = SourcesListBox.SelectedItem;

            try
            {
                string sourceType = source.Type?.ToString() ?? "";
                string path = source.Path?.ToString() ?? source.Url?.ToString() ?? "";

                if (sourceType.Equals("Local", StringComparison.OrdinalIgnoreCase) && Directory.Exists(path))
                {
                    LoadTracksFromFolder(path);
                }
            }
            catch { }
        }

        private void LoadTracksFromFolder(string folderPath)
        {
            if (!Directory.Exists(folderPath)) return;

            var extensions = new[] { ".mp3", ".wav", ".flac", ".ogg", ".m4a", ".aac" };

            var files = Directory.EnumerateFiles(folderPath, "*.*", SearchOption.AllDirectories)
                                 .Where(f => extensions.Contains(Path.GetExtension(f).ToLowerInvariant()));

            _allTracks.Clear();

            foreach (var file in files)
            {
                var track = CreateTrackFromFile(file);
                _allTracks.Add(track);
            }

            UpdateLibraryUI();
        }

        private MusicFile CreateTrackFromFile(string filePath)
        {
            string title = Path.GetFileNameWithoutExtension(filePath);
            string artist = "Unknown Artist";
            string album = "Unknown Album";
            int year = 0;

            try
            {
                var tag = TagLibFile.Create(filePath);
                if (!string.IsNullOrWhiteSpace(tag.Tag.Title)) title = tag.Tag.Title;
                if (tag.Tag.Performers?.Length > 0 && !string.IsNullOrWhiteSpace(tag.Tag.Performers[0]))
                    artist = tag.Tag.Performers[0];
                if (!string.IsNullOrWhiteSpace(tag.Tag.Album)) album = tag.Tag.Album;
                if (tag.Tag.Year > 0) year = (int)tag.Tag.Year;
                tag.Dispose();
            }
            catch { }

            return new MusicFile(title, artist, album, year, filePath)
            {
                SourceType = "Local",
                SourceUri = filePath
            };
        }

        private void UpdateLibraryUI()
        {
            int count = _allTracks.Count;
            TrackCountLabel.Text = $"{count} track{(count == 1 ? "" : "s")}";
            LibraryEmptyState.Visibility = count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void EnqueueTrack(MusicFile track)
        {
            if (!_queue.Contains(track))
                _queue.Add(track);
            QueueEmptyHint.Visibility = _queue.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ClearQueue_Click(object sender, RoutedEventArgs e)
        {
            _queue.Clear();
            _currentQueueIndex = -1;
            QueueEmptyHint.Visibility = Visibility.Visible;
        }

        private void TracksList_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (TracksList.SelectedItem is MusicFile track)
            {
                _queue.Clear();
                foreach (var t in _allTracks) _queue.Add(t);
                _currentQueueIndex = _queue.IndexOf(track);
                QueueEmptyHint.Visibility = _queue.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
                PlayTrack(track);
            }
        }

        private void QueueList_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (QueueList.SelectedItem is MusicFile track)
            {
                _currentQueueIndex = _queue.IndexOf(track);
                PlayTrack(track);
            }
        }

        private void PlayButton_Click

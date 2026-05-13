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

        private int _currentQueueIndex = -1;
        private bool _isInitialized = false;

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
            _isInitialized = true;
        }
        private void RefreshSources()
        {
            if (!_isInitialized) return;

            SourcesListBox.Items.Clear();
            var sources = _sourceManager.GetAllSources();
            foreach (var source in sources)
                SourcesListBox.Items.Add(source);

            NoSourcesHint.Visibility = sources.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_isInitialized) return;

            string query = SearchTextBox.Text?.Trim().ToLowerInvariant() ?? "";
            TracksList.ItemsSource = string.IsNullOrEmpty(query)
                ? _allTracks.ToList()
                : _allTracks.Where(t =>
                    (t.Title?.ToLowerInvariant().Contains(query) == true) ||
                    (t.Artist?.ToLowerInvariant().Contains(query) == true) ||
                    (t.Album?.ToLowerInvariant().Contains(query) == true))
                  .ToList();
        }

        private void AddSourceButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized) return;

            var win = new AddSourceWindow(_sourceManager) { Owner = this };
            if (win.ShowDialog() == true)
            {
                RefreshSources();
                MessageBox.Show("Source added!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void SourcesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isInitialized) return;
            if (SourcesListBox.SelectedItem == null) return;

            var source = SourcesListBox.SelectedItem;
            string sourceType = source.GetType().GetProperty("Type")?.GetValue(source)?.ToString() ?? "";
            string path = source.GetType().GetProperty("Path")?.GetValue(source)?.ToString()
                         ?? source.GetType().GetProperty("Url")?.GetValue(source)?.ToString()
                         ?? "";

            if (sourceType.Equals("Local", StringComparison.OrdinalIgnoreCase) && Directory.Exists(path))
            {
                LoadTracksFromFolder(path);
            }
        }
        private void LoadTracksFromFolder(string folderPath)
        {
            if (!_isInitialized) return;
            if (!Directory.Exists(folderPath)) return;

            var extensions = new[] { ".mp3", ".wav", ".flac", ".ogg", ".m4a", ".aac" };
            var files = Directory.EnumerateFiles(folderPath, "*.*", SearchOption.AllDirectories)
                                 .Where(f => extensions.Contains(Path.GetExtension(f).ToLowerInvariant()));

            _allTracks.Clear();
            foreach (var file in files)
                _allTracks.Add(CreateTrackFromFile(file));

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
            if (!_isInitialized) return;

            int count = _allTracks.Count;
            TrackCountLabel.Text = $"{count} track{(count == 1 ? "" : "s")}";
            LibraryEmptyState.Visibility = count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void EnqueueTrack(MusicFile track)
        {
            if (!_isInitialized || track == null) return;

            if (!_queue.Contains(track))
                _queue.Add(track);

            QueueEmptyHint.Visibility = _queue.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ClearQueue_Click(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized) return;

            _queue.Clear();
            _currentQueueIndex = -1;
            QueueEmptyHint.Visibility = Visibility.Visible;
        }
        private void TracksList_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (!_isInitialized) return;

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
            if (!_isInitialized) return;

            if (QueueList.SelectedItem is MusicFile track)
            {
                _currentQueueIndex = _queue.IndexOf(track);
                PlayTrack(track);
            }
        }

        private void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized) return;

            if (_waveOut != null)
            {
                _waveOut.Play();
                return;
            }
            if (TracksList.SelectedItem is MusicFile track)
            {
                EnqueueTrack(track);
                _currentQueueIndex = _queue.IndexOf(track);
                PlayTrack(track);
            }
        }

        private void PauseButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized) return;
            _waveOut?.Pause();
        }

        private void PreviousButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized) return;
            if (_queue.Count == 0) return;
            _currentQueueIndex = Math.Max(0, _currentQueueIndex - 1);
            PlayTrack(_queue[_currentQueueIndex]);
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized) return;
            if (_queue.Count == 0) return;
            _currentQueueIndex = _playlistManager.ShuffleEnabled
                ? new Random().Next(_queue.Count)
                : (_currentQueueIndex + 1) % _queue.Count;
            PlayTrack(_queue[_currentQueueIndex]);
        }
        private void Timer_Tick(object sender, EventArgs e)
        {
            if (!_isInitialized || _audioFile == null) return;
            PlaybackSlider.Value = _audioFile.CurrentTime.TotalSeconds;
            ElapsedText.Text = _audioFile.CurrentTime.ToString(@"m\:ss");
            RemainingText.Text = (_audioFile.TotalTime - _audioFile.CurrentTime).ToString(@"m\:ss");
        }

        private void StopPlayback()
        {
            _timer.Stop();
            if (_waveOut != null)
            {
                _waveOut.PlaybackStopped -= WaveOut_PlaybackStopped;
                _waveOut.Stop();
                _waveOut.Dispose();
                _waveOut = null;
            }
            _audioFile?.Dispose();
            _audioFile = null;
        }

        private void WaveOut_PlaybackStopped(object sender, StoppedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                if (e.Exception == null && _playlistManager.RepeatEnabled)
                {
                    if (_currentQueueIndex >= 0 && _currentQueueIndex < _queue.Count)
                        PlayTrack(_queue[_currentQueueIndex]);
                }
                else if (e.Exception == null)
                {
                    NextButton_Click(null, null);
                }
            });
        }

        private void PlaybackSlider_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isInitialized) return;
            if (_audioFile != null)
                _audioFile.CurrentTime = TimeSpan.FromSeconds(PlaybackSlider.Value);
        }

        private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_isInitialized) return;

            if (_audioFile != null)
                _audioFile.Volume = (float)(VolumeSlider.Value / 100.0);
            if (VolumeLabel != null)
                VolumeLabel.Text = $"{(int)VolumeSlider.Value}%";
        }

        private void TracksList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isInitialized) return;

            if (TracksList.SelectedItem is MusicFile track)
            {
                NowPlayingTitle.Text = track.Title;
                NowPlayingArtist.Text = track.Artist;
                NowPlayingMeta.Text = $"{track.Album}{(track.Year > 0 ? " • " + track.Year : "")}";
                LoadAlbumArt(track);
            }
        }

        private void PlayTrack(MusicFile track)
        {
            if (!_isInitialized || track == null) return;

            StopPlayback();

            string uri = track.SourceUri ?? track.FilePath;
            if (string.IsNullOrEmpty(uri) || !IOFile.Exists(uri))
            {
                MessageBox.Show($"File not found:\n{uri}", "Cannot Play", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                _waveOut = new WaveOutEvent();
                _audioFile = new AudioFileReader(uri);
                _audioFile.Volume = (float)(VolumeSlider.Value / 100.0);
                _waveOut.Init(_audioFile);
                _waveOut.Play();
                _waveOut.PlaybackStopped += WaveOut_PlaybackStopped;

                NowPlayingTitle.Text = track.Title;
                NowPlayingArtist.Text = track.Artist;
                NowPlayingMeta.Text = $"{track.Album}{(track.Year > 0 ? " • " + track.Year : "")}";

                LoadAlbumArt(track);

                PlaybackSlider.Maximum = _audioFile.TotalTime.TotalSeconds;
                PlaybackSlider.Value = 0;
                _timer.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Playback error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadAlbumArt(MusicFile track)
        {
            if (!_isInitialized || track == null) return;

            try
            {
                string path = track.FilePath;
                if (!string.IsNullOrEmpty(path) && IOFile.Exists(path))
                {
                    var file = TagLibFile.Create(path);
                    if (file.Tag.Pictures.Length > 0)
                    {
                        var pic = file.Tag.Pictures[0];
                        using (var ms = new MemoryStream(pic.Data.Data))
                        {
                            var img = new BitmapImage();
                            img.BeginInit();
                            img.StreamSource = ms;
                            img.CacheOption = BitmapCacheOption.OnLoad;
                            img.EndInit();
                            AlbumArtImage.Source = img;
                            file.Dispose();
                            return;
                        }
                    }
                    file.Dispose();
                }
            }
            catch { }

            AlbumArtImage.Source = new BitmapImage(new Uri("pack://application:,,,/Assets/default_cover.png"));
        }

        private void ShuffleButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized) return;

            _playlistManager.ShuffleEnabled = !_playlistManager.ShuffleEnabled;
            ShuffleButton.Foreground = _playlistManager.ShuffleEnabled
                ? (Brush)FindResource("AccentBrush")
                : (Brush)FindResource("ForegroundBrush");
        }

        private void RepeatButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized) return;

            _playlistManager.RepeatEnabled = !_playlistManager.RepeatEnabled;
            RepeatButton.Foreground = _playlistManager.RepeatEnabled
                ? (Brush)FindResource("AccentBrush")
                : (Brush)FindResource("ForegroundBrush");
        }

        private void OpenSettings_Click(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized) return;

            var win = new SettingsWindow { Owner = this };
            if (win.ShowDialog() == true)
            {
                string theme = Musicefy.Properties.Settings.Default.Theme ?? "Dark|Default";
                ThemeManager.ApplyThemeFromString(theme);
            }
        }

        private void About_Click(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized) return;

            MessageBox.Show("Musicefy — Music Streaming Player\nVersion 1.0.0\n© 2026",
                            "About Musicefy", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized) return;
            Application.Current.Shutdown();
        }
    }
}

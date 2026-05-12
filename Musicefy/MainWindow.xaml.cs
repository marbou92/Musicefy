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

        // … (library, queue, playback methods as before)

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

        private void Timer_Tick(object sender, EventArgs e)
        {
            if (_audioFile == null) return;
            PlaybackSlider.Value = _audioFile.CurrentTime.TotalSeconds;
            ElapsedText.Text = _audioFile.CurrentTime.ToString(@"m\:ss");
            RemainingText.Text = (_audioFile.TotalTime - _audioFile.CurrentTime).ToString(@"m\:ss");
        }

        private void PlaybackSlider_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_audioFile != null)
                _audioFile.CurrentTime = TimeSpan.FromSeconds(PlaybackSlider.Value);
        }

        private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_audioFile != null)
                _audioFile.Volume = (float)(VolumeSlider.Value / 100.0);
            if (VolumeLabel != null)
                VolumeLabel.Text = $"{(int)VolumeSlider.Value}%";
        }

        private void TracksList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TracksList.SelectedItem is MusicFile track)
            {
                NowPlayingTitle.Text = track.Title;
                NowPlayingArtist.Text = track.Artist;
                NowPlayingMeta.Text = $"{track.Album}{(track.Year > 0 ? " • " + track.Year : "")}";
                LoadAlbumArt(track);
            }
        }

        private void LoadAlbumArt(MusicFile track)
        {
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
            _playlistManager.ShuffleEnabled = !_playlistManager.ShuffleEnabled;
            ShuffleButton.Foreground = _playlistManager.ShuffleEnabled
                ? (Brush)FindResource("AccentBrush")
                : (Brush)FindResource("ForegroundBrush");
        }

        private void RepeatButton_Click(object sender, RoutedEventArgs e)
        {
            _playlistManager.RepeatEnabled = !_playlistManager.RepeatEnabled;
            RepeatButton.Foreground = _playlistManager.RepeatEnabled
                ? (Brush)FindResource("AccentBrush")
                : (Brush)FindResource("ForegroundBrush");
        }

        private void OpenSettings_Click(object sender, RoutedEventArgs e)
        {
            var win = new SettingsWindow { Owner = this };
            if (win.ShowDialog() == true)
            {
                string theme = Musicefy.Properties.Settings.Default.Theme;
                ThemeManager.ApplyTheme(theme);
            }
        }

        private void About_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Musicefy — Music Streaming Player\nVersion 1.0.0\n© 2026",
                            "About Musicefy", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void Exit_Click(object sender, RoutedEventArgs e) => Application.Current.Shutdown();
    }
}

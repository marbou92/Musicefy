using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Musicefy.Core.Models;
using Musicefy.Core.Services;
using Musicefy.Views;
using Musicefy.Services; // ThemeManager
using NAudio.Wave;
using IOFile = System.IO.File;   // ✅ alias for System.IO.File
using TagLibFile = TagLib.File; // ✅ alias for TagLib.File

namespace Musicefy
{
    public partial class MainWindow : Window
    {
        private IWavePlayer waveOut;
        private AudioFileReader audioFile;
        private DispatcherTimer timer;
        private StreamingSourceManager sourceManager;
        private PlaylistManager playlistManager;
        private int currentTrackIndex = -1;

        public MainWindow()
        {
            InitializeComponent();
            InitializeApp();
            RefreshSources();
            RefreshTracks();
        }

        private void InitializeApp()
        {
            sourceManager = new StreamingSourceManager();
            playlistManager = new PlaylistManager();

            TracksList.SelectionChanged += TracksList_SelectionChanged;

            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(1);
            timer.Tick += Timer_Tick;

            VolumeSlider.ValueChanged += VolumeSlider_ValueChanged;
        }

        private void RefreshSources()
        {
            SourcesListBox.Items.Clear();
            var sources = sourceManager.GetAllSources();

            foreach (var source in sources)
            {
                var item = new ListBoxItem
                {
                    Content = source.Name,
                    Padding = new Thickness(10),
                    Margin = new Thickness(0, 5, 0, 0)
                };
                SourcesListBox.Items.Add(item);
            }
        }

        private void RefreshTracks()
        {
            var tracks = playlistManager.GetSampleTracks();
            TracksList.ItemsSource = tracks;
            QueueList.ItemsSource = tracks; // ✅ Show queue
        }

        private void AddSourceButton_Click(object sender, RoutedEventArgs e)
        {
            var addSourceWindow = new AddSourceWindow(sourceManager)
            {
                Owner = this
            };

            if (addSourceWindow.ShowDialog() == true)
            {
                RefreshSources();
                MessageBox.Show("Source added successfully!", "Source Added");
            }
        }

        private void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            if (TracksList.SelectedItem is MusicFile track)
            {
                PlayTrack(track);
            }
        }

        private void PauseButton_Click(object sender, RoutedEventArgs e)
        {
            waveOut?.Pause();
        }

        private void PreviousButton_Click(object sender, RoutedEventArgs e)
        {
            var prev = playlistManager.Previous();
            if (prev != null) PlayTrack(prev);
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            var next = playlistManager.Next();
            if (next != null) PlayTrack(next);
        }

        private void QueueList_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (QueueList.SelectedItem is MusicFile track)
            {
                PlayTrack(track);
            }
        }

        private void PlayTrack(MusicFile track)
        {
            StopPlayback();

            if (string.IsNullOrEmpty(track.SourceUri) && !string.IsNullOrEmpty(track.Path))
                track.SourceUri = track.Path;

            if (string.IsNullOrEmpty(track.SourceUri))
            {
                MessageBox.Show("Track has no source URI.", "Cannot Play");
                return;
            }

            try
            {
                waveOut = new WaveOutEvent();
                audioFile = new AudioFileReader(track.SourceUri);
                waveOut.Init(audioFile);
                waveOut.Play();

                currentTrackIndex = TracksList.Items.IndexOf(track);

                // Update Now Playing info
                NowPlayingTitle.Text = track.Title;
                NowPlayingArtist.Text = track.Artist;
                NowPlayingMeta.Text = $"{track.Album} • {track.Year}";

                // Album art
                LoadAlbumArt(track);

                PlaybackSlider.Maximum = audioFile.TotalTime.TotalSeconds;
                timer.Start();

                // ✅ Update queue highlight
                QueueList.SelectedItem = track;
                QueueList.ScrollIntoView(track);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Playback error: {ex.Message}", "Playback Error");
            }
        }

        private void StopPlayback()
        {
            timer.Stop();
            waveOut?.Stop();
            audioFile?.Dispose();
            waveOut?.Dispose();
            waveOut = null;
            audioFile = null;
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            if (audioFile != null)
            {
                PlaybackSlider.Value = audioFile.CurrentTime.TotalSeconds;
                ElapsedText.Text = audioFile.CurrentTime.ToString(@"m\:ss");
                RemainingText.Text = (audioFile.TotalTime - audioFile.CurrentTime).ToString(@"m\:ss");
            }
        }

        private void PlaybackSlider_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (audioFile != null)
            {
                audioFile.CurrentTime = TimeSpan.FromSeconds(PlaybackSlider.Value);
            }
        }

        private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (audioFile != null)
            {
                audioFile.Volume = (float)(VolumeSlider.Value / 100.0);
            }
        }

        private void TracksList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TracksList.SelectedItem is MusicFile track)
            {
                NowPlayingTitle.Text = track.Title;
                NowPlayingArtist.Text = track.Artist;
                NowPlayingMeta.Text = $"{track.Album} • {track.Year}";
                LoadAlbumArt(track);
            }
        }

        private void LoadAlbumArt(MusicFile track)
        {
            try
            {
                if (!string.IsNullOrEmpty(track.Path) && IOFile.Exists(track.Path))
                {
                    var file = TagLibFile.Create(track.Path);
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
                        }
                    }
                    else
                    {
                        AlbumArtImage.Source = new BitmapImage(new Uri("pack://application:,,,/Assets/default_cover.png"));
                    }
                }
                else
                {
                    AlbumArtImage.Source = new BitmapImage(new Uri("pack://application:,,,/Assets/default_cover.png"));
                }
            }
            catch
            {
                AlbumArtImage.Source = new BitmapImage(new Uri("pack://application:,,,/Assets/default_cover.png"));
            }
        }

        // Shuffle & Repeat toggles
        private void ShuffleButton_Click(object sender, RoutedEventArgs e)
        {
            playlistManager.ShuffleEnabled = !playlistManager.ShuffleEnabled;
            ShuffleButton.Background = playlistManager.ShuffleEnabled
                ? (Brush)FindResource("AccentBrush")
                : (Brush)FindResource("SecondaryBackgroundBrush");
        }

        private void RepeatButton_Click(object sender, RoutedEventArgs e)
        {
            playlistManager.RepeatEnabled = !playlistManager.RepeatEnabled;
            RepeatButton.Background = playlistManager.RepeatEnabled
                ? (Brush)FindResource("AccentBrush")
                : (Brush)FindResource("SecondaryBackgroundBrush");
        }

        // Settings button handler
        private void OpenSettings_Click(object sender, RoutedEventArgs e)
        {
            var settingsWindow = new SettingsWindow { Owner = this };
            if (settingsWindow.ShowDialog() == true)
            {
                string selectedTheme = Musicefy.Properties.Settings.Default.Theme;
                ThemeManager.ApplyTheme(selectedTheme);
            }
        }

        private void About_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Musicefy - Music Streaming Player\nVersion 1.0.0\n© 2026 Musicefy Team",
                            "About Musicefy",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
    }
}

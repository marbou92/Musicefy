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

            if (

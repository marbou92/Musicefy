using System;
using System.Windows.Controls;
using Musicefy.Services;
using Musicefy.Core.Models;

namespace Musicefy.Views
{
    public partial class NowPlayingControl : UserControl
    {
        private readonly PlaybackService _playback;

        // Event to notify MainWindow to collapse back
        public event Action RequestCollapse;

        public NowPlayingControl(PlaybackService playback)
        {
            InitializeComponent();
            _playback = playback;

            _playback.TrackChanged += OnTrackChanged;
            _playback.ProgressChanged += OnProgressChanged;
        }

        // Back button click
        private void BackButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            RequestCollapse?.Invoke();
        }

        // Update UI when track changes
        private void OnTrackChanged(MusicFile track)
        {
            // Reset progress slider
            ProgressSlider.Value = 0;
            ProgressSlider.Maximum = track.Duration.TotalSeconds;

            // Ensure placeholders if metadata is missing
            if (string.IsNullOrWhiteSpace(track.Title))
                track.Title = "Untitled Track";
            if (string.IsNullOrWhiteSpace(track.Artist))
                track.Artist = "Unknown";
            if (string.IsNullOrWhiteSpace(track.Album))
                track.Album = "Unknown Album";
            if (string.IsNullOrWhiteSpace(track.Genre))
                track.Genre = "Unknown Genre";
        }

        // Update progress slider
        private void OnProgressChanged(TimeSpan current, TimeSpan total)
        {
            ProgressSlider.Maximum = total.TotalSeconds;
            ProgressSlider.Value = current.TotalSeconds;
        }

        // Playback controls
        private void Play_Click(object sender, System.Windows.RoutedEventArgs e) => _playback.Resume();
        private void Pause_Click(object sender, System.Windows.RoutedEventArgs e) => _playback.Pause();
        private void Next_Click(object sender, System.Windows.RoutedEventArgs e) => _playback.Next();
        private void Previous_Click(object sender, System.Windows.RoutedEventArgs e) => _playback.Previous();
    }
}

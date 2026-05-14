using System;
using System.Windows.Controls;
using System.Windows.Input;
using Musicefy.Services;
using Musicefy.Core.Models;

namespace Musicefy.Views
{
    public partial class NowPlayingControl : UserControl
    {
        private readonly PlaybackService _playback;

        // Event to notify MainWindow to collapse back
        public event Action RequestCollapse;

        // Track swipe/drag start position
        private double _startY;

        public NowPlayingControl(PlaybackService playback)
        {
            InitializeComponent();
            _playback = playback;

            _playback.TrackChanged += OnTrackChanged;
            _playback.ProgressChanged += OnProgressChanged;

            // Enable touch and mouse events
            this.IsManipulationEnabled = true;
            this.TouchDown += OnTouchDown;
            this.TouchMove += OnTouchMove;

            this.MouseDown += OnMouseDown;
            this.MouseMove += OnMouseMove;
        }

        // Capture initial touch position
        private void OnTouchDown(object sender, TouchEventArgs e)
        {
            _startY = e.GetTouchPoint(this).Position.Y;
        }

        // Detect downward swipe (touch)
        private void OnTouchMove(object sender, TouchEventArgs e)
        {
            double currentY = e.GetTouchPoint(this).Position.Y;
            if (currentY - _startY > 50) // downward swipe threshold
            {
                RequestCollapse?.Invoke();
                _startY = currentY; // reset so it doesn’t trigger repeatedly
            }
        }

        // Capture initial mouse position
        private void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            _startY = e.GetPosition(this).Y;
        }

        // Detect downward drag (mouse)
        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                double currentY = e.GetPosition(this).Y;
                if (currentY - _startY > 50) // downward drag threshold
                {
                    RequestCollapse?.Invoke();
                    _startY = currentY; // reset so it doesn’t trigger repeatedly
                }
            }
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

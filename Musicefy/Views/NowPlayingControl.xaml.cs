using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Musicefy.Services;
using Musicefy.Core.Models;

namespace Musicefy.Views
{
    public partial class NowPlayingControl : UserControl
    {
        private readonly PlaybackService _playback;
        public event Action RequestCollapse;
        private double _startY;

        public NowPlayingControl(PlaybackService playback)
        {
            InitializeComponent();
            _playback = playback;

            // Subscribe to playback events
            _playback.TrackChanged += OnTrackChanged;
            _playback.ProgressChanged += OnProgressChanged;

            // Cleanup when control is removed
            this.Unloaded += (s, e) => {
                _playback.TrackChanged -= OnTrackChanged;
                _playback.ProgressChanged -= OnProgressChanged;
            };

            this.IsManipulationEnabled = true;
        }

        #region Interaction Logic
        private void OnMouseDown(object sender, MouseButtonEventArgs e) => _startY = e.GetPosition(this).Y;

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                double currentY = e.GetPosition(this).Y;
                if (currentY - _startY > 60) // Increased threshold slightly for better UX
                {
                    RequestCollapse?.Invoke();
                    _startY = currentY; 
                }
            }
        }

        private void OnTouchDown(object sender, TouchEventArgs e) => _startY = e.GetTouchPoint(this).Position.Y;

        private void OnTouchMove(object sender, TouchEventArgs e)
        {
            double currentY = e.GetTouchPoint(this).Position.Y;
            if (currentY - _startY > 60)
            {
                RequestCollapse?.Invoke();
                _startY = currentY;
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e) => RequestCollapse?.Invoke();
        #endregion

        #region Playback Updates
        private void OnTrackChanged(MusicFile track)
        {
            if (track == null) return;

            ProgressSlider.Value = 0;
            ProgressSlider.Maximum = track.Duration.TotalSeconds > 0 ? track.Duration.TotalSeconds : 100;
            
            // Updating UI Text is usually better via DataBinding, 
            // but this ensures the View updates immediately on event fire.
        }

        private void OnProgressChanged(TimeSpan current, TimeSpan total)
        {
            ProgressSlider.Maximum = total.TotalSeconds;
            ProgressSlider.Value = current.TotalSeconds;
        }

        private void Play_Click(object sender, RoutedEventArgs e) => _playback.Resume();
        private void Pause_Click(object sender, RoutedEventArgs e) => _playback.Pause();
        private void Next_Click(object sender, RoutedEventArgs e) => _playback.Next();
        private void Previous_Click(object sender, RoutedEventArgs e) => _playback.Previous();
        #endregion
    }
}

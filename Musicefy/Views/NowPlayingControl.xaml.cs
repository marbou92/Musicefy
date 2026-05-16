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
        private bool _isDragging = false;
        private bool _userIsScrubbingSlider = false;

        public NowPlayingControl(PlaybackService playback)
        {
            InitializeComponent();
            _playback = playback;

            _playback.TrackChanged += OnTrackChanged;
            _playback.ProgressChanged += OnProgressChanged;
            _playback.PlaybackStateChanged += OnPlaybackStateChanged;

            this.Unloaded += (s, e) => {
                _playback.TrackChanged -= OnTrackChanged;
                _playback.ProgressChanged -= OnProgressChanged;
                _playback.PlaybackStateChanged -= OnPlaybackStateChanged;
            };

            SyncPlayPauseControls(_playback.IsPlaying);
            if (_playback.CurrentTrack != null) OnTrackChanged(_playback.CurrentTrack);
        }

        #region Gesture Interaction Engine
        private void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            _startY = e.GetPosition(this).Y;
            _isDragging = true;
            this.CaptureMouse();
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (_isDragging && e.LeftButton == MouseButtonState.Pressed)
            {
                double currentY = e.GetPosition(this).Y;
                if (currentY - _startY > 80) 
                {
                    _isDragging = false;
                    this.ReleaseMouseCapture();
                    RequestCollapse?.Invoke();
                }
            }
        }

        // FIXED SIGNATURE: Perfectly matches delegate 'MouseButtonEventHandler'
        private void OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            _isDragging = false;
            this.ReleaseMouseCapture();
        }

        private void OnTouchDown(object sender, TouchEventArgs e)
        {
            _startY = e.GetTouchPoint(this).Position.Y;
        }

        private void OnTouchMove(object sender, TouchEventArgs e)
        {
            double currentY = e.GetTouchPoint(this).Position.Y;
            if (currentY - _startY > 80)
            {
                RequestCollapse?.Invoke();
            }
        }

        // FIXED SIGNATURE: Perfectly matches delegate 'EventHandler<TouchEventArgs>'
        private void OnTouchUp(object sender, TouchEventArgs e) 
        { 
            // Satisfies touch release registration hooks cleanly
        }
        
        private void BackButton_Click(object sender, RoutedEventArgs e) => RequestCollapse?.Invoke();
        #endregion

        #region Processing Engine Triggers
        private void OnTrackChanged(MusicFile track)
        {
            if (track == null) return;
            Dispatcher.Invoke(() =>
            {
                ProgressSlider.Value = 0;
                ProgressSlider.Maximum = track.Duration.TotalSeconds > 0 ? track.Duration.TotalSeconds : 100;
                TxtTotalTime.Text = FormatTimeInterval(track.Duration);
            });
        }

        private void OnProgressChanged(TimeSpan current, TimeSpan total)
        {
            if (_userIsScrubbingSlider) return; 

            Dispatcher.Invoke(() =>
            {
                ProgressSlider.Maximum = total.TotalSeconds;
                ProgressSlider.Value = current.TotalSeconds;
                TxtCurrentTime.Text = FormatTimeInterval(current);
            });
        }

        private void OnPlaybackStateChanged(bool isPlaying)
        {
            Dispatcher.Invoke(() => SyncPlayPauseControls(isPlaying));
        }

        private void SyncPlayPauseControls(bool isPlaying)
        {
            string token = isPlaying ? "⏸" : "▶";
            BtnMainPlay.Content = token;
        }

        private string FormatTimeInterval(TimeSpan ts)
        {
            return $"{(int)ts.TotalMinutes}:{ts.Seconds:D2}";
        }

        private void Play_Click(object sender, RoutedEventArgs e)
        {
            if (_playback.IsPlaying) _playback.Pause(); else _playback.Resume();
        }

        private void Next_Click(object sender, RoutedEventArgs e) => _playback.Next();
        private void Previous_Click(object sender, RoutedEventArgs e) => _playback.Previous();

        // FIXED: Added missing tracking method required by XAML Thumb.DragStarted
        private void Slider_DragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
        {
            _userIsScrubbingSlider = true;
        }

        // FIXED: Added missing tracking method required by XAML Thumb.DragCompleted
        private void Slider_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            _userIsScrubbingSlider = false;
            _playback.Seek(TimeSpan.FromSeconds(ProgressSlider.Value));
        }
        #endregion
    }
}

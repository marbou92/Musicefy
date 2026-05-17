using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
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

        private enum RightViewMode { None, Lyrics, Queue }
        private RightViewMode _currentMode = RightViewMode.None;

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

        #region Spatial Fluid State Machine (Echo Transitions)
        private void UpdateLayoutState(RightViewMode targetMode)
        {
            if (_currentMode == targetMode)
            {
                // Toggle action: Clicking active item minimizes view back to perfect center baseline
                _currentMode = RightViewMode.None;
            }
            else
            {
                _currentMode = targetMode;
            }

            // Reset UI state vectors
            LyricsPanelContainer.Visibility = Visibility.Collapsed;
            QueuePanelContainer.Visibility = Visibility.Collapsed;
            BtnToggleLyrics.ClearValue(Button.ForegroundProperty);
            BtnToggleQueue.ClearValue(Button.ForegroundProperty);
            QueueIcon.Fill = (Brush)FindResource("MutedTextBrush");
            LyricsIcon.Fill = (Brush)FindResource("MutedTextBrush");

            if (_currentMode == RightViewMode.None)
            {
                // Collapse Right Panel Column, snap player back into centered alignment context
                LeftPlayerColumn.Width = new GridLength(1, GridUnitType.Star);
                RightPanelColumn.Width = new GridLength(0);
                RightPanelRoot.Visibility = Visibility.Collapsed;
                PlayerDeckRoot.HorizontalAlignment = HorizontalAlignment.Center;
            }
            else
            {
                // Dynamic Shift Left Step: Allocate a clean 4.5* to 5.5* asymmetric viewport spread
                LeftPlayerColumn.Width = new GridLength(4.5, GridUnitType.Star);
                RightPanelColumn.Width = new GridLength(5.5, GridUnitType.Star);
                RightPanelRoot.Visibility = Visibility.Visible;
                PlayerDeckRoot.HorizontalAlignment = HorizontalAlignment.Left;

                Brush activeAccent = (Brush)FindResource("AccentBrush");

                if (_currentMode == RightViewMode.Lyrics)
                {
                    LyricsPanelContainer.Visibility = Visibility.Visible;
                    LyricsIcon.Fill = activeAccent;
                }
                else if (_currentMode == RightViewMode.Queue)
                {
                    QueuePanelContainer.Visibility = Visibility.Visible;
                    QueueIcon.Fill = activeAccent;
                }
            }
        }

        private void BtnToggleLyrics_Click(object sender, RoutedEventArgs e) => UpdateLayoutState(RightViewMode.Lyrics);
        private void BtnToggleQueue_Click(object sender, RoutedEventArgs e) => UpdateLayoutState(RightViewMode.Queue);
        #endregion

        #region Gesture Recognition Links
        private void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                _startY = e.GetPosition(this).Y;
                _isDragging = true;
                this.CaptureMouse();
            }
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

        private void OnTouchUp(object sender, TouchEventArgs e) { }
        private void BackButton_Click(object sender, RoutedEventArgs e) => RequestCollapse?.Invoke();
        #endregion

        #region Core Core Engine Event Syncs
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
            BtnMainPlay.Content = isPlaying ? "⏸" : "▶";
        }

        private string FormatTimeInterval(TimeSpan ts) => $"{(int)ts.TotalMinutes}:{ts.Seconds:D2}";

        private void Play_Click(object sender, RoutedEventArgs e)
        {
            if (_playback.IsPlaying) _playback.Pause(); else _playback.Resume();
        }

        private void Next_Click(object sender, RoutedEventArgs e) => _playback.Next();
        private void Previous_Click(object sender, RoutedEventArgs e) => _playback.Previous();

        private void Slider_DragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e) => _userIsScrubbingSlider = true;

        private void Slider_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            _userIsScrubbingSlider = false;
            _playback.Seek(TimeSpan.FromSeconds(ProgressSlider.Value));
        }
        #endregion
    }
}

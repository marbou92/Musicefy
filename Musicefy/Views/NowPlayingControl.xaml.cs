// File Path: Musicefy/Views/NowPlayingControl.xaml.cs
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Musicefy.Services;
using Musicefy.Core.Models;

namespace Musicefy.Views
{
    public partial class NowPlayingControl : UserControl, INotifyPropertyChanged
    {
        private readonly PlaybackService _playback;
        public event Action RequestCollapse;
        
        private double _startY;
        private bool _isDragging = false;
        private bool _userIsScrubbingSlider = false;

        private enum RightViewMode { None, Lyrics, Queue }
        private RightViewMode _currentMode = RightViewMode.None;

        // Expose the current track safely for UI Bindings
        public MusicFile NowPlaying => _playback?.CurrentTrack;

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public NowPlayingControl(PlaybackService playback)
        {
            InitializeComponent();
            _playback = playback;
            
            // Explicitly set the DataContext to this code-behind instance
            this.DataContext = this;

            _playback.TrackChanged += OnTrackChanged;
            _playback.ProgressChanged += OnProgressChanged;
            _playback.PlaybackStateChanged += OnPlaybackStateChanged;

            this.SizeChanged += OnControlSizeChanged;

            this.Unloaded += (s, e) => {
                _playback.TrackChanged -= OnTrackChanged;
                _playback.ProgressChanged -= OnProgressChanged;
                _playback.PlaybackStateChanged -= OnPlaybackStateChanged;
                this.SizeChanged -= OnControlSizeChanged;
            };

            SyncPlayPauseControls(_playback.IsPlaying);
            if (_playback.CurrentTrack != null) OnTrackChanged(_playback.CurrentTrack);
        }

        private void OnControlSizeChanged(object sender, SizeChangedEventArgs e) => ApplyLayoutCalculations();
        private void BtnToggleLyrics_Click(object sender, RoutedEventArgs e) => UpdateLayoutState(RightViewMode.Lyrics);
        private void BtnToggleQueue_Click(object sender, RoutedEventArgs e) => UpdateLayoutState(RightViewMode.Queue);

        private void UpdateLayoutState(RightViewMode targetMode)
        {
            _currentMode = (_currentMode == targetMode) ? RightViewMode.None : targetMode;
            ApplyLayoutCalculations();
        }

        private void ApplyLayoutCalculations()
        {
            LyricsPanelContainer.Visibility = Visibility.Collapsed;
            QueuePanelContainer.Visibility = Visibility.Collapsed;
            QueueIcon.Fill = (Brush)FindResource("MutedTextBrush");
            LyricsIcon.Fill = (Brush)FindResource("MutedTextBrush");

            if (_currentMode == RightViewMode.None)
            {
                LeftPlayerColumn.Width = new GridLength(1, GridUnitType.Star);
                RightPanelColumn.Width = new GridLength(0);
                RightPanelRoot.Visibility = Visibility.Collapsed;
                PlayerDeckRoot.Visibility = Visibility.Visible;
                PlayerDeckRoot.HorizontalAlignment = HorizontalAlignment.Center;
                CoverArtBorder.Height = double.NaN; 
            }
            else
            {
                RightPanelRoot.Visibility = Visibility.Visible;
                Brush activeAccent = (Brush)FindResource("AccentBrush");

                if (this.ActualWidth < 840)
                {
                    LeftPlayerColumn.Width = new GridLength(0);
                    RightPanelColumn.Width = new GridLength(1, GridUnitType.Star);
                    PlayerDeckRoot.Visibility = Visibility.Collapsed;
                    RightPanelRoot.Margin = new Thickness(0, 10, 0, 10);
                }
                else
                {
                    LeftPlayerColumn.Width = new GridLength(4.5, GridUnitType.Star);
                    RightPanelColumn.Width = new GridLength(5.5, GridUnitType.Star);
                    PlayerDeckRoot.Visibility = Visibility.Visible;
                    PlayerDeckRoot.HorizontalAlignment = HorizontalAlignment.Center;
                    RightPanelRoot.Margin = new Thickness(40, 10, 0, 10);
                    CoverArtBorder.Height = double.NaN;
                }

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

        #region Spatial User Input Gesture Recognition
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

        private void OnTouchDown(object sender, TouchEventArgs e) => _startY = e.GetTouchPoint(this).Position.Y;
        private void OnTouchMove(object sender, TouchEventArgs e)
        {
            double currentY = e.GetTouchPoint(this).Position.Y;
            if (currentY - _startY > 80) RequestCollapse?.Invoke();
        }
        private void OnTouchUp(object sender, TouchEventArgs e) { }
        private void BackButton_Click(object sender, RoutedEventArgs e) => RequestCollapse?.Invoke();
        #endregion

        #region Playback Service Event Synchronization
        private void OnTrackChanged(MusicFile track)
        {
            if (track == null) return;
            Dispatcher.Invoke(() =>
            {
                // Push update warnings down to the XAML parsing layer
                OnPropertyChanged(nameof(NowPlaying));

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

        private void OnPlaybackStateChanged(bool isPlaying) => Dispatcher.Invoke(() => SyncPlayPauseControls(isPlaying));
        private void SyncPlayPauseControls(bool isPlaying) => BtnMainPlay.Content = isPlaying ? "⏸" : "▶";
        private string FormatTimeInterval(TimeSpan ts) => $"{(int)ts.TotalMinutes}:{ts.Seconds:D2}";
        private void Play_Click(object sender, RoutedEventArgs e) { if (_playback.IsPlaying) _playback.Pause(); else _playback.Resume(); }
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

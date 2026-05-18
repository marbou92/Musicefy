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

        // Visual states variables
        private bool _isShuffleEnabled = false;
        private bool _isRepeatEnabled = false;
        private bool _isFavoriteTrack = false;

        private enum RightViewMode { None, Lyrics, Queue }
        private RightViewMode _currentMode = RightViewMode.None;

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

        #region Functional Transport Action Event Triggers

        private void Shuffle_Click(object sender, RoutedEventArgs e)
        {
            _isShuffleEnabled = !_isShuffleEnabled;
            // TODO: Wire up to your streaming backend service logic here if needed: _playback.SetShuffle(_isShuffleEnabled);
            
            ShuffleIcon.Fill = _isShuffleEnabled 
                ? (Brush)FindResource("AccentBrush") 
                : (Brush)FindResource("MutedTextBrush");
        }

        private void Repeat_Click(object sender, RoutedEventArgs e)
        {
            _isRepeatEnabled = !_isRepeatEnabled;
            // TODO: Wire up to backend engine logic here if needed: _playback.SetRepeat(_isRepeatEnabled);
            
            RepeatIcon.Fill = _isRepeatEnabled 
                ? (Brush)FindResource("AccentBrush") 
                : (Brush)FindResource("MutedTextBrush");
        }

        private void Favorite_Click(object sender, RoutedEventArgs e)
        {
            _isFavoriteTrack = !_isFavoriteTrack;
            
            if (_isFavoriteTrack)
            {
                FavoriteIcon.Fill = new SolidColorBrush(Color.FromRgb(244, 67, 54)); // Vivid Material Red Heart Fill
                // Vector heart toggle data to completely filled shape representation
                FavoriteIcon.Data = Geometry.Parse("M12,21.35L10.55,20.03C5.4,15.36 2,12.27 2,8.5C2,5.41 4.42,3 7.5,3C9.24,3 10.91,3.81 12,5.08C13.09,3.81 14.76,3 16.5,3C19.58,3 22,5.41 22,8.5C22,12.27 18.6,15.36 13.45,20.03L12,21.35Z");
            }
            else
            {
                FavoriteIcon.Fill = (Brush)FindResource("MutedTextBrush");
                // Vector heart toggle back to clean outline representation
                FavoriteIcon.Data = Geometry.Parse("M12,21.35L10.55,20.03C5.4,15.36 2,12.27 2,8.5C2,5.41 4.42,3 7.5,3C9.24,3 10.91,3.81 12,5.08C13.09,3.81 14.76,3 16.5,3C19.58,3 22,5.41 22,8.5C22,12.27 18.6,15.36 13.45,20.03L12,21.35Z");
            }
        }

        private void MoreOptions_Click(object sender, RoutedEventArgs e)
        {
            // Instantiates an aesthetic localized context menu strip layout on flyby execution
            ContextMenu ctxMenu = new ContextMenu();
            
            MenuItem subItemQueue = new MenuItem { Header = "Add to Queue" };
            MenuItem subItemPlaylist = new MenuItem { Header = "Add to Playlist..." };
            MenuItem subItemArtist = new MenuItem { Header = "Go to Artist View" };
            MenuItem subItemInfo = new MenuItem { Header = "View Track Details" };

            ctxMenu.Items.Add(subItemQueue);
            ctxMenu.Items.Add(subItemPlaylist);
            ctxMenu.Items.Add(new Separator());
            ctxMenu.Items.Add(subItemArtist);
            ctxMenu.Items.Add(subItemInfo);

            ctxMenu.PlacementTarget = sender as Button;
            ctxMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            ctxMenu.IsOpen = true;
        }

        #endregion

        #region Playback Service Event Synchronization
        private void OnTrackChanged(MusicFile track)
        {
            if (track == null) return;
            Dispatcher.Invoke(() =>
            {
                OnPropertyChanged(nameof(NowPlaying));

                // Reset dynamic contextual state flags on new track updates
                _isFavoriteTrack = false; 
                FavoriteIcon.Fill = (Brush)FindResource("MutedTextBrush");

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

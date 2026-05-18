using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
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

        private void OnControlSizeChanged(object sender, SizeChangedEventArgs e) => ApplyLayoutCalculations(false);
        private void BtnToggleLyrics_Click(object sender, RoutedEventArgs e) => UpdateLayoutState(RightViewMode.Lyrics);
        private void BtnToggleQueue_Click(object sender, RoutedEventArgs e) => UpdateLayoutState(RightViewMode.Queue);

        private void UpdateLayoutState(RightViewMode targetMode)
        {
            _currentMode = (_currentMode == targetMode) ? RightViewMode.None : targetMode;
            ApplyLayoutCalculations(true);
        }

        /// <summary>
        /// Handles responsive scaling updates and triggers fluid WPF DoubleAnimations 
        /// instead of jarring visibility jumps when changing panel view states.
        /// </summary>
        private void ApplyLayoutCalculations(bool animateTransition)
        {
            if (RightPanelRoot == null || LeftPlayerColumn == null || RightPanelColumn == null) return;

            // Resolve base shared vector resource colors
            Brush mutedBrush = (Brush)FindResource("MutedTextBrush");
            Brush activeAccent = (Brush)FindResource("AccentBrush");

            // Clear visual indicators back to base unselected neutral
            BtnToggleLyrics.Foreground = mutedBrush;
            BtnToggleQueue.Foreground = mutedBrush;

            if (_currentMode == RightViewMode.None)
            {
                LeftPlayerColumn.Width = new GridLength(1, GridUnitType.Star);
                RightPanelColumn.Width = new GridLength(0);
                
                if (animateTransition)
                {
                    AnimateFadeOutCollapse(RightPanelRoot);
                }
                else
                {
                    RightPanelRoot.Visibility = Visibility.Collapsed;
                }

                PlayerDeckRoot.Visibility = Visibility.Visible;
                PlayerDeckRoot.HorizontalAlignment = HorizontalAlignment.Center;
            }
            else
            {
                // Assign highlighted states to the respective toggles
                if (_currentMode == RightViewMode.Lyrics)
                {
                    LyricsPanelContainer.Visibility = Visibility.Visible;
                    QueuePanelContainer.Visibility = Visibility.Collapsed;
                    BtnToggleLyrics.Foreground = activeAccent;
                }
                else if (_currentMode == RightViewMode.Queue)
                {
                    QueuePanelContainer.Visibility = Visibility.Visible;
                    LyricsPanelContainer.Visibility = Visibility.Collapsed;
                    BtnToggleQueue.Foreground = activeAccent;
                }

                // Window Size Responsiveness Logic
                if (this.ActualWidth < 840)
                {
                    // Miniplayer/Compact sizing threshold: Hide album profile deck to prioritize lyrics visibility layout
                    LeftPlayerColumn.Width = new GridLength(0);
                    RightPanelColumn.Width = new GridLength(1, GridUnitType.Star);
                    PlayerDeckRoot.Visibility = Visibility.Collapsed;
                    RightPanelRoot.Margin = new Thickness(0, 10, 0, 10);
                }
                else
                {
                    // Expanded Sizing Configuration
                    LeftPlayerColumn.Width = new GridLength(4.5, GridUnitType.Star);
                    RightPanelColumn.Width = new GridLength(5.5, GridUnitType.Star);
                    PlayerDeckRoot.Visibility = Visibility.Visible;
                    PlayerDeckRoot.HorizontalAlignment = HorizontalAlignment.Center;
                    RightPanelRoot.Margin = new Thickness(48, 10, 0, 10);
                }

                if (animateTransition && RightPanelRoot.Visibility != Visibility.Visible)
                {
                    AnimateFadeInExpand(RightPanelRoot);
                }
                else
                {
                    RightPanelRoot.Visibility = Visibility.Visible;
                    RightPanelRoot.Opacity = 1.0;
                    if (RightPanelRoot.RenderTransform is TranslateTransform tt) tt.X = 0;
                }
            }
        }

        private void AnimateFadeInExpand(FrameworkElement element)
        {
            element.Visibility = Visibility.Visible;
            
            DoubleAnimation fadeIn = new DoubleAnimation(0.0,  1.0, TimeSpan.FromMilliseconds(350))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            
            DoubleAnimation slideIn = new DoubleAnimation(30, 0, TimeSpan.FromMilliseconds(400))
            {
                EasingFunction = new DecelerateEase()
            };

            element.BeginAnimation(UIElement.OpacityProperty, fadeIn);
            if (element.RenderTransform is TranslateTransform translate)
            {
                translate.BeginAnimation(TranslateTransform.XProperty, slideIn);
            }
        }

        private void AnimateFadeOutCollapse(FrameworkElement element)
        {
            DoubleAnimation fadeOut = new DoubleAnimation(element.Opacity, 0.0, TimeSpan.FromMilliseconds(250))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
            };
            
            fadeOut.Completed += (s, e) => {
                if (_currentMode == RightViewMode.None) element.Visibility = Visibility.Collapsed;
            };

            element.BeginAnimation(UIElement.OpacityProperty, fadeOut);
        }

        #region Interaction Gesture Trackers
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

        #region Functional Transport Controls Actions
        private void Shuffle_Click(object sender, RoutedEventArgs e)
        {
            _isShuffleEnabled = !_isShuffleEnabled;
            ShuffleIcon.Fill = _isShuffleEnabled ? (Brush)FindResource("AccentBrush") : (Brush)FindResource("MutedTextBrush");
        }

        private void Repeat_Click(object sender, RoutedEventArgs e)
        {
            _isRepeatEnabled = !_isRepeatEnabled;
            RepeatIcon.Fill = _isRepeatEnabled ? (Brush)FindResource("AccentBrush") : (Brush)FindResource("MutedTextBrush");
        }

        private void Favorite_Click(object sender, RoutedEventArgs e)
        {
            _isFavoriteTrack = !_isFavoriteTrack;
            
            if (_isFavoriteTrack)
            {
                // Bright Aesthetic Heart Fill Match
                FavoriteIcon.Fill = new SolidColorBrush(Color.FromRgb(244, 67, 54));
                FavoriteIcon.Data = Geometry.Parse("M12,21.35L10.55,20.03C5.4,15.36 2,12.27 2,8.5C2,5.41 4.42,3 7.5,3C9.24,3 10.91,3.81 12,5.08C13.09,3.81 14.76,3 16.5,3C19.58,3 22,5.41 22,8.5C22,12.27 18.6,15.36 13.45,20.03L12,21.35Z");
            }
            else
            {
                // Dynamic clean vector path mapping back to outline form factor
                FavoriteIcon.ClearValue(Shape.FillProperty);
                FavoriteIcon.Data = Geometry.Parse("M12,21.35L10.55,20.03C5.4,15.36 2,12.27 2,8.5C2,5.41 4.42,3 7.5,3C9.24,3 10.91,3.81 12,5.08C13.09,3.81 14.76,3 16.5,3C19.58,3 22,5.41 22,8.5C22,12.27 18.6,15.36 13.45,20.03L12,21.35ZM7.5,5C5.5,5 4,6.5 4,8.5C4,11 6.33,13.6 12,18.52C17.67,13.6 20,11 20,8.5C20,6.5 18.5,5 16.5,5C15.15,5 13.87,5.88 13.39,7.1H10.61C10.13,5.88 8.85,5 7.5,5Z");
            }
        }

        private void MoreOptions_Click(object sender, RoutedEventArgs e)
        {
            ContextMenu ctxMenu = new ContextMenu();
            ctxMenu.Items.Add(new MenuItem { Header = "Add to Queue" });
            ctxMenu.Items.Add(new MenuItem { Header = "Add to Playlist..." });
            ctxMenu.Items.Add(new Separator());
            ctxMenu.Items.Add(new MenuItem { Header = "Go to Artist View" });
            ctxMenu.Items.Add(new MenuItem { Header = "View Track Details" });

            ctxMenu.PlacementTarget = sender as Button;
            ctxMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            ctxMenu.IsOpen = true;
        }

        private void Play_Click(object sender, RoutedEventArgs e)
        {
            if (_playback.IsPlaying) _playback.Pause(); else _playback.Play();
        }

        private void Previous_Click(object sender, RoutedEventArgs e) => _playback.Previous();
        private void Next_Click(object sender, RoutedEventArgs e) => _playback.Next();

        private void Slider_DragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e) => _userIsScrubbingSlider = true;
        private void Slider_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            _userIsScrubbingSlider = false;
            _playback.Seek(TimeSpan.FromSeconds(ProgressSlider.Value));
        }
        #endregion

        #region Service Synchronization Sync Core Engines
        private void OnTrackChanged(MusicFile track)
        {
            if (track == null) return;
            Dispatcher.Invoke(() =>
            {
                OnPropertyChanged(nameof(NowPlaying));
                _isFavoriteTrack = false; 
                FavoriteIcon.ClearValue(Shape.FillProperty);

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
                ProgressSlider.Value = current.TotalSeconds;
                TxtCurrentTime.Text = FormatTimeInterval(current);
            });
        }

        private void OnPlaybackStateChanged(bool isPlaying) => Dispatcher.Invoke(() => SyncPlayPauseControls(isPlaying));

        private void SyncPlayPauseControls(bool isPlaying) => BtnMainPlay.Content = isPlaying ? "⏸" : "▶";

        private string FormatTimeInterval(TimeSpan ts) => $"{(int)ts.TotalMinutes}:{ts.Seconds:D2}";
        #endregion
    }
}

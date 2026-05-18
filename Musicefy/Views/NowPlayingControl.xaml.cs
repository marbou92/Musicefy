using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
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

            // Resolve dynamic accent color resource
            Brush activeAccent = (Brush)FindResource("AccentBrush");

            // Clear local values so elements fall back cleanly to their dynamic XAML Template Storyboards
            BtnToggleLyrics.ClearValue(Control.ForegroundProperty);
            BtnToggleQueue.ClearValue(Control.ForegroundProperty);

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
                // Assign highlighted states to active selections
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

                // Window Size Responsive Layout Engine Switcher
                if (this.ActualWidth < 840)
                {
                    // Miniplayer Mode: Collapse left main deck to focus purely on lyrics/queue panels
                    LeftPlayerColumn.Width = new GridLength(0);
                    RightPanelColumn.Width = new GridLength(1, GridUnitType.Star);
                    PlayerDeckRoot.Visibility = Visibility.Collapsed;
                    RightPanelRoot.Margin = new Thickness(0, 10, 0, 10);
                }
                else
                {
                    // Widescreen Dual-Pane Panel Mode
                    LeftPlayerColumn.Width = new GridLength(4.5, GridUnitType.Star);
                    RightPanelColumn.Width = new GridLength(5.5, GridUnitType.Star);
                    PlayerDeckRoot.Visibility = Visibility.Visible;
                    PlayerDeckRoot.HorizontalAlignment = HorizontalAlignment.Center;
                    RightPanelRoot.Margin = new Thickness(44, 10, 0, 10);
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
            
            DoubleAnimation fadeIn = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(350))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            
            DoubleAnimation slideIn = new DoubleAnimation(30, 0, TimeSpan.FromMilliseconds(400))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            element.BeginAnimation(UIElement.OpacityProperty, fadeIn);
            if (element.RenderTransform is TranslateTransform translate)
            {
                translate.BeginAnimation(TranslateTransform.XProperty, slideIn);
            }
        }

        private void AnimateFadeOutCollapse(FrameworkElement element)
        {
            DoubleAnimation fadeOut = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(250))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };

            fadeOut.Completed += (s, e) => { element.Visibility = Visibility.Collapsed; };
            element.BeginAnimation(UIElement.OpacityProperty, fadeOut);
        }

        // ==========================================
        // Event Handlers for Buttons & Playback
        // ==========================================

        private void BackButton_Click(object sender, RoutedEventArgs e) => RequestCollapse?.Invoke();

        private void Play_Click(object sender, RoutedEventArgs e) => _ = _playbackService.IsPlaying ? _playbackService.Pause() : _playbackService.Resume();

        private void Previous_Click(object sender, RoutedEventArgs e) => _playbackService.Previous();

        private void Next_Click(object sender, RoutedEventArgs e) => _playbackService.Next();

        private void Shuffle_Click(object sender, RoutedEventArgs e)
        {
            _isShuffleEnabled = !_isShuffleEnabled;
            // Update UI color or icon state
        }

        private void Repeat_Click(object sender, RoutedEventArgs e)
        {
            _isRepeatEnabled = !_isRepeatEnabled;
            // Update UI color or icon state
        }

        private void Favorite_Click(object sender, RoutedEventArgs e)
        {
            _isFavoriteTrack = !_isFavoriteTrack;
            // Add favorite logic and update FavoriteIcon path/color
        }

        private void Share_Click(object sender, RoutedEventArgs e)
        {
            // Trigger share dialog logic
            MessageBox.Show("Share track: " + NowPlaying?.Title);
        }

        private void MoreOptions_Click(object sender, RoutedEventArgs e)
        {
            // Open Context Menu or Dropdown with more options
        }

        private void SleepTimer_Click(object sender, RoutedEventArgs e)
        {
            // Add sleep timer logic
            MessageBox.Show("Sleep timer dialog");
        }

        private void Slider_DragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
        {
            _userIsScrubbingSlider = true;
        }

        private void Slider_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            _userIsScrubbingSlider = false;
            if (_playback.CurrentTrack != null)
            {
                // Seek logic mapping ProgressSlider.Value back to playback position
                // _playback.SeekTo(ProgressSlider.Value);
            }
        }

        private void OnTrackChanged(MusicFile track)
        {
            Dispatcher.Invoke(() => {
                OnPropertyChanged(nameof(NowPlaying));
                // Update specific track logic here
            });
        }

        private void OnProgressChanged(TimeSpan currentTime, TimeSpan totalTime)
        {
            Dispatcher.Invoke(() => {
                if (!_userIsScrubbingSlider && totalTime > 0)
                {
                    ProgressSlider.Value = (currentTime / totalTime) * 100;
                    TxtCurrentTime.Text = TimeSpan.FromSeconds(currentTime).ToString(@"m\:ss");
                    TxtTotalTime.Text = TimeSpan.FromSeconds(totalTime).ToString(@"m\:ss");
                }
            });
        }

        private void OnPlaybackStateChanged(bool isPlaying)
        {
            Dispatcher.Invoke(() => {
                SyncPlayPauseControls(isPlaying);
            });
        }

        private void SyncPlayPauseControls(bool isPlaying)
        {
            BtnMainPlay.Content = isPlaying ? "⏸" : "▶";
        }

        // Window drag logic (if applicable from the user controls layout)
        private void OnMouseDown(object sender, MouseButtonEventArgs e) { }
        private void OnMouseMove(object sender, MouseEventArgs e) { }
        private void OnMouseUp(object sender, MouseButtonEventArgs e) { }
        private void OnTouchDown(object sender, TouchEventArgs e) { }
        private void OnTouchMove(object sender, TouchEventArgs e) { }
        private void OnTouchUp(object sender, TouchEventArgs e) { }
    }
}

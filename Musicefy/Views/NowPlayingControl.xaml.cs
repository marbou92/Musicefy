using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Musicefy.Services;
using Musicefy.ViewModels;
using Musicefy.Core.Models;

namespace Musicefy.Views
{
    public partial class NowPlayingControl : UserControl
    {
        private readonly PlaybackService _playback;
        private readonly NowPlayingViewModel _viewModel;
        private bool _isMouseOverQueue;
        private DispatcherTimer _queueScrollTimer;
        public event Action RequestCollapse;

        public NowPlayingControl(PlaybackService playback, NowPlayingViewModel viewModel)
        {
            InitializeComponent();
            _playback = playback;
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));

            this.DataContext = _viewModel;
            _viewModel.RequestCollapse += () => RequestCollapse?.Invoke();
            _viewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(NowPlayingViewModel.IsLyricsPanelVisible) ||
                    e.PropertyName == nameof(NowPlayingViewModel.IsQueuePanelVisible) ||
                    e.PropertyName == nameof(NowPlayingViewModel.IsRightPanelVisible))
                {
                    ApplyLayoutCalculations();
                }

                if (e.PropertyName == nameof(NowPlayingViewModel.NowPlaying))
                {
                    if (_isMouseOverQueue)
                        _queueScrollTimer.Stop();
                    else
                        ScrollToNowPlaying();
                }
            };

            this.SizeChanged += (s, e) =>
            {
                _viewModel.UpdateActualWidth(this.ActualWidth);
                ApplyLayoutCalculations();
            };

            _queueScrollTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(7) };
            _queueScrollTimer.Tick += (s, e) =>
            {
                _queueScrollTimer.Stop();
                ScrollToNowPlaying();
            };

            if (_playback.CurrentTrack != null) OnTrackChanged(_playback.CurrentTrack);
        }

        private void ScrollToNowPlaying()
        {
            if (QueueItemsControl == null || QueueScrollViewer == null || _viewModel.NowPlaying == null) return;

            var container = QueueItemsControl.ItemContainerGenerator.ContainerFromItem(_viewModel.NowPlaying) as UIElement;
            if (container == null) return;

            var transform = container.TransformToAncestor(QueueScrollViewer);
            var offset = transform.Transform(new Point(0, 0));
            QueueScrollViewer.ScrollToVerticalOffset(QueueScrollViewer.VerticalOffset + offset.Y);
        }

        private void ScheduleQueueScroll()
        {
            _queueScrollTimer.Stop();
            _queueScrollTimer.Start();
        }

        private void QueueScrollViewer_MouseEnter(object sender, MouseEventArgs e)
        {
            _isMouseOverQueue = true;
            _queueScrollTimer.Stop();
        }

        private void QueueScrollViewer_MouseLeave(object sender, MouseEventArgs e)
        {
            _isMouseOverQueue = false;
            ScheduleQueueScroll();
        }

        private void ApplyLayoutCalculations()
        {
            var targetMode = _viewModel.IsLyricsPanelVisible ? NowPlayingViewModel.RightViewMode.Lyrics :
                             _viewModel.IsQueuePanelVisible ? NowPlayingViewModel.RightViewMode.Queue :
                             NowPlayingViewModel.RightViewMode.None;
            UpdateLayoutState(targetMode);
        }

        private void UpdateLayoutState(NowPlayingViewModel.RightViewMode targetMode)
        {
            if (RightPanelRoot == null || LeftPlayerColumn == null || RightPanelColumn == null) return;

            Brush activeAccent = (Brush)FindResource("AccentBrush");
            BtnToggleLyrics.ClearValue(Control.ForegroundProperty);
            BtnToggleQueue.ClearValue(Control.ForegroundProperty);

            bool isVisible = targetMode != NowPlayingViewModel.RightViewMode.None;

            if (!isVisible)
            {
                LeftPlayerColumn.Width = new GridLength(1, GridUnitType.Star);
                RightPanelColumn.Width = new GridLength(0);
                AnimateFadeOutCollapse(RightPanelRoot);
                PlayerDeckRoot.Visibility = Visibility.Visible;
                PlayerDeckRoot.HorizontalAlignment = HorizontalAlignment.Center;
            }
            else
            {
                if (targetMode == NowPlayingViewModel.RightViewMode.Lyrics)
                {
                    LyricsPanelContainer.Visibility = Visibility.Visible;
                    QueuePanelContainer.Visibility = Visibility.Collapsed;
                    BtnToggleLyrics.Foreground = activeAccent;
                }
                else if (targetMode == NowPlayingViewModel.RightViewMode.Queue)
                {
                    QueuePanelContainer.Visibility = Visibility.Visible;
                    LyricsPanelContainer.Visibility = Visibility.Collapsed;
                    BtnToggleQueue.Foreground = activeAccent;
                    ScheduleQueueScroll();
                }

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
                    RightPanelRoot.Margin = new Thickness(44, 10, 0, 10);
                }

                if (RightPanelRoot.Visibility != Visibility.Visible)
                    AnimateFadeInExpand(RightPanelRoot);
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
            var fadeIn = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(350))
                { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
            var slideIn = new DoubleAnimation(30, 0, TimeSpan.FromMilliseconds(400))
                { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
            element.BeginAnimation(OpacityProperty, fadeIn);
            if (element.RenderTransform is TranslateTransform translate)
                translate.BeginAnimation(TranslateTransform.XProperty, slideIn);
        }

        private void AnimateFadeOutCollapse(FrameworkElement element)
        {
            var fadeOut = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(250))
                { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn } };
            fadeOut.Completed += (s, e) => { element.Visibility = Visibility.Collapsed; };
            element.BeginAnimation(OpacityProperty, fadeOut);
        }

        private void Slider_DragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
            => _viewModel.IsUserScrubbing = true;

        private void Slider_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            _viewModel.IsUserScrubbing = false;
            _viewModel.SeekToPercent(ProgressSlider.Value);
        }

        private void OnTrackChanged(MusicFile track) { }
    }
}

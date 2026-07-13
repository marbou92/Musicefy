using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Musicefy.Core.Interfaces;
using Musicefy.Core.Models;
using Musicefy.ViewModels;

namespace Musicefy.Views
{
    public partial class NowPlayingControl : UserControl
    {
        private readonly IAudioPlayer _playback;
        private readonly NowPlayingViewModel _viewModel;
        private bool _isMouseOverQueue;
        private DispatcherTimer _queueScrollTimer;
        public event Action RequestCollapse;

        public NowPlayingControl(IAudioPlayer playback, NowPlayingViewModel viewModel)
        {
            InitializeComponent();
            _playback = playback;
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));

            this.DataContext = _viewModel;
            _viewModel.RequestCollapse += OnRequestCollapse;
            _viewModel.RequestNavigateToArtist += OnRequestNavigateToArtist;
            _viewModel.RequestNavigateToAlbum += OnRequestNavigateToAlbum;
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            this.Unloaded += OnUnloaded;

            _queueScrollTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(7) };
            _queueScrollTimer.Tick += OnQueueScrollTimerTick;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            _viewModel.RequestCollapse -= OnRequestCollapse;
            _viewModel.RequestNavigateToArtist -= OnRequestNavigateToArtist;
            _viewModel.RequestNavigateToAlbum -= OnRequestNavigateToAlbum;
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _queueScrollTimer.Tick -= OnQueueScrollTimerTick;
            _queueScrollTimer.Stop();
        }

        private void OnQueueScrollTimerTick(object sender, EventArgs e)
        {
            _queueScrollTimer.Stop();
            ScrollToNowPlaying();
        }

        private void OnRequestCollapse() => RequestCollapse?.Invoke();

        private void OnRequestNavigateToArtist(string artistName)
        {
            if (Window.GetWindow(this) is MainWindow mainWindow)
            {
                // Phase 1: Build an ArtistInfo from the now-playing track so that
                // the ArtistViewModel can use YouTube channel ID if available.
                var artistInfo = BuildArtistInfoFromNowPlaying();
                mainWindow.NavigateToArtist(artistInfo);
            }
        }

        private void OnRequestNavigateToAlbum(string albumName, string artistName)
        {
            if (Window.GetWindow(this) is MainWindow mainWindow)
            {
                // Phase 1: Build an AlbumInfo from the now-playing track so that
                // the AlbumViewModel can use YouTube album browse ID if available.
                var albumInfo = BuildAlbumInfoFromNowPlaying();
                mainWindow.NavigateToAlbum(albumInfo);
            }
        }

        /// <summary>
        /// Build an <see cref="ArtistInfo"/> from the currently playing track,
        /// preserving YouTube channel ID for rich browsing.
        /// </summary>
        private ArtistInfo BuildArtistInfoFromNowPlaying()
        {
            var track = _viewModel.NowPlaying;
            if (track == null) return null;

            var info = new ArtistInfo
            {
                Name = track.Artist,
                SourceType = track.SourceType
            };

            if (!string.IsNullOrEmpty(track.ArtistBrowseId))
            {
                info.Id = track.ArtistBrowseId;
                info.YouTubeChannelId = track.ArtistBrowseId;
            }

            return info;
        }

        /// <summary>
        /// Build an <see cref="AlbumInfo"/> from the currently playing track,
        /// preserving YouTube album browse ID for rich browsing.
        /// </summary>
        private AlbumInfo BuildAlbumInfoFromNowPlaying()
        {
            var track = _viewModel.NowPlaying;
            if (track == null) return null;

            var info = new AlbumInfo
            {
                Name = track.Album,
                Artist = track.Artist,
                // Phase 2: Set ArtistId from track's ArtistBrowseId for reliable navigation
                ArtistId = track.ArtistBrowseId,
                Year = track.Year,
                SourceType = track.SourceType
            };

            if (!string.IsNullOrEmpty(track.AlbumBrowseId))
            {
                info.Id = track.AlbumBrowseId;
                info.YouTubeAlbumId = track.AlbumBrowseId;
            }

            return info;
        }

        private void ArtistText_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_viewModel.NowPlaying != null && !string.IsNullOrEmpty(_viewModel.NowPlaying.Artist))
                OnRequestNavigateToArtist(_viewModel.NowPlaying.Artist);
        }

        private void OnViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(NowPlayingViewModel.IsLyricsPanelVisible))
                ToggleLyricsOverlay(_viewModel.IsLyricsPanelVisible);

            if (e.PropertyName == nameof(NowPlayingViewModel.IsQueuePanelVisible))
                ToggleQueueOverlay(_viewModel.IsQueuePanelVisible);

            if (e.PropertyName == nameof(NowPlayingViewModel.NowPlaying))
            {
                if (_isMouseOverQueue)
                    _queueScrollTimer.Stop();
                else
                    ScrollToNowPlaying();
            }
        }

        private void ToggleLyricsOverlay(bool show)
        {
            if (LyricsOverlay == null) return;

            if (show)
            {
                QueueOverlay.Visibility = Visibility.Collapsed;
                QueueOverlay.Opacity = 0;
                if (QueueOverlay.RenderTransform is TranslateTransform qt) qt.Y = 100;

                LyricsOverlay.Visibility = Visibility.Visible;
                var slideUp = new DoubleAnimation(100, 0, TimeSpan.FromMilliseconds(350))
                { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
                var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300));
                LyricsOverlay.BeginAnimation(OpacityProperty, fadeIn);
                if (LyricsOverlay.RenderTransform is TranslateTransform lt)
                    lt.BeginAnimation(TranslateTransform.YProperty, slideUp);
            }
            else
            {
                var slideDown = new DoubleAnimation(0, 100, TimeSpan.FromMilliseconds(250))
                { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn } };
                var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(200));
                fadeOut.Completed += (s, args) => LyricsOverlay.Visibility = Visibility.Collapsed;
                LyricsOverlay.BeginAnimation(OpacityProperty, fadeOut);
                if (LyricsOverlay.RenderTransform is TranslateTransform lt)
                    lt.BeginAnimation(TranslateTransform.YProperty, slideDown);
            }
        }

        private void ToggleQueueOverlay(bool show)
        {
            if (QueueOverlay == null) return;

            if (show)
            {
                LyricsOverlay.Visibility = Visibility.Collapsed;
                LyricsOverlay.Opacity = 0;
                if (LyricsOverlay.RenderTransform is TranslateTransform lt) lt.Y = 100;

                QueueOverlay.Visibility = Visibility.Visible;
                ScheduleQueueScroll();
                var slideUp = new DoubleAnimation(100, 0, TimeSpan.FromMilliseconds(350))
                { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
                var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300));
                QueueOverlay.BeginAnimation(OpacityProperty, fadeIn);
                if (QueueOverlay.RenderTransform is TranslateTransform qt)
                    qt.BeginAnimation(TranslateTransform.YProperty, slideUp);
            }
            else
            {
                var slideDown = new DoubleAnimation(0, 100, TimeSpan.FromMilliseconds(250))
                { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn } };
                var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(200));
                fadeOut.Completed += (s, args) => QueueOverlay.Visibility = Visibility.Collapsed;
                QueueOverlay.BeginAnimation(OpacityProperty, fadeOut);
                if (QueueOverlay.RenderTransform is TranslateTransform qt)
                    qt.BeginAnimation(TranslateTransform.YProperty, slideDown);
            }
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

        private void Slider_DragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
            => _viewModel.IsUserScrubbing = true;

        private void Slider_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            _viewModel.IsUserScrubbing = false;
            _viewModel.SeekToPercent(ProgressSlider.Value);
        }

        /// <summary>
        /// Sprint 9: Handle value changes from the custom Wavy/Squiggly sliders.
        /// Called when the user drags or taps the custom slider.
        /// </summary>
        private void CustomSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (sender is Controls.WavySlider wavy)
            {
                _viewModel.IsUserScrubbing = true;
                _viewModel.SeekToPercent(wavy.Value);
                _viewModel.IsUserScrubbing = false;
            }
            else if (sender is Controls.SquigglySlider squiggly)
            {
                _viewModel.IsUserScrubbing = true;
                _viewModel.SeekToPercent(squiggly.Value);
                _viewModel.IsUserScrubbing = false;
            }
        }

        /// <summary>
        /// Sprint 9: Switch the player slider style based on settings.
        /// Called on Loaded and when the setting changes.
        /// </summary>
        public void UpdateSliderStyle()
        {
            var style = Musicefy.Properties.Settings.Default.PlayerSliderStyle ?? "Default";
            var isPlaying = _viewModel?.IsPlaying ?? false;

            switch (style.ToUpperInvariant())
            {
                case "WAVY":
                    ProgressSlider.Visibility = Visibility.Collapsed;
                    SquigglyProgressSlider.Visibility = Visibility.Collapsed;
                    WavyProgressSlider.Visibility = Visibility.Visible;
                    WavyProgressSlider.IsPlaying = isPlaying;
                    WavyProgressSlider.StartAnimation();
                    SquigglyProgressSlider.StopAnimation();
                    break;

                case "SQUIGGLY":
                    ProgressSlider.Visibility = Visibility.Collapsed;
                    WavyProgressSlider.Visibility = Visibility.Collapsed;
                    SquigglyProgressSlider.Visibility = Visibility.Visible;
                    SquigglyProgressSlider.IsPlaying = isPlaying;
                    SquigglyProgressSlider.StartAnimation();
                    WavyProgressSlider.StopAnimation();
                    break;

                default:
                    ProgressSlider.Visibility = Visibility.Visible;
                    WavyProgressSlider.Visibility = Visibility.Collapsed;
                    SquigglyProgressSlider.Visibility = Visibility.Collapsed;
                    WavyProgressSlider.StopAnimation();
                    SquigglyProgressSlider.StopAnimation();
                    break;
            }
        }
    }
}

using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Musicefy.Views;
using Musicefy.Services;
using Musicefy.ViewModels;
using Musicefy.Core.Models;

namespace Musicefy
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private readonly PlaybackService _playback;
        private readonly MainViewModel _mainViewModel;
        private NowPlayingControl _nowPlayingView;
        private bool _isInitializing = true;

        public MusicFile NowPlaying => _playback?.CurrentTrack;

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public MainWindow()
        {
            _mainViewModel = new MainViewModel();
            _playback = new PlaybackService();
            
            this.DataContext = this;
            
            InitializeComponent();

            AttachCustomTitleBarWindowActions();

            _isInitializing = false;
            SidebarList.SelectedIndex = 0;

            _playback.TrackChanged += OnTrackChanged;
            _playback.PlaybackStateChanged += OnPlaybackStateChanged;
        }

        private void AttachCustomTitleBarWindowActions()
        {
            this.Loaded += (s, e) =>
            {
                var btnMinimize = this.Template.FindName("BtnShellMinimize", this) as Button;
                var btnMaximize = this.Template.FindName("BtnShellMaximize", this) as Button;
                var btnClose = this.Template.FindName("BtnShellClose", this) as Button;
        
                if (btnMinimize != null) btnMinimize.Click += (o, a) => this.WindowState = WindowState.Minimized;
                if (btnMaximize != null) btnMaximize.Click += (o, a) => 
                {
                    this.WindowState = (this.WindowState == WindowState.Maximized) ? WindowState.Normal : WindowState.Maximized;
                };
                if (btnClose != null) btnClose.Click += (o, a) => this.Close();
            };
        }

        private void OnTrackChanged(MusicFile track)
        {
            Dispatcher.Invoke(() =>
            {
                OnPropertyChanged(nameof(NowPlaying));
                UpdateMiniPlayerVisibility();
            });
        }

        private void Sidebar_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing || MainContent == null || _mainViewModel == null || _playback == null) 
                return;

            if (SidebarList.SelectedItem == null) return;

            if (SidebarList.SelectedItem == SettingsItem)
            {
                new SettingsWindow { Owner = this }.ShowDialog();
                SidebarList.SelectedIndex = 0; 
                return;
            }

            UserControl nextView = null;
            switch (SidebarList.SelectedIndex)
            {
                case 0: nextView = new HomeControl(_playback, _mainViewModel); break;
                case 1: nextView = new SearchControl(_playback); break;
                case 2: nextView = new LibraryControl(_playback); break;
            }

            if (nextView != null)
            {
                NavigateWithFade(newContent: nextView);
            }
        }

        private void NavigateWithFade(UserControl newContent)
        {
            DoubleAnimation fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(150));
            fadeOut.Completed += (s, e) =>
            {
                MainContent.Content = newContent;
                this.UpdateLayout();

                DoubleAnimation fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200));
                MainContent.BeginAnimation(OpacityProperty, fadeIn);
            };
            MainContent.BeginAnimation(OpacityProperty, fadeOut);
        }

    #region Mini-Player Pipeline Slide Controllers
        private bool _isDraggingMiniPlayer = false;
        private Point _dragStartPoint;
        private double _dragStartTranslateX;
        private double _dragStartTranslateY;
        private bool _hasDraggedSignificantly = false;
        private bool _isFullPanelOpen = false;

        private void UpdateMiniPlayerVisibility()
        {
            if (NowPlaying == null)
            {
                MiniPlayerBar.Visibility = Visibility.Collapsed;
            }
            else if (!_isFullPanelOpen)
            {
                MiniPlayerBar.Visibility = Visibility.Visible;
                
                // Clear any ongoing animation clocks to restore base interactive states
                MiniPlayerBar.BeginAnimation(UIElement.OpacityProperty, null);
                MiniPlayerTransform.BeginAnimation(TranslateTransform.YProperty, null);
                MiniPlayerTransform.BeginAnimation(TranslateTransform.XProperty, null);
                
                MiniPlayerBar.Opacity = 1;
                MiniPlayerTransform.X = 0;
                MiniPlayerTransform.Y = 0;
            }
            else
            {
                MiniPlayerBar.Visibility = Visibility.Collapsed;
            }
        }

        private void MiniPlayerBar_Click(object sender, MouseButtonEventArgs e)
        {
            // Retained mapping reference node context placeholder if needed for alternative visual hooks
        }

        private void MiniPlayerBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _isDraggingMiniPlayer = true;
            _dragStartPoint = e.GetPosition(this);
            _dragStartTranslateX = MiniPlayerTransform.X;
            _dragStartTranslateY = MiniPlayerTransform.Y;
            _hasDraggedSignificantly = false;
            
            MiniPlayerBar.CaptureMouse();
        }

        private void MiniPlayerBar_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDraggingMiniPlayer) return;

            Point currentPoint = e.GetPosition(this);
            double deltaX = currentPoint.X - _dragStartPoint.X;
            double deltaY = currentPoint.Y - _dragStartPoint.Y;

            if (!_hasDraggedSignificantly && (Math.Abs(deltaX) > 5 || Math.Abs(deltaY) > 5))
            {
                _hasDraggedSignificantly = true;
            }

            // 1. Horizontal Tracking Constraints
            double targetX = _dragStartTranslateX + deltaX;
            double maxOffset = (this.ActualWidth - MiniPlayerBar.Width) / 2.0 - 16; 
            if (maxOffset < 0) maxOffset = 0;

            if (targetX < -maxOffset) targetX = -maxOffset;
            if (targetX > maxOffset) targetX = maxOffset;
            MiniPlayerTransform.X = targetX;

            // 2. Vertical Drag-to-Dismiss Tracking (Only allow downward drag)
            if (deltaY > 0)
            {
                MiniPlayerTransform.Y = _dragStartTranslateY + deltaY;
                
                // Dynamically fade out the player the further down it goes
                double dragOpacity = 1.0 - (deltaY / 120.0); 
                MiniPlayerBar.Opacity = Math.Max(0.2, dragOpacity);
            }
            else
            {
                MiniPlayerTransform.Y = 0;
                MiniPlayerBar.Opacity = 1.0;
            }
        }

        private void MiniPlayerBar_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isDraggingMiniPlayer) return;

            _isDraggingMiniPlayer = false;
            MiniPlayerBar.ReleaseMouseCapture();

            // Dismiss Threshold condition met (dragged down more than 45 pixels)
            if (MiniPlayerTransform.Y > 45)
            {
                DismissMiniPlayerAndStopMusic();
            }
            else
            {
                if (!_hasDraggedSignificantly)
                {
                    OpenFullNowPlayingPanel();
                }
                else
                {
                    SnapMiniPlayerBack();
                }
            }
        }

        private void DismissMiniPlayerAndStopMusic()
        {
            // 1. Stop playback safely through the backend pipeline
            if (_playback.IsPlaying)
            {
                _playback.Pause();
            }

            // 2. Smoothly slide the bar out of the view window frame boundaries
            var easeIn = new CubicEase { EasingMode = EasingMode.EaseIn };
            var dismissFlydown = new DoubleAnimation(MiniPlayerTransform.Y, MiniPlayerTransform.Y + 80, TimeSpan.FromMilliseconds(200)) { EasingFunction = easeIn };
            var dismissFade = new DoubleAnimation(MiniPlayerBar.Opacity, 0, TimeSpan.FromMilliseconds(180)) { EasingFunction = easeIn };

            dismissFlydown.Completed += (s, e) =>
            {
                MiniPlayerBar.Visibility = Visibility.Collapsed;
                
                // Reset defaults so everything behaves cleanly the next time a track plays
                MiniPlayerTransform.X = 0;
                MiniPlayerTransform.Y = 0;
                MiniPlayerBar.Opacity = 1.0;
            };

            MiniPlayerTransform.BeginAnimation(TranslateTransform.YProperty, dismissFlydown);
            MiniPlayerBar.BeginAnimation(UIElement.OpacityProperty, dismissFade);
        }

        private void SnapMiniPlayerBack()
        {
            var easeOut = new CubicEase { EasingMode = EasingMode.EaseOut };
            var snapY = new DoubleAnimation(MiniPlayerTransform.Y, 0, TimeSpan.FromMilliseconds(250)) { EasingFunction = easeOut };
            var snapOpacity = new DoubleAnimation(MiniPlayerBar.Opacity, 1.0, TimeSpan.FromMilliseconds(200)) { EasingFunction = easeOut };

            MiniPlayerTransform.BeginAnimation(TranslateTransform.YProperty, snapY);
            MiniPlayerBar.BeginAnimation(UIElement.OpacityProperty, snapOpacity);
        }

        private void OpenFullNowPlayingPanel()
        {
            if (_nowPlayingView == null)
            {
                _nowPlayingView = new NowPlayingControl(_playback);
                _nowPlayingView.RequestCollapse += CollapseFullNowPlayingPanel;
                NowPlayingPresenter.Content = _nowPlayingView;
            }

            _isFullPanelOpen = true;

            FullNowPlayingContainer.Visibility = Visibility.Visible;
            FullNowPlayingContainer.Opacity = 0;
            FullPanelTransform.Y = this.ActualHeight;

            var easeOut = new CubicEase { EasingMode = EasingMode.EaseOut };

            var panelSlideUp = new DoubleAnimation(this.ActualHeight, 0, TimeSpan.FromMilliseconds(400)) { EasingFunction = easeOut };
            var panelFadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300)) { EasingFunction = easeOut };
            var miniFadeOut = new DoubleAnimation(MiniPlayerBar.Opacity, 0, TimeSpan.FromMilliseconds(200)) { EasingFunction = easeOut };
            var miniSinkDown = new DoubleAnimation(MiniPlayerTransform.Y, MiniPlayerTransform.Y + 40, TimeSpan.FromMilliseconds(350)) { EasingFunction = easeOut };

            miniFadeOut.Completed += (s, e) =>
            {
                if (_isFullPanelOpen) MiniPlayerBar.Visibility = Visibility.Collapsed;
            };

            FullPanelTransform.BeginAnimation(TranslateTransform.YProperty, panelSlideUp);
            FullNowPlayingContainer.BeginAnimation(UIElement.OpacityProperty, panelFadeIn);
            MiniPlayerBar.BeginAnimation(UIElement.OpacityProperty, miniFadeOut);
            MiniPlayerTransform.BeginAnimation(TranslateTransform.YProperty, miniSinkDown);
        }

        private void CollapseFullNowPlayingPanel()
        {
            MiniPlayerBar.Visibility = Visibility.Visible;

            var easeIn = new CubicEase { EasingMode = EasingMode.EaseIn };
            var easeOut = new CubicEase { EasingMode = EasingMode.EaseOut };

            var panelSlideDown = new DoubleAnimation(0, this.ActualHeight, TimeSpan.FromMilliseconds(350)) { EasingFunction = easeIn };
            var panelFadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(250)) { EasingFunction = easeIn };
            var miniFadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300)) { EasingFunction = easeOut };
            var miniRiseUp = new DoubleAnimation(40, 0, TimeSpan.FromMilliseconds(350)) { EasingFunction = easeOut };

            panelSlideDown.Completed += (s, e) =>
            {
                _isFullPanelOpen = false;
                FullNowPlayingContainer.Visibility = Visibility.Collapsed;
                UpdateMiniPlayerVisibility();
            };

            FullPanelTransform.BeginAnimation(TranslateTransform.YProperty, panelSlideDown);
            FullNowPlayingContainer.BeginAnimation(UIElement.OpacityProperty, panelFadeOut);
            MiniPlayerBar.BeginAnimation(UIElement.OpacityProperty, miniFadeIn);
            MiniPlayerTransform.BeginAnimation(TranslateTransform.YProperty, miniRiseUp);
        }

        private void OnPlaybackStateChanged(bool isPlaying)
        {
            Dispatcher.Invoke(() =>
            {
                BtnMiniPlay.Content = isPlaying ? "⏸" : "▶";
            });
        }

        private void MiniPlay_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true; 
            if (_playback.IsPlaying) _playback.Pause(); else _playback.Resume();
        }

        private void MiniPrevious_Click(object sender, RoutedEventArgs e) { e.Handled = true; _playback.Previous(); }
        private void MiniNext_Click(object sender, RoutedEventArgs e) { e.Handled = true; _playback.Next(); }
        #endregion
    }
}

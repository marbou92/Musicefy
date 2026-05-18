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
                
                // FIXED: Force a structural layout layout math refresh pass precisely 
                // when incoming subviews swap out. This tells the ScrollViewer how tall 
                // its screen window actual boundaries are to activate your scrollbars!
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
        private bool _hasDraggedSignificantly = false;
        private bool _isFullPanelOpen = false;

        private void UpdateMiniPlayerVisibility()
        {
            // Only show the bar if a valid audio file track object exists AND the full panel view is tucked away
            if (NowPlaying == null || _isFullPanelOpen)
            {
                MiniPlayerBar.Visibility = Visibility.Collapsed;
            }
            else
            {
                MiniPlayerBar.Visibility = Visibility.Visible;
            }
        }

        private void MiniPlayerBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _isDraggingMiniPlayer = true;
            _dragStartPoint = e.GetPosition(this);
            _dragStartTranslateX = MiniPlayerTransform.X;
            _hasDraggedSignificantly = false;
            
            MiniPlayerBar.CaptureMouse();
        }

        private void MiniPlayerBar_MouseMove(object sender, MouseButtonEventArgs e)
        {
            if (!_isDraggingMiniPlayer) return;

            Point currentPoint = e.GetPosition(this);
            double deltaX = currentPoint.X - _dragStartPoint.X;

            // Prevent hypersensitive click-misfires by enforcing a 5px structural deadzone
            if (!_hasDraggedSignificantly && Math.Abs(deltaX) > 5)
            {
                _hasDraggedSignificantly = true;
            }

            double targetX = _dragStartTranslateX + deltaX;

            // Clamping framework calculations to prevent drifting off the screen margins
            double maxOffset = (this.ActualWidth - MiniPlayerBar.Width) / 2.0 - 16; 
            if (maxOffset < 0) maxOffset = 0;

            if (targetX < -maxOffset) targetX = -maxOffset;
            if (targetX > maxOffset) targetX = maxOffset;

            MiniPlayerTransform.X = targetX;
        }

        private void MiniPlayerBar_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isDraggingMiniPlayer) return;

            _isDraggingMiniPlayer = false;
            MiniPlayerBar.ReleaseMouseCapture();

            // If the user simply tapped without driving the dock left or right, trigger panel slide up action
            if (!_hasDraggedSignificantly)
            {
                OpenFullNowPlayingPanel();
            }
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
            UpdateMiniPlayerVisibility();
            
            FullPanelTransform.Y = this.ActualHeight;
            var slideUpAnim = new DoubleAnimation(this.ActualHeight, 0, TimeSpan.FromMilliseconds(450))
            {
                EasingFunction = new CircleEase { EasingMode = EasingMode.EaseOut }
            };
            FullPanelTransform.BeginAnimation(TranslateTransform.YProperty, slideUpAnim);
        }

        private void CollapseFullNowPlayingPanel()
        {
            var slideDownAnim = new DoubleAnimation(0, this.ActualHeight, TimeSpan.FromMilliseconds(400))
            {
                EasingFunction = new CircleEase { EasingMode = EasingMode.EaseIn }
            };

            slideDownAnim.Completed += (s, e) =>
            {
                _isFullPanelOpen = false;
                FullNowPlayingContainer.Visibility = Visibility.Collapsed;
                UpdateMiniPlayerVisibility();
            };

            FullPanelTransform.BeginAnimation(TranslateTransform.YProperty, slideDownAnim);
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

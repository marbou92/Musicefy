using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Musicefy.Views;
using Musicefy.Services;
using Musicefy.ViewModels;

namespace Musicefy
{
    public partial class MainWindow : Window
    {
        private readonly PlaybackService _playback;
        private readonly MainViewModel _mainViewModel;
        private NowPlayingControl _nowPlayingView;
        private bool _isInitializing = true;

        public MainWindow()
        {
            // Initialize Core Data Layers First
            _mainViewModel = new MainViewModel();
            _playback = new PlaybackService();
            
            this.DataContext = _mainViewModel;
            
            // Render Initial Core Visual Component Layout Trees
            InitializeComponent();

            _isInitializing = false;

            // Seed initial fallback focus index
            SidebarList.SelectedIndex = 0;

            // Hook Core Player Service events to keep the tiny mini-player bar buttons updated automatically
            _playback.PlaybackStateChanged += OnPlaybackStateChanged;
        }

        private void Sidebar_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing || MainContent == null || _mainViewModel == null || _playback == null) 
                return;

            if (SidebarList.SelectedItem == null) return;

            // Modal Settings Hook Intercept Router
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
                // FIXED PARAMS: Instantiated with the dynamic constructor framework pass
                case 2: nextView = new LibraryControl(_playback); break;
            }

            if (nextView != null)
            {
                NavigateWithFade(nextView);
            }
        }

        private void NavigateWithFade(UserControl newContent)
        {
            DoubleAnimation fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(150));
            fadeOut.Completed += (s, e) =>
            {
                MainContent.Content = newContent;
                DoubleAnimation fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200));
                MainContent.BeginAnimation(OpacityProperty, fadeIn);
            };
            MainContent.BeginAnimation(OpacityProperty, fadeOut);
        }

        #region Mini-Player Pipeline Slide Controllers
        private void MiniPlayerBar_Click(object sender, MouseButtonEventArgs e)
        {
            if (_nowPlayingView == null)
            {
                _nowPlayingView = new NowPlayingControl(_playback);
                _nowPlayingView.RequestCollapse += CollapseFullNowPlayingPanel;
                NowPlayingPresenter.Content = _nowPlayingView;
            }

            // Reveal hidden full expanded overlay frame wrapper
            FullNowPlayingContainer.Visibility = Visibility.Visible;
            MiniPlayerBar.Visibility = Visibility.Collapsed;
            
            // Execute premium circular slide ease transitions up the view axis
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
                FullNowPlayingContainer.Visibility = Visibility.Collapsed;
                MiniPlayerBar.Visibility = Visibility.Visible;
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
            e.Handled = true; // Stop event escalation from firing the parent slide container maximize logic
            if (_playback.IsPlaying) _playback.Pause(); else _playback.Resume();
        }

        private void MiniPrevious_Click(object sender, RoutedEventArgs e) { e.Handled = true; _playback.Previous(); }
        private void MiniNext_Click(object sender, RoutedEventArgs e) { e.Handled = true; _playback.Next(); }
        #endregion
    }
}

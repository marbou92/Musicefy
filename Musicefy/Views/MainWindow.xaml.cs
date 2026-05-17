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

        // FIXED: Expose the playback track object directly so XAML can query properties on change warnings
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
            
            // Set the master window DataContext to self so standard properties filter perfectly
            this.DataContext = this;
            
            InitializeComponent();

            _isInitializing = false;
            SidebarList.SelectedIndex = 0;

            // Wire pipeline change tracking triggers up directly
            _playback.TrackChanged += OnTrackChanged;
            _playback.PlaybackStateChanged += OnPlaybackStateChanged;
        }

        // Call this inside your Window class constructor (e.g., public SettingsWindow() { ... })
        private void AttachCustomTitleBarWindowActions()
        {
            this.Loaded += (s, e) =>
            {
                // Resolve buttons from the applied ControlTemplate shell context
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
                // Forces XAML to re-evaluate the data link pathways instantly
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

            FullNowPlayingContainer.Visibility = Visibility.Visible;
            MiniPlayerBar.Visibility = Visibility.Collapsed;
            
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
            e.Handled = true; // Stop event escalation bubbles
            if (_playback.IsPlaying) _playback.Pause(); else _playback.Resume();
        }

        private void MiniPrevious_Click(object sender, RoutedEventArgs e) { e.Handled = true; _playback.Previous(); }
        private void MiniNext_Click(object sender, RoutedEventArgs e) { e.Handled = true; _playback.Next(); }
        #endregion
    }
}

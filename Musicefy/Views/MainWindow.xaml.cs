using System;
using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using Musicefy.Views;
using Musicefy.Services;
using Musicefy.Core.Models;

namespace Musicefy
{
    public partial class MainWindow : Window
    {
        private readonly PlaybackService _playback;
        private bool _isExpanded = false;

        public MainWindow()
        {
            InitializeComponent();
            _playback = new PlaybackService();

            _playback.TrackChanged += OnTrackChanged;
            _playback.ProgressChanged += OnProgressChanged;

            MainContent.Content = new HomeControl(_playback);
        }

        // Navigation
        private void Home_Click(object sender, RoutedEventArgs e) => MainContent.Content = new HomeControl(_playback);
        private void Search_Click(object sender, RoutedEventArgs e) => MainContent.Content = new SearchControl(_playback);
        private void Library_Click(object sender, RoutedEventArgs e) => MainContent.Content = new LibraryControl(_playback);
        private void NowPlaying_Click(object sender, RoutedEventArgs e) => ExpandNowPlaying();

        private void OpenSettings_Click(object sender, RoutedEventArgs e)
        {
            var win = new SettingsWindow { Owner = this };
            win.ShowDialog();
        }

        // Mini Player click → expand/collapse
        private void MiniPlayer_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (_isExpanded)
                CollapseNowPlaying();
            else
                ExpandNowPlaying();
        }

        private void ExpandNowPlaying()
        {
            var nowPlaying = new NowPlayingControl(_playback);
            nowPlaying.RequestCollapse += CollapseNowPlaying; // hook collapse event
            MainContent.Content = nowPlaying;

            var fadeIn = new DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(400)));
            nowPlaying.BeginAnimation(OpacityProperty, fadeIn);

            var slideUp = new ThicknessAnimation
            {
                From = new Thickness(0, 100, 0, -100),
                To = new Thickness(0),
                Duration = new Duration(TimeSpan.FromMilliseconds(400)),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            nowPlaying.BeginAnimation(MarginProperty, slideUp);

            _isExpanded = true;
        }

        private void CollapseNowPlaying()
        {
            var fadeOut = new DoubleAnimation(1, 0, new Duration(TimeSpan.FromMilliseconds(400)));
            MainContent.BeginAnimation(OpacityProperty, fadeOut);

            var slideDown = new ThicknessAnimation
            {
                From = new Thickness(0),
                To = new Thickness(0, 100, 0, -100),
                Duration = new Duration(TimeSpan.FromMilliseconds(400)),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
            };
            MainContent.BeginAnimation(MarginProperty, slideDown);

            fadeOut.Completed += (s, e) =>
            {
                MainContent.Content = new HomeControl(_playback);
                _isExpanded = false;
            };
        }

        // Mini Player updates
        private void OnTrackChanged(MusicFile track)
        {
            MiniTitle.Text = track.Title;
            MiniArtist.Text = track.Artist;

            MiniCover.Source = string.IsNullOrEmpty(track.CoverPath)
                ? new BitmapImage(new Uri("pack://application:,,,/Assets/default_cover.png"))
                : new BitmapImage(new Uri(track.CoverPath, UriKind.RelativeOrAbsolute));

            MiniProgress.Value = 0;
            MiniProgress.Maximum = track.Duration.TotalSeconds;
        }

        private void OnProgressChanged(TimeSpan current, TimeSpan total)
        {
            MiniProgress.Maximum = total.TotalSeconds;
            MiniProgress.Value = current.TotalSeconds;
        }

        // Playback controls
        private void PlayButton_Click(object sender, RoutedEventArgs e) => _playback.Resume();
        private void PauseButton_Click(object sender, RoutedEventArgs e) => _playback.Pause();
        private void NextButton_Click(object sender, RoutedEventArgs e) => _playback.Next();
        private void PreviousButton_Click(object sender, RoutedEventArgs e) => _playback.Previous();

        private void MiniProgress_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (_playback.CurrentAudioFile != null)
                _playback.Seek(TimeSpan.FromSeconds(MiniProgress.Value));
        }
    }
}

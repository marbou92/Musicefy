using System;
using System.Windows;
using System.Windows.Media.Imaging;
using Musicefy.Views;
using Musicefy.Services;
using Musicefy.Core.Models;

namespace Musicefy
{
    public partial class MainWindow : Window
    {
        private readonly PlaybackService _playback;

        public MainWindow()
        {
            InitializeComponent();
            _playback = new PlaybackService();

            // Hook events
            _playback.TrackChanged += OnTrackChanged;
            _playback.ProgressChanged += OnProgressChanged;

            // Default landing page
            MainContent.Content = new HomeControl(_playback);
        }

        // Navigation
        private void Home_Click(object sender, RoutedEventArgs e) => MainContent.Content = new HomeControl(_playback);
        private void Search_Click(object sender, RoutedEventArgs e) => MainContent.Content = new SearchControl(_playback);
        private void Library_Click(object sender, RoutedEventArgs e) => MainContent.Content = new LibraryControl(_playback);
        private void NowPlaying_Click(object sender, RoutedEventArgs e) => MainContent.Content = new NowPlayingControl(_playback);

        private void OpenSettings_Click(object sender, RoutedEventArgs e)
        {
            var win = new SettingsWindow { Owner = this };
            win.ShowDialog();
        }

        // Mini Player updates
        private void OnTrackChanged(MusicFile track)
        {
            MiniTitle.Text = track.Title;
            MiniArtist.Text = track.Artist;

            if (!string.IsNullOrEmpty(track.CoverPath))
                MiniCover.Source = new BitmapImage(new Uri(track.CoverPath, UriKind.RelativeOrAbsolute));
            else
                MiniCover.Source = new BitmapImage(new Uri("pack://application:,,,/Assets/default_cover.png"));

            MiniProgress.Value = 0;
            MiniProgress.Maximum = track.Duration.TotalSeconds;
        }

        private void OnProgressChanged(TimeSpan current, TimeSpan total)
        {
            MiniProgress.Maximum = total.TotalSeconds;
            MiniProgress.Value = current.TotalSeconds;
        }

        // Controls
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

using System.Windows;
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

        private void Home_Click(object sender, RoutedEventArgs e) => MainContent.Content = new HomeControl(_playback);
        private void Search_Click(object sender, RoutedEventArgs e) => MainContent.Content = new SearchControl(_playback);
        private void Library_Click(object sender, RoutedEventArgs e) => MainContent.Content = new LibraryControl(_playback);
        private void NowPlaying_Click(object sender, RoutedEventArgs e) => MainContent.Content = new NowPlayingControl(_playback);

        private void OpenSettings_Click(object sender, RoutedEventArgs e)
        {
            var win = new SettingsWindow { Owner = this };
            win.ShowDialog();
        }

        // Event handlers
        private void OnTrackChanged(MusicFile track)
        {
            // Could update a mini player bar here
        }

        private void OnProgressChanged(System.TimeSpan current, System.TimeSpan total)
        {
            // Could update a global progress bar here
        }
    }
}

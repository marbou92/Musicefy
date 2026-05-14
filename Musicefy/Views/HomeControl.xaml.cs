using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Musicefy.Services;
using Musicefy.ViewModels;
using Musicefy.Core.Models;

namespace Musicefy.Views
{
    public partial class HomeControl : UserControl
    {
        private readonly PlaybackService _playback;
        private readonly MainViewModel _mainViewModel;

        // Constructor now accepts its dependencies
        public HomeControl(PlaybackService playback, MainViewModel mainViewModel)
        {
            InitializeComponent();
            _playback = playback;
            _mainViewModel = mainViewModel;
            this.DataContext = _mainViewModel; // Set ViewModel as the DataContext

            // Bind the ListBoxes in the XAML to the ViewModel collections
            ChartsList.ItemsSource = _mainViewModel.BrowseCharts;
            QuickPicksList.ItemsSource = _mainViewModel.QuickPicks;
            VideosList.ItemsSource = _mainViewModel.TopMusicVideos;
        }

        // Logic for Quick Picks list double click to play the track
        private void QuickPicksList_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (QuickPicksList.SelectedItem is TrackCard selectedTrackCard)
            {
                // Convert the TrackCard view model from ViewModel.cs back into a dummy MusicFile core model
                // for playing, as full conversion logic isn't fully implemented yet.
                var trackCoreModel = new MusicFile(selectedTrackCard.Title, selectedTrackCard.Artist, EnsureAlbum(""), 0, genre: EnsureGenre(""), duration: TimeSpan.Zero);
                trackCoreModel.MarkPlayed();
                _playback.PlayTrack(trackCoreModel);
            }
        }

        // Action for "Play all" button for quick picks
        private void PlayAllQuickPicks_Click(object sender, RoutedEventArgs e)
        {
            if (_mainViewModel.QuickPicks == null || !_mainViewModel.QuickPicks.Any()) return;

            foreach (var trackCard in _mainViewModel.QuickPicks)
            {
                // Create dummy MusicFile core models and add them to the queue
                var trackCoreModel = new MusicFile(trackCard.Title, trackCard.Artist, EnsureAlbum(""), 0, genre: EnsureGenre(""), duration: TimeSpan.Zero);
                _playback.EnqueueTrack(trackCoreModel);
            }
            
            // Construct a temporary dummy MusicFile model for the first track in the queue to start playback.
            var firstTrackCard = _mainViewModel.QuickPicks.FirstOrDefault();
            if (firstTrackCard != null)
            {
                var firstTrackCoreModel = new MusicFile(firstTrackCard.Title, firstTrackCard.Artist, EnsureAlbum(""), 0, genre: EnsureGenre(""), duration: TimeSpan.Zero);
                _playback.PlayTrack(firstTrackCoreModel);
            }
        }

        // Action for "More" button for music videos. Navigation logic placeholder.
        private void MoreVideos_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Navigate to dedicated videos page (not implemented)");
        }

        // Helper methods re-used from previous HomeControl.xaml.cs logic
        private string EnsureAlbum(string album) { return string.IsNullOrWhiteSpace(album) ? "Unknown Album" : album; }
        private string EnsureGenre(string genre) { return string.IsNullOrWhiteSpace(genre) ? "Unknown Genre" : genre; }
    }
}

using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Musicefy.Services;
using Musicefy.ViewModels;
using Musicefy.Core.Models;

namespace Musicefy.Views
{
    public partial class HomeControl : UserControl
    {
        private readonly PlaybackService _playback;
        private readonly MainViewModel _mainViewModel;

        public HomeControl(PlaybackService playback, MainViewModel mainViewModel)
        {
            InitializeComponent();
            _playback = playback;
            _mainViewModel = mainViewModel;
            this.DataContext = _mainViewModel;

            // Bindings are usually handled in XAML, but explicit assignment ensures immediate sync
            ChartsList.ItemsSource = _mainViewModel.BrowseCharts;
            QuickPicksList.ItemsSource = _mainViewModel.QuickPicks;
            VideosList.ItemsSource = _mainViewModel.TopMusicVideos;
        }

        private void QuickPicksList_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (QuickPicksList.SelectedItem is TrackCard selectedTrack)
            {
                var track = MapToMusicFile(selectedTrack);
                track.MarkPlayed();
                _playback.PlayTrack(track);
            }
        }

        private void PlayAllQuickPicks_Click(object sender, RoutedEventArgs e)
        {
            if (_mainViewModel.QuickPicks == null || !_mainViewModel.QuickPicks.Any()) return;

            foreach (var trackCard in _mainViewModel.QuickPicks)
            {
                _playback.EnqueueTrack(MapToMusicFile(trackCard));
            }
            
            var first = _mainViewModel.QuickPicks.FirstOrDefault();
            if (first != null)
            {
                _playback.PlayTrack(MapToMusicFile(first));
            }
        }

        private void MoreVideos_Click(object sender, RoutedEventArgs e)
        {
            // Future: Use a NavigationService instead of MessageBox
            MessageBox.Show("Navigation to Videos is coming soon!", "Musicefy", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // Centralized mapping logic to avoid repetition
        private MusicFile MapToMusicFile(TrackCard card)
        {
            return new MusicFile(
                string.IsNullOrWhiteSpace(card.Title) ? "Unknown Track" : card.Title,
                string.IsNullOrWhiteSpace(card.Artist) ? "Unknown Artist" : card.Artist,
                "Unknown Album", 
                0, 
                genre: "Unknown Genre", 
                duration: TimeSpan.Zero
            );
        }
    }
}

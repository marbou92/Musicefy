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
            _playback = playback ?? throw new ArgumentNullException(nameof(playback));
            _mainViewModel = mainViewModel ?? throw new ArgumentNullException(nameof(mainViewModel));
            
            this.DataContext = _mainViewModel;

            // Fluid UI local assignment handles data stream synchronization safely
            if (ChartsList != null) ChartsList.ItemsSource = _mainViewModel.BrowseCharts;
            if (QuickPicksList != null) QuickPicksList.ItemsSource = _mainViewModel.QuickPicks;
            if (VideosList != null) VideosList.ItemsSource = _mainViewModel.TopMusicVideos;
        }

        private void QuickPicksList_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (QuickPicksList.SelectedItem is TrackCard selectedTrack)
            {
                var track = MapToMusicFile(selectedTrack);
                _playback.PlayTrack(track);
            }
        }

        private void PlayAllQuickPicks_Click(object sender, RoutedEventArgs e)
        {
            if (_mainViewModel.QuickPicks == null || !_mainViewModel.QuickPicks.Any()) return;

            // Enqueue track data collection safely down to core streaming layers
            foreach (var trackCard in _mainViewModel.QuickPicks)
            {
                _playback.EnqueueTrack(MapToMusicFile(trackCard));
            }
            
            // Instantly resolve and ignite first record sequence
            var first = _mainViewModel.QuickPicks.FirstOrDefault();
            if (first != null)
            {
                _playback.PlayTrack(MapToMusicFile(first));
            }
        }

        private void MoreVideos_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Video engine integration coming in the next core compilation step!", "Musicefy", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        /// <summary>
        /// Maps UI view metadata cards cleanly to backend domain storage entities 
        /// </summary>
        private MusicFile MapToMusicFile(TrackCard card)
        {
            return new MusicFile(
                title: string.IsNullOrWhiteSpace(card.Title) ? "Unknown Track" : card.Title,
                artist: string.IsNullOrWhiteSpace(card.Artist) ? "Unknown Artist" : card.Artist,
                album: "Musicefy Discovery", 
                year: DateTime.Now.Year, 
                genre: "Discovery", 
                duration: TimeSpan.Zero // Populated contextually by playback media streams
            );
        }
    }
}

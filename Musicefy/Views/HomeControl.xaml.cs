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
        private readonly MainViewModel _viewModel;

        public HomeControl(PlaybackService playback, MainViewModel mainViewModel)
        {
            InitializeComponent();
            _playback = playback ?? throw new ArgumentNullException(nameof(playback));
            _viewModel = mainViewModel ?? throw new ArgumentNullException(nameof(mainViewModel));

            this.DataContext = _viewModel;

            if (ChartsList != null) ChartsList.ItemsSource = _viewModel.BrowseCharts;
            if (QuickPicksList != null) QuickPicksList.ItemsSource = _viewModel.QuickPicks;
            if (VideosList != null) VideosList.ItemsSource = _viewModel.TopMusicVideos;
        }

        private void QuickPicksList_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (QuickPicksList.SelectedItem is TrackCard selectedTrack)
                _playback.PlayTrack(MapToMusicFile(selectedTrack));
        }

        private void PlayAllQuickPicks_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel.QuickPicks == null || !_viewModel.QuickPicks.Any()) return;
            foreach (var trackCard in _viewModel.QuickPicks)
                _playback.EnqueueTrack(MapToMusicFile(trackCard));

            var first = _viewModel.QuickPicks.FirstOrDefault();
            if (first != null)
                _playback.PlayTrack(MapToMusicFile(first));
        }

        private void MoreVideos_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Video engine coming soon!", "Musicefy",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private MusicFile MapToMusicFile(TrackCard card)
        {
            return new MusicFile
            {
                Id = Guid.NewGuid().ToString(),
                Title = string.IsNullOrWhiteSpace(card.Title) ? "Unknown Track" : card.Title,
                Artist = string.IsNullOrWhiteSpace(card.Artist) ? "Unknown Artist" : card.Artist,
                Album = "Musicefy Discovery",
                Year = DateTime.Now.Year,
                Genre = "Discovery",
                Duration = TimeSpan.Zero,
                SourceType = "Discovery"
            };
        }
    }
}

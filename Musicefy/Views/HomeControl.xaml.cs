using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Musicefy.Services;
using Musicefy.ViewModels;

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
            if (QuickPicksList.SelectedItem is TrackCard selected && selected.SourceTrack != null)
                _playback.PlayTrack(selected.SourceTrack);
        }

        private void PlayAllQuickPicks_Click(object sender, RoutedEventArgs e)
        {
            var tracks = _viewModel.QuickPicks
                .Where(c => c.SourceTrack != null)
                .Select(c => c.SourceTrack)
                .ToList();

            if (tracks.Count == 0) return;

            foreach (var track in tracks)
                _playback.EnqueueTrack(track);

            _playback.PlayTrack(tracks[0]);
        }

        private void MoreVideos_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Video engine coming soon!", "Musicefy",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}

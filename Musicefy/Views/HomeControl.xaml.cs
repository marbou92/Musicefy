using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using Musicefy.Services;
using Musicefy.ViewModels;

namespace Musicefy.Views
{
    public partial class HomeControl : UserControl
    {
        private readonly PlaybackService _playback;
        private readonly MainViewModel _viewModel;
        private Storyboard _shimmerStoryboard;

        public HomeControl(PlaybackService playback, MainViewModel mainViewModel)
        {
            InitializeComponent();
            _playback = playback ?? throw new ArgumentNullException(nameof(playback));
            _viewModel = mainViewModel ?? throw new ArgumentNullException(nameof(mainViewModel));

            DataContext = _viewModel;

            _viewModel.PropertyChanged += OnViewModelPropertyChanged;

            Loaded += async (s, e) =>
            {
                await _viewModel.ReloadAsync();
                UpdateViewState();
            };
        }

        private void OnViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainViewModel.IsLoading) ||
                e.PropertyName == nameof(MainViewModel.IsEmpty))
            {
                UpdateViewState();
            }
        }

        private void UpdateViewState()
        {
            if (_viewModel.IsLoading)
            {
                LoadingSkeleton.Visibility = Visibility.Visible;
                ContentArea.Visibility = Visibility.Collapsed;
                EmptyState.Visibility = Visibility.Collapsed;
                StartShimmer();
            }
            else if (_viewModel.IsEmpty)
            {
                EmptyState.Visibility = Visibility.Visible;
                ContentArea.Visibility = Visibility.Collapsed;
                LoadingSkeleton.Visibility = Visibility.Collapsed;
                StopShimmer();
            }
            else
            {
                ContentArea.Visibility = Visibility.Visible;
                EmptyState.Visibility = Visibility.Collapsed;
                LoadingSkeleton.Visibility = Visibility.Collapsed;
                StopShimmer();
            }
        }

        private void StartShimmer()
        {
            if (_shimmerStoryboard != null) return;
            _shimmerStoryboard = new Storyboard();
            var pulse = new DoubleAnimation
            {
                From = 0.2,
                To = 0.6,
                Duration = new Duration(TimeSpan.FromSeconds(1.4)),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever
            };
            Storyboard.SetTargetProperty(pulse, new PropertyPath("Opacity"));
            _shimmerStoryboard.Children.Add(pulse);
            _shimmerStoryboard.Begin(LoadingSkeleton, true);
        }

        private void StopShimmer()
        {
            if (_shimmerStoryboard == null) return;
            _shimmerStoryboard.Stop(LoadingSkeleton);
            _shimmerStoryboard = null;
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

        private void OpenSources_Click(object sender, RoutedEventArgs e)
        {
            var settings = new SettingsWindow { Owner = Window.GetWindow(this) };
            settings.ShowDialog();
            _ = _viewModel.ReloadAsync();
        }
    }
}

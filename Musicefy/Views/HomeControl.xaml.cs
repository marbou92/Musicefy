using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
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
            Unloaded += OnUnloaded;

            Loaded += async (s, e) =>
            {
                try
                {
                    await _viewModel.ReloadAsync();
                    UpdateViewState();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[HomeControl] Loaded handler failed: {ex}");
                }
            };
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            StopShimmer();
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

            var brush = new LinearGradientBrush
            {
                StartPoint = new Point(-1, 0),
                EndPoint = new Point(0, 0)
            };
            brush.GradientStops.Add(new GradientStop(Color.FromArgb(0x1A, 0xFF, 0xFF, 0xFF), 0.0));
            brush.GradientStops.Add(new GradientStop(Color.FromArgb(0x4D, 0xFF, 0xFF, 0xFF), 0.5));
            brush.GradientStops.Add(new GradientStop(Color.FromArgb(0x1A, 0xFF, 0xFF, 0xFF), 1.0));

            ShimmerOverlay.Fill = brush;

            _shimmerStoryboard = new Storyboard { RepeatBehavior = RepeatBehavior.Forever };

            var startPointAnim = new PointAnimation
            {
                From = new Point(-1, 0),
                To = new Point(1, 0),
                Duration = TimeSpan.FromSeconds(2),
                AutoReverse = true
            };
            Storyboard.SetTarget(startPointAnim, brush);
            Storyboard.SetTargetProperty(startPointAnim, new PropertyPath(LinearGradientBrush.StartPointProperty));

            var endPointAnim = new PointAnimation
            {
                From = new Point(0, 0),
                To = new Point(2, 0),
                Duration = TimeSpan.FromSeconds(2),
                AutoReverse = true
            };
            Storyboard.SetTarget(endPointAnim, brush);
            Storyboard.SetTargetProperty(endPointAnim, new PropertyPath(LinearGradientBrush.EndPointProperty));

            _shimmerStoryboard.Children.Add(startPointAnim);
            _shimmerStoryboard.Children.Add(endPointAnim);
            _shimmerStoryboard.Begin(ShimmerOverlay, true);
        }

        private void StopShimmer()
        {
            if (_shimmerStoryboard == null) return;
            _shimmerStoryboard.Stop(ShimmerOverlay);
            _shimmerStoryboard = null;
            ShimmerOverlay.Fill = null;
        }

        private void QuickPicksList_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (QuickPicksList.SelectedItem is TrackCard selected && selected.SourceTrack != null)
                _playback.PlayTrack(selected.SourceTrack);
        }

        private void PlayAllQuickPicks_Click(object sender, RoutedEventArgs e)
        {
            var tracks = _viewModel.FilteredQuickPicks
                .Where(c => c.SourceTrack != null)
                .Select(c => c.SourceTrack)
                .ToList();

            if (tracks.Count == 0)
            {
                tracks = _viewModel.QuickPicks
                    .Where(c => c.SourceTrack != null)
                    .Select(c => c.SourceTrack)
                    .ToList();
            }

            if (tracks.Count == 0) return;

            foreach (var track in tracks)
                _playback.EnqueueTrack(track);

            _playback.PlayTrack(tracks[0]);
        }

        private void RecentlyPlayed_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2 && sender is Border border && border.DataContext is TrackCard selected && selected.SourceTrack != null)
                _playback.PlayTrack(selected.SourceTrack);
        }

        private void HeroCard_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2 && _viewModel.HeroTrack?.SourceTrack != null)
                _playback.PlayTrack(_viewModel.HeroTrack.SourceTrack);
        }

        private void OpenSources_Click(object sender, RoutedEventArgs e)
        {
            if (Window.GetWindow(this) is MainWindow mainWindow)
                mainWindow.NavigateToSettings();
            _ = _viewModel.ReloadAsync();
        }
    }
}

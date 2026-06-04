using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Musicefy.Core.Models;
using Musicefy.Core.Services;
using Musicefy.Services;
using Musicefy.ViewModels;

namespace Musicefy.Views
{
    public partial class AlbumView : UserControl
    {
        private readonly AlbumViewModel _viewModel;

        public AlbumView(AlbumViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = _viewModel;
            _viewModel.RequestNavigateToArtist += OnRequestNavigateToArtist;
        }

        /// <summary>
        /// Load album by name (legacy path — local/subsonic sources).
        /// </summary>
        public async void LoadAlbum(string albumName, string artistName = null)
        {
            await _viewModel.LoadAsync(albumName, artistName);
            ExtractColorsFromCover();
        }

        /// <summary>
        /// Load album from a full <see cref="AlbumInfo"/> object.
        /// Preserves YouTube browse IDs for rich YouTube Music integration.
        /// Inspired by Echo Music's two-step album fetch (list → detail).
        /// </summary>
        public async void LoadAlbum(AlbumInfo albumInfo)
        {
            if (albumInfo == null) return;
            await _viewModel.LoadAsync(albumInfo);
            ExtractColorsFromCover();
        }

        private void ExtractColorsFromCover()
        {
            try
            {
                string coverPath = _viewModel.CoverPath;
                if (string.IsNullOrEmpty(coverPath) || coverPath.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                    return;

                if (!System.IO.File.Exists(coverPath))
                    return;

                var cover = new BitmapImage(new Uri(coverPath, UriKind.Absolute));
                var colors = ColorExtractor.Extract(cover);

                // Use the current theme's surface color for the gradient endpoint
                // so the gradient adapts to light/dark mode instead of always
                // ending in near-black.
                var surfaceColor = ThemeManager.GetCurrentSurfaceColor();

                var gradient = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0),
                    EndPoint = new Point(0, 1)
                };
                gradient.GradientStops.Add(new GradientStop(colors.Primary, 0.0));
                gradient.GradientStops.Add(new GradientStop(colors.Surface, 0.5));
                gradient.GradientStops.Add(new GradientStop(surfaceColor, 1.0));
                gradient.Freeze();
                _viewModel.BackgroundGradient = gradient;
            }
            catch
            {
                // Color extraction is non-critical
            }
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            if (Window.GetWindow(this) is MainWindow mainWindow)
                mainWindow.NavigateBack();
        }

        private void ArtistText_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // Phase 1: Navigate to artist using the full ArtistInfo object,
            // preserving YouTube channel ID if available.
            NavigateToArtistFromAlbum();
        }

        /// <summary>
        /// Navigate to the artist page from within the album view.
        /// Uses the AlbumViewModel's ArtistYouTubeId when available
        /// for seamless YouTube Music artist browsing.
        /// </summary>
        private void NavigateToArtistFromAlbum()
        {
            if (Window.GetWindow(this) is MainWindow mainWindow)
            {
                var artistInfo = new ArtistInfo
                {
                    Name = _viewModel.ArtistName,
                    SourceType = _viewModel.IsYouTubeAlbum ? SourceTypes.YouTube : null
                };

                // Carry the YouTube channel ID if available
                if (!string.IsNullOrEmpty(_viewModel.ArtistYouTubeId))
                {
                    artistInfo.Id = _viewModel.ArtistYouTubeId;
                    artistInfo.YouTubeChannelId = _viewModel.ArtistYouTubeId;
                }

                mainWindow.NavigateToArtist(artistInfo);
            }
        }

        private void OnRequestNavigateToArtist(ArtistInfo artistInfo)
        {
            if (Window.GetWindow(this) is MainWindow mainWindow)
                mainWindow.NavigateToArtist(artistInfo);
        }
    }
}

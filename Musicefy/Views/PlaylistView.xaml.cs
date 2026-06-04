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
    /// <summary>
    /// Interaction logic for PlaylistView.xaml
    /// Phase 5: Playlists & Collection Management.
    /// Inspired by Echo Music's playlist detail screen.
    /// </summary>
    public partial class PlaylistView : UserControl
    {
        private readonly PlaylistViewModel _viewModel;

        public PlaylistView(PlaylistViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = _viewModel;
            _viewModel.RequestGoBack += OnRequestGoBack;
            _viewModel.PlaylistDeleted += OnPlaylistDeleted;
        }

        /// <summary>
        /// Load a playlist by its database ID.
        /// </summary>
        public async void LoadPlaylist(string playlistId)
        {
            await _viewModel.LoadAsync(playlistId);
            ExtractColorsFromCover();
        }

        /// <summary>
        /// Load from an existing PlaylistInfo object.
        /// </summary>
        public async void LoadPlaylist(PlaylistInfo playlistInfo)
        {
            if (playlistInfo == null) return;
            await _viewModel.LoadAsync(playlistInfo);
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

        private void OnRequestGoBack()
        {
            if (Window.GetWindow(this) is MainWindow mainWindow)
                mainWindow.NavigateBack();
        }

        private void OnPlaylistDeleted()
        {
            // Playlist was deleted; navigate back to library
            if (Window.GetWindow(this) is MainWindow mainWindow)
                mainWindow.NavigateBack();
        }
    }
}

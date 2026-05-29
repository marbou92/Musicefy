using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Musicefy.Core.Services;
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

        public async void LoadAlbum(string albumName, string artistName = null)
        {
            await _viewModel.LoadAsync(albumName, artistName);
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

                var gradient = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0),
                    EndPoint = new Point(0, 1)
                };
                gradient.GradientStops.Add(new GradientStop(colors.Primary, 0.0));
                gradient.GradientStops.Add(new GradientStop(colors.Surface, 0.5));
                gradient.GradientStops.Add(new GradientStop(Color.FromRgb(18, 18, 18), 1.0));
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
            if (!string.IsNullOrEmpty(_viewModel.ArtistName))
                OnRequestNavigateToArtist(_viewModel.ArtistName);
        }

        private void OnRequestNavigateToArtist(string artistName)
        {
            if (Window.GetWindow(this) is MainWindow mainWindow)
                mainWindow.NavigateToArtist(artistName);
        }
    }
}

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Musicefy.Core.Models;
using Musicefy.Core.Services;
using Musicefy.ViewModels;

namespace Musicefy.Views
{
    public partial class ArtistView : UserControl
    {
        private readonly ArtistViewModel _viewModel;

        public ArtistView(ArtistViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = _viewModel;
            _viewModel.RequestNavigateToAlbum += OnRequestNavigateToAlbum;
        }

        public async void LoadArtist(string artistName)
        {
            await _viewModel.LoadAsync(artistName);
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

        private void OnRequestNavigateToAlbum(AlbumInfo album)
        {
            if (Window.GetWindow(this) is MainWindow mainWindow)
                mainWindow.NavigateToAlbum(album.Name, album.Artist);
        }
    }
}

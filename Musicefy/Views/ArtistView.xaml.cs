using System;
using System.Windows;
using System.Windows.Controls;
using Musicefy.Core.Models;
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

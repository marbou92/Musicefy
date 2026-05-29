using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Musicefy.ViewModels;

namespace Musicefy.Views
{
    public partial class AlbumView : UserControl
    {
        private readonly AlbumViewModel _viewModel;

        public event Action<string> RequestNavigateToArtist;

        public AlbumView(AlbumViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = _viewModel;
        }

        public async void LoadAlbum(string albumName, string artistName = null)
        {
            await _viewModel.LoadAsync(albumName, artistName);
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            if (Window.GetWindow(this) is MainWindow mainWindow)
                mainWindow.NavigateBack();
        }

        private void ArtistText_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!string.IsNullOrEmpty(_viewModel.ArtistName))
            {
                RequestNavigateToArtist?.Invoke(_viewModel.ArtistName);
                if (Window.GetWindow(this) is MainWindow mainWindow)
                    mainWindow.NavigateToArtist(_viewModel.ArtistName);
            }
        }
    }
}

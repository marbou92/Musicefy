using System;
using System.Windows;
using System.Windows.Controls;
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
    }
}

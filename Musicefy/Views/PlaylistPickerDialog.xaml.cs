using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows;
using Musicefy.Core.Interfaces;
using Musicefy.Core.Models;

namespace Musicefy.Views
{
    public partial class PlaylistPickerDialog : Window
    {
        private readonly ILibraryService _libraryService;

        public PlaylistInfo SelectedPlaylist { get; private set; }

        public PlaylistPickerDialog(ILibraryService libraryService)
        {
            _libraryService = libraryService ?? throw new ArgumentNullException(nameof(libraryService));
            InitializeComponent();
            LoadPlaylists();
        }

        private async void LoadPlaylists()
        {
            try
            {
                var playlists = await _libraryService.GetAllPlaylistsAsync(CancellationToken.None);
                PlaylistList.ItemsSource = playlists;
                if (playlists.Count > 0)
                    PlaylistList.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PlaylistPickerDialog] LoadPlaylists failed: {ex.Message}");
            }
        }

        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            if (PlaylistList.SelectedItem is PlaylistInfo playlist)
            {
                SelectedPlaylist = playlist;
                DialogResult = true;
            }
            else
            {
                MessageBox.Show("Please select a playlist.", "No Playlist Selected",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}

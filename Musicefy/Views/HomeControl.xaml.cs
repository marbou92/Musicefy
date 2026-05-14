using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Musicefy.Core.Models;

namespace Musicefy.Views
{
    public partial class HomeControl : UserControl
    {
        public ObservableCollection<AlbumItem> Albums { get; set; }
        public ObservableCollection<MusicFile> AlbumTracks { get; set; }

        private Random _rng = new Random();

        public HomeControl()
        {
            InitializeComponent();

            // Example: pull albums from your library
            var allTracks = MusicefyApp.Library; // assume you expose your library globally
            var randomAlbums = allTracks
                .GroupBy(t => t.Album)
                .OrderBy(x => _rng.Next())
                .Take(6)
                .Select(g => new AlbumItem
                {
                    Album = g.Key,
                    Artist = g.First().Artist,
                    Cover = string.IsNullOrEmpty(g.First().CoverPath)
                        ? "pack://application:,,,/Assets/default_cover.png"
                        : g.First().CoverPath,
                    Tracks = g.ToList()
                });

            Albums = new ObservableCollection<AlbumItem>(randomAlbums);
            AlbumTracks = new ObservableCollection<MusicFile>();

            AlbumsList.ItemsSource = Albums;
            AlbumTracksList.ItemsSource = AlbumTracks;
        }

        private void AlbumsList_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (AlbumsList.SelectedItem is AlbumItem album)
            {
                AlbumTracks.Clear();
                foreach (var track in album.Tracks)
                    AlbumTracks.Add(track);

                AlbumTracksEmpty.Visibility = AlbumTracks.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            }
        }
    }

    public class AlbumItem
    {
        public string Album { get; set; }
        public string Artist { get; set; }
        public string Cover { get; set; }
        public System.Collections.Generic.List<MusicFile> Tracks { get; set; }
    }
}

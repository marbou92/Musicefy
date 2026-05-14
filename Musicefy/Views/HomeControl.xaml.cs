using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Controls;
using Musicefy.Core.Models;

namespace Musicefy.Views
{
    public partial class HomeControl : UserControl
    {
        public ObservableCollection<AlbumItem> Albums { get; set; }
        public ObservableCollection<string> QuickPicks { get; set; }
        public ObservableCollection<string> TopVideos { get; set; }

        private Random _rng = new Random();

        public HomeControl()
        {
            InitializeComponent();

            // Example: pull albums from your library (replace with PlaylistManager or SourceManager)
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
                        : g.First().CoverPath
                });

            Albums = new ObservableCollection<AlbumItem>(randomAlbums);

            QuickPicks = new ObservableCollection<string>
            {
                "Sahiba", "Ishqa Ve", "Nee Singam Dhan", "Pal Pal (with Talwinder)"
            };

            TopVideos = new ObservableCollection<string>
            {
                "Shararat", "Teri Yaadon Ki Chadar", "RANU BOMBAI KI RANU"
            };

            AlbumsList.ItemsSource = Albums;
            QuickPicksList.ItemsSource = QuickPicks;
            TopVideosList.ItemsSource = TopVideos;
        }
    }

    public class AlbumItem
    {
        public string Album { get; set; }
        public string Artist { get; set; }
        public string Cover { get; set; }
    }
}

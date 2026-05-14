using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Musicefy.Core.Models;
using Musicefy.Services;

namespace Musicefy.Views
{
    public partial class HomeControl : UserControl
    {
        public ObservableCollection<AlbumItem> Albums { get; set; }
        public ObservableCollection<MusicFile> AlbumTracks { get; set; }

        private readonly PlaybackService _playback;
        private AlbumItem _selectedAlbum;
        private Random _rng = new Random();

        public HomeControl(PlaybackService playback)
        {
            InitializeComponent();
            _playback = playback;

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

        private void AlbumsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (AlbumsList.SelectedItem is AlbumItem album)
            {
                _selectedAlbum = album;
                AlbumTitle.Text = $"{album.Album} — {album.Artist}";
                AlbumTracks.Clear();
                foreach (var track in album.Tracks)
                    AlbumTracks.Add(track);

                AlbumTracksEmpty.Visibility = AlbumTracks.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void AlbumTracksList_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (AlbumTracksList.SelectedItem is MusicFile track)
            {
                track.MarkPlayed();
                _playback.PlayTrack(track);
            }
        }

        private void PlayAlbum_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedAlbum == null) return;
            foreach (var track in _selectedAlbum.Tracks)
                _playback.EnqueueTrack(track);
            _playback.PlayTrack(_selectedAlbum.Tracks.FirstOrDefault());
        }

        private void ShuffleAlbum_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedAlbum == null) return;
            var shuffled = _selectedAlbum.Tracks.OrderBy(t => _rng.Next()).ToList();
            foreach (var track in shuffled)
                _playback.EnqueueTrack(track);
            _playback.PlayTrack(shuffled.FirstOrDefault());
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

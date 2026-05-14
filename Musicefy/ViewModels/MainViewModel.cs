using System;
using System.Collections.ObjectModel;
using Musicefy.Core.Models;

namespace Musicefy.ViewModels
{
    public class MainViewModel
    {
        // Collections of full MusicFile objects
        public ObservableCollection<MusicFile> Favourites { get; set; }
        public ObservableCollection<MusicFile> Downloads { get; set; }
        public ObservableCollection<MusicFile> History { get; set; }

        // Now Playing track
        public MusicFile NowPlaying { get; set; }

        public MainViewModel()
        {
            // Example data with all placeholders
            Favourites = new ObservableCollection<MusicFile>
            {
                new MusicFile("Untitled Track", EnsureArtist(""), EnsureAlbum(""), 0, genre: EnsureGenre(""), duration: TimeSpan.Zero),
                new MusicFile("Untitled Track", EnsureArtist(""), EnsureAlbum(""), 0, genre: EnsureGenre(""), duration: TimeSpan.Zero)
            };

            Downloads = new ObservableCollection<MusicFile>
            {
                new MusicFile("Untitled Track", EnsureArtist(""), EnsureAlbum(""), 0, genre: EnsureGenre(""), duration: TimeSpan.Zero),
                new MusicFile("Untitled Track", EnsureArtist(""), EnsureAlbum(""), 0, genre: EnsureGenre(""), duration: TimeSpan.Zero)
            };

            History = new ObservableCollection<MusicFile>
            {
                new MusicFile("Untitled Track", EnsureArtist(""), EnsureAlbum("")),
                new MusicFile("Untitled Track", EnsureArtist(""), EnsureAlbum("")),
                new MusicFile("Untitled Track", EnsureArtist(""), EnsureAlbum("")),
                new MusicFile("Untitled Track", EnsureArtist(""), EnsureAlbum(""))
            };

            // Set Now Playing
            NowPlaying = new MusicFile("Untitled Track", EnsureArtist(""), EnsureAlbum(""), 0, genre: EnsureGenre(""), duration: TimeSpan.Zero);
            NowPlaying.MarkPlayed(); // increment play count
        }

        /// <summary>
        /// Ensures that the artist string is never null or empty.
        /// Returns "Unknown" if missing.
        /// </summary>
        private string EnsureArtist(string artist)
        {
            return string.IsNullOrWhiteSpace(artist) ? "Unknown" : artist;
        }

        /// <summary>
        /// Ensures that the album string is never null or empty.
        /// Returns "Unknown Album" if missing.
        /// </summary>
        private string EnsureAlbum(string album)
        {
            return string.IsNullOrWhiteSpace(album) ? "Unknown Album" : album;
        }

        /// <summary>
        /// Ensures that the genre string is never null or empty.
        /// Returns "Unknown Genre" if missing.
        /// </summary>
        private string EnsureGenre(string genre)
        {
            return string.IsNullOrWhiteSpace(genre) ? "Unknown Genre" : genre;
        }
    }
}

using System.Collections.Generic;
using Musicefy.Core.Models;

namespace Musicefy.Core.Services
{
    public class PlaylistManager
    {
        private readonly List<MusicFile> _tracks = new List<MusicFile>();

        public PlaylistManager()
        {
            // lightweight constructor
        }

        public IEnumerable<MusicFile> GetSampleTracks()
        {
            return new[]
            {
                new MusicFile("Song Title 1", "Artist 1", "Album A", 2020, @"C:\Music\song1.mp3"),
                new MusicFile("Song Title 2", "Artist 2", "Album B", 2019, @"C:\Music\song2.mp3"),
                new MusicFile("Song Title 3", "Artist 3", "Album C", 2021, @"C:\Music\song3.mp3")
            };
        }

        public void Add(MusicFile t) => _tracks.Add(t);
        public bool Remove(MusicFile t) => _tracks.Remove(t);
        public IEnumerable<MusicFile> All() => _tracks.AsReadOnly();
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Musicefy.Core.Models;

namespace Musicefy.Core.Services
{
    public class PlaylistManager
    {
        private List<MusicFile> _tracks = new List<MusicFile>();
        private int _currentIndex = -1;

        public bool ShuffleEnabled { get; set; } = false;
        public bool RepeatEnabled { get; set; } = false;

        public PlaylistManager()
        {
            // lightweight constructor
        }

        /// <summary>
        /// Load tracks from a local folder into the playlist
        /// </summary>
        public void LoadFromLocalFolder(string folderPath)
        {
            if (!Directory.Exists(folderPath))
                throw new DirectoryNotFoundException("Local folder not found.");

            var files = Directory.EnumerateFiles(folderPath, "*.*", SearchOption.AllDirectories)
                                 .Where(f => f.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase) ||
                                             f.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) ||
                                             f.EndsWith(".flac", StringComparison.OrdinalIgnoreCase));

            foreach (var file in files)
            {
                var track = new MusicFile
                {
                    Id = Guid.NewGuid().ToString(),
                    Title = Path.GetFileNameWithoutExtension(file),
                    Artist = "Local",
                    Album = "",
                    Year = DateTime.Now.Year,
                    Path = file,
                    SourceType = "Local",
                    SourceUri = file
                };
                _tracks.Add(track);
            }
        }

        /// <summary>
        /// Add a track manually
        /// </summary>
        public void Add(MusicFile t) => _tracks.Add(t);

        /// <summary>
        /// Remove a track
        /// </summary>
        public bool Remove(MusicFile t) => _tracks.Remove(t);

        /// <summary>
        /// Get all tracks
        /// </summary>
        public IEnumerable<MusicFile> All() => _tracks.AsReadOnly();

        /// <summary>
        /// Get sample demo tracks
        /// </summary>
        public IEnumerable<MusicFile> GetSampleTracks()
        {
            return new[]
            {
                new MusicFile("Song Title 1", "Artist 1", "Album A", 2020, @"C:\Music\song1.mp3"),
                new MusicFile("Song Title 2", "Artist 2", "Album B", 2019, @"C:\Music\song2.mp3"),
                new MusicFile("Song Title 3", "Artist 3", "Album C", 2021, @"C:\Music\song3.mp3")
            };
        }

        /// <summary>
        /// Shuffle playlist
        /// </summary>
        public void Shuffle()
        {
            var rnd = new Random();
            _tracks = _tracks.OrderBy(x => rnd.Next()).ToList();
            _currentIndex = -1;
            ShuffleEnabled = true;
        }

        /// <summary>
        /// Get next track
        /// </summary>
        public MusicFile Next()
        {
            if (_tracks.Count == 0) return null;

            if (ShuffleEnabled)
            {
                var rnd = new Random();
                _currentIndex = rnd.Next(_tracks.Count);
            }
            else
            {
                _currentIndex++;
                if (_currentIndex >= _tracks.Count)
                {
                    if (RepeatEnabled)
                        _currentIndex = 0;
                    else
                        _currentIndex = _tracks.Count - 1;
                }
            }

            return _tracks[_currentIndex];
        }

        /// <summary>
        /// Get previous track
        /// </summary>
        public MusicFile Previous()
        {
            if (_tracks.Count == 0) return null;

            _currentIndex--;
            if (_currentIndex < 0)
            {
                if (RepeatEnabled)
                    _currentIndex = _tracks.Count - 1;
                else
                    _currentIndex = 0;
            }

            return _tracks[_currentIndex];
        }

        /// <summary>
        /// Get current track
        /// </summary>
        public MusicFile Current()
        {
            if (_currentIndex < 0 || _currentIndex >= _tracks.Count) return null;
            return _tracks[_currentIndex];
        }

        /// <summary>
        /// Reset playlist
        /// </summary>
        public void Clear()
        {
            _tracks.Clear();
            _currentIndex = -1;
        }
    }
}

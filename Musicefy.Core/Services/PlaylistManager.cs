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
        private readonly Random _rnd = new Random(); 

        public bool ShuffleEnabled { get; set; } = false;
        public bool RepeatEnabled { get; set; } = false;

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
                if (_tracks.Any(t => t.FilePath == file)) continue; 

                var track = new MusicFile
                {
                    Title = Path.GetFileNameWithoutExtension(file),
                    Artist = "Local",
                    Album = "",
                    Year = DateTime.Now.Year,
                    FilePath = file,
                    SourceUri = file,
                    SourceType = "Local"
                };
                _tracks.Add(track);
            }
        }

        public IEnumerable<MusicFile> All() => _tracks.AsReadOnly();

        public IEnumerable<MusicFile> GetSampleTracks()
        {
            return new[]
            {
                new MusicFile("Song Title 1", "Artist 1", "Album A", 2020, @"C:\Music\song1.mp3"),
                new MusicFile("Song Title 2", "Artist 2", "Album B", 2019, @"C:\Music\song2.mp3"),
                new MusicFile("Song Title 3", "Artist 3", "Album C", 2021, @"C:\Music\song3.mp3")
            };
        }

        public void Shuffle()
        {
            _tracks = _tracks.OrderBy(x => _rnd.Next()).ToList();
            _currentIndex = -1;
            ShuffleEnabled = true;
        }

        public MusicFile Next()
        {
            if (_tracks.Count == 0) return null;

            if (ShuffleEnabled)
            {
                _currentIndex = _rnd.Next(_tracks.Count);
            }
            else
            {
                _currentIndex++;
                if (_currentIndex >= _tracks.Count)
                {
                    if (RepeatEnabled)
                        _currentIndex = 0;
                    else
                        return null; 
                }
            }

            return _tracks[_currentIndex];
        }

        public MusicFile Previous()
        {
            if (_tracks.Count == 0) return null;

            _currentIndex--;
            if (_currentIndex < 0)
            {
                if (RepeatEnabled)
                    _currentIndex = _tracks.Count - 1;
                else
                    return null; 
            }

            return _tracks[_currentIndex];
        }

        public MusicFile Current()
        {
            if (_currentIndex < 0 || _currentIndex >= _tracks.Count) return null;
            return _tracks[_currentIndex];
        }

        public void Clear()
        {
            _tracks.Clear();
            _currentIndex = -1;
        }
    }
}

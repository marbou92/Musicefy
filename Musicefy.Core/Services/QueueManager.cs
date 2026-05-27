using System;
using System.Collections.Generic;
using System.Linq;
using Musicefy.Core.Interfaces;
using Musicefy.Core.Models;

namespace Musicefy.Core.Services
{
    /// <summary>
    /// Queue manager implementation with shuffle and repeat support
    /// </summary>
    public class QueueManager : IQueueManager
    {
        private readonly List<MusicFile> _tracks = new List<MusicFile>();
        private readonly List<MusicFile> _originalOrder = new List<MusicFile>();
        private readonly HashSet<MusicFile> _trackSet = new HashSet<MusicFile>();
        private readonly Random _random = new Random();
        private int _currentIndex = -1;
        private readonly object _lock = new object();

        public event Action<MusicFile> TrackChanged;
        public event Action<int> IndexChanged;

        public IReadOnlyList<MusicFile> Tracks
        {
            get { lock (_lock) return _tracks.ToList().AsReadOnly(); }
        }
        public int CurrentIndex
        {
            get { lock (_lock) return _currentIndex; }
        }

        public bool ShuffleEnabled { get; set; } = false;
        public bool RepeatEnabled { get; set; } = false;

        public QueueManager()
        {
        }

        public MusicFile Current()
        {
            lock (_lock)
            {
                if (_currentIndex < 0 || _currentIndex >= _tracks.Count)
                    return null;
                return _tracks[_currentIndex];
            }
        }

        public MusicFile Next()
        {
            lock (_lock)
            {
                if (_tracks.Count == 0) return null;

                _currentIndex++;
                if (_currentIndex >= _tracks.Count)
                {
                    if (RepeatEnabled)
                        _currentIndex = 0;
                    else
                        return null;
                }

                IndexChanged?.Invoke(_currentIndex);
                TrackChanged?.Invoke(_tracks[_currentIndex]);
                return _tracks[_currentIndex];
            }
        }

        public MusicFile Previous()
        {
            lock (_lock)
            {
                if (_tracks.Count == 0) return null;

                if (_currentIndex < 0 || _currentIndex >= _tracks.Count)
                    _currentIndex = 0;

                _currentIndex--;
                if (_currentIndex < 0)
                {
                    if (RepeatEnabled)
                        _currentIndex = _tracks.Count - 1;
                    else
                    {
                        _currentIndex = 0;
                        return null;
                    }
                }

                IndexChanged?.Invoke(_currentIndex);
                TrackChanged?.Invoke(_tracks[_currentIndex]);
                return _tracks[_currentIndex];
            }
        }

        public void Enqueue(MusicFile track)
        {
            if (track == null) throw new ArgumentNullException(nameof(track));
            lock (_lock)
            {
                if (!_trackSet.Add(track)) return;

                _tracks.Add(track);
                _originalOrder.Add(track);

                if (_tracks.Count == 1)
                    _currentIndex = 0;
            }
        }

        public void EnqueueRange(IEnumerable<MusicFile> tracks)
        {
            lock (_lock)
            {
                foreach (var track in tracks)
                {
                    if (track == null) throw new ArgumentNullException(nameof(track));
                    if (!_trackSet.Add(track)) continue;
                    _tracks.Add(track);
                    _originalOrder.Add(track);
                    if (_tracks.Count == 1)
                        _currentIndex = 0;
                }
            }
        }

        public bool Remove(MusicFile track)
        {
            lock (_lock)
            {
                var index = _tracks.IndexOf(track);
                if (index < 0) return false;

                _tracks.Remove(track);
                _originalOrder.Remove(track);
                _trackSet.Remove(track);

                if (index < _currentIndex)
                {
                    _currentIndex--;
                }
                else if (index == _currentIndex && _currentIndex >= _tracks.Count)
                {
                    _currentIndex = _tracks.Count > 0 ? _tracks.Count - 1 : -1;
                }

                return true;
            }
        }

        public void InsertAt(int index, MusicFile track)
        {
            if (index < 0)
                throw new ArgumentOutOfRangeException(nameof(index));
            lock (_lock)
            {
                if (index > _tracks.Count)
                    throw new ArgumentOutOfRangeException(nameof(index));

                _tracks.Insert(index, track);
                _originalOrder.Insert(index, track);
                _trackSet.Add(track);

                if (index <= _currentIndex)
                    _currentIndex++;
            }
        }

        public void JumpToIndex(int index)
        {
            lock (_lock)
            {
                if (index < 0 || index >= _tracks.Count)
                    throw new ArgumentOutOfRangeException(nameof(index));

                _currentIndex = index;
                IndexChanged?.Invoke(_currentIndex);
                TrackChanged?.Invoke(_tracks[_currentIndex]);
            }
        }

        public void Clear()
        {
            lock (_lock)
            {
                _tracks.Clear();
                _originalOrder.Clear();
                _trackSet.Clear();
                _currentIndex = -1;
                IndexChanged?.Invoke(-1);
            }
        }

        public void Shuffle()
        {
            lock (_lock)
            {
                var currentTrack = _currentIndex >= 0 && _currentIndex < _tracks.Count ? _tracks[_currentIndex] : null;

                _tracks.Clear();
                _tracks.AddRange(_originalOrder);
                // Fisher-Yates shuffle
                for (int i = _tracks.Count - 1; i > 0; i--)
                {
                    int j = _random.Next(i + 1);
                    var tmp = _tracks[i];
                    _tracks[i] = _tracks[j];
                    _tracks[j] = tmp;
                }
                _trackSet.Clear();
                _trackSet.UnionWith(_tracks);

                if (currentTrack != null)
                {
                    int newIndex = _tracks.IndexOf(currentTrack);
                    _currentIndex = newIndex >= 0 ? newIndex : 0;
                }
                else
                {
                    _currentIndex = _tracks.Count > 0 ? 0 : -1;
                }

                ShuffleEnabled = true;
            }
        }

        public void RestoreOrder()
        {
            lock (_lock)
            {
                var currentTrack = _currentIndex >= 0 && _currentIndex < _tracks.Count ? _tracks[_currentIndex] : null;

                _tracks.Clear();
                _tracks.AddRange(_originalOrder);
                _trackSet.Clear();
                _trackSet.UnionWith(_tracks);

                if (currentTrack != null)
                {
                    int newIndex = _tracks.IndexOf(currentTrack);
                    _currentIndex = newIndex >= 0 ? newIndex : 0;
                }
                else
                {
                    _currentIndex = _tracks.Count > 0 ? 0 : -1;
                }

                ShuffleEnabled = false;
            }
        }

        public void LoadFromLocalFolder(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath))
                throw new ArgumentNullException(nameof(folderPath));

            if (!System.IO.Directory.Exists(folderPath))
                throw new System.IO.DirectoryNotFoundException($"Directory not found: {folderPath}");

            var extensions = new[] { ".mp3", ".wav", ".flac", ".ogg", ".m4a", ".aac" };
            var files = System.IO.Directory.EnumerateFiles(folderPath, "*.*", System.IO.SearchOption.AllDirectories)
                .Where(f => extensions.Any(e => f.EndsWith(e, StringComparison.OrdinalIgnoreCase)));

            lock (_lock)
            {
                foreach (var file in files)
                {
                    var track = new MusicFile
                    {
                        Title = System.IO.Path.GetFileNameWithoutExtension(file),
                        Artist = "Local",
                        FilePath = file,
                        SourceUri = file,
                        SourceType = "Local"
                    };
                    if (!_trackSet.Add(track)) continue;
                    _tracks.Add(track);
                    _originalOrder.Add(track);
                    if (_tracks.Count == 1)
                        _currentIndex = 0;
                }
            }
        }
    }
}

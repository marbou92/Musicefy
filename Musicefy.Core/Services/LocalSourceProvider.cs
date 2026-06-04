using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Musicefy.Core.Interfaces;
using Musicefy.Core.Library;
using Musicefy.Core.Models;
using static Musicefy.Core.SourceTypes;

namespace Musicefy.Core.Services
{
    public class LocalSourceProvider : IMusicSourceProvider
    {
        private readonly ILibraryService _libraryService;
        private readonly IServiceProvider _serviceProvider;

        public string SourceType => Local;
        public string DisplayName => "Local Folder";
        public string Description => "Music files stored on your local disk";
        public string IconGlyph => "💻";

        public IReadOnlyList<SourceConfigField> ConfigurationFields { get; } = new List<SourceConfigField>
        {
            new SourceConfigField
            {
                Key = "folderPath",
                Label = "Folder Path",
                Description = "Path to a local folder containing music files",
                IsRequired = true,
                Placeholder = @"C:\Users\You\Music"
            }
        };

        /// <summary>
        /// Default constructor for backward compatibility (no library service integration).
        /// Falls back to basic file system scanning without metadata search.
        /// </summary>
        public LocalSourceProvider()
        {
            _libraryService = null;
            _serviceProvider = null;
        }

        /// <summary>
        /// Constructor with library service for rich metadata support.
        /// Uses IServiceProvider for lazy resolution of ArtistAlbumService to avoid
        /// circular dependency (ArtistAlbumService -> IStreamingSourceManager -> IMusicSourceProvider -> this).
        /// </summary>
        public LocalSourceProvider(ILibraryService libraryService, IServiceProvider serviceProvider)
        {
            _libraryService = libraryService ?? throw new ArgumentNullException(nameof(libraryService));
            _serviceProvider = serviceProvider;
        }

        public Task<bool> TestConnectionAsync(IReadOnlyDictionary<string, string> config)
        {
            var path = GetConfig(config, "folderPath");
            return Task.FromResult(!string.IsNullOrEmpty(path) && Directory.Exists(path));
        }

        public IMusicSourceSession CreateSession(IReadOnlyDictionary<string, string> config, string sourceId)
        {
            return new LocalSourceSession(config, sourceId, _libraryService, _serviceProvider);
        }

        private static string GetConfig(IReadOnlyDictionary<string, string> config, string key)
        {
            return config.TryGetValue(key, out var val) ? val ?? "" : "";
        }

        private class LocalSourceSession : IMusicSourceSession
        {
            private readonly string _folderPath;
            private readonly string _sourceId;
            private readonly ILibraryService _libraryService;
            private readonly IServiceProvider _serviceProvider;
            private ArtistAlbumService _artistAlbumService;
            private static readonly HashSet<string> _extensions = MusicFileExtensions.All;

            public LocalSourceSession(
                IReadOnlyDictionary<string, string> config,
                string sourceId,
                ILibraryService libraryService,
                IServiceProvider serviceProvider)
            {
                _sourceId = sourceId;
                _folderPath = GetConfig(config, "folderPath");
                _libraryService = libraryService;
                _serviceProvider = serviceProvider;
            }

            /// <summary>
            /// Lazily resolve ArtistAlbumService to avoid circular dependency at construction time.
            /// </summary>
            private ArtistAlbumService GetArtistAlbumService()
            {
                if (_artistAlbumService == null && _serviceProvider != null)
                {
                    _artistAlbumService = _serviceProvider.GetService(typeof(ArtistAlbumService)) as ArtistAlbumService;
                }
                return _artistAlbumService;
            }

            public async Task<IReadOnlyList<MusicFile>> SearchAsync(string query, int limit = 50)
            {
                // Use ILibraryService.SearchAsync for proper metadata search if available
                if (_libraryService != null)
                {
                    try
                    {
                        var results = await _libraryService.SearchAsync(query);
                        if (results != null && results.Count > 0)
                        {
                            // Filter to only include tracks within this source's folder path
                            if (!string.IsNullOrEmpty(_folderPath))
                            {
                                results = results
                                    .Where(t => t.FilePath != null &&
                                                t.FilePath.StartsWith(_folderPath, StringComparison.OrdinalIgnoreCase))
                                    .ToList();
                            }
                            return results.Take(limit).ToList();
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"[LocalSourceProvider] ILibraryService.SearchAsync failed, falling back to file search: {ex.Message}");
                    }
                }

                // Fallback: basic file system search
                return await SearchFileSystemAsync(query, limit);
            }

            private Task<IReadOnlyList<MusicFile>> SearchFileSystemAsync(string query, int limit)
            {
                var results = new List<MusicFile>();

                if (!Directory.Exists(_folderPath))
                    return Task.FromResult<IReadOnlyList<MusicFile>>(results);

                var files = Directory.EnumerateFiles(_folderPath, "*.*", SearchOption.AllDirectories)
                    .Where(f => _extensions.Contains(System.IO.Path.GetExtension(f)));

                foreach (var file in files)
                {
                    if (results.Count >= limit)
                        break;

                    var name = Path.GetFileNameWithoutExtension(file);
                    if (string.IsNullOrEmpty(query) || name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        results.Add(new MusicFile
                        {
                            Title = name,
                            Artist = "Local",
                            FilePath = file,
                            SourceUri = file,
                            SourceType = Local
                        });
                    }
                }

                return Task.FromResult<IReadOnlyList<MusicFile>>(results);
            }

            public Task<string> GetStreamUrlAsync(string trackId)
            {
                return Task.FromResult(trackId);
            }

            public async Task<byte[]> GetCoverArtAsync(string coverId)
            {
                if (string.IsNullOrEmpty(coverId))
                    return null;

                // coverId for local sources is the file path
                try
                {
                    // First try the artwork cache from LibraryScanner
                    var cachePath = LibraryScanner.GetArtworkCachePath(coverId);
                    if (File.Exists(cachePath))
                    {
                        return File.ReadAllBytes(cachePath);
                    }

                    // Then try to find folder artwork
                    if (File.Exists(coverId))
                    {
                        var dir = Path.GetDirectoryName(coverId);
                        if (dir != null)
                        {
                            var folderArtNames = new[] { "cover.jpg", "cover.png", "folder.jpg", "folder.png",
                                "front.jpg", "front.png", "album.jpg", "album.png" };
                            foreach (var artName in folderArtNames)
                            {
                                var candidate = Path.Combine(dir, artName);
                                if (File.Exists(candidate))
                                {
                                    return File.ReadAllBytes(candidate);
                                }
                            }
                        }
                    }

                    // Use ILibraryService to get artwork cache path if available
                    if (_libraryService != null)
                    {
                        var artworkPath = _libraryService.GetArtworkCachePath(coverId);
                        if (File.Exists(artworkPath))
                        {
                            return File.ReadAllBytes(artworkPath);
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[LocalSourceProvider] GetCoverArtAsync failed for {coverId}: {ex.Message}");
                }

                return null;
            }

            public async Task<IReadOnlyList<MusicFile>> GetRandomSongsAsync(int count = 50)
            {
                // Use ILibraryService for random songs if available
                if (_libraryService != null)
                {
                    try
                    {
                        var allTracks = await _libraryService.GetAllTracksAsync();

                        if (allTracks != null && allTracks.Count > 0)
                        {
                            // Filter to tracks within this source's folder path
                            if (!string.IsNullOrEmpty(_folderPath))
                            {
                                allTracks = allTracks
                                    .Where(t => t.FilePath != null &&
                                                t.FilePath.StartsWith(_folderPath, StringComparison.OrdinalIgnoreCase))
                                    .ToList();
                            }

                            // Fisher-Yates shuffle for efficient random ordering
                            var random = new Random();
                            var shuffled = allTracks.OrderBy(_ => random.Next()).Take(count).ToList();
                            return shuffled;
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"[LocalSourceProvider] ILibraryService.GetRandomSongsAsync failed: {ex.Message}");
                    }
                }

                // Fallback: file system enumeration with efficient random sampling
                return await GetRandomSongsFromFileSystemAsync(count);
            }

            private Task<IReadOnlyList<MusicFile>> GetRandomSongsFromFileSystemAsync(int count)
            {
                var results = new List<MusicFile>();

                if (!Directory.Exists(_folderPath))
                    return Task.FromResult<IReadOnlyList<MusicFile>>(results);

                var files = Directory.EnumerateFiles(_folderPath, "*.*", SearchOption.AllDirectories)
                    .Where(f => _extensions.Any(e => f.EndsWith(e, StringComparison.OrdinalIgnoreCase)))
                    .ToList();

                if (files.Count == 0)
                    return Task.FromResult<IReadOnlyList<MusicFile>>(results);

                // Fisher-Yates shuffle for efficient random sampling
                var random = new Random();
                var n = files.Count;
                for (int i = n - 1; i > 0 && results.Count < count; i--)
                {
                    int j = random.Next(i + 1);
                    var temp = files[i];
                    files[i] = files[j];
                    files[j] = temp;
                }

                foreach (var file in files.Take(count))
                {
                    results.Add(new MusicFile
                    {
                        Title = Path.GetFileNameWithoutExtension(file),
                        Artist = "Local",
                        FilePath = file,
                        SourceUri = file,
                        SourceType = Local
                    });
                }

                return Task.FromResult<IReadOnlyList<MusicFile>>(results);
            }

            public async Task<IReadOnlyList<MusicFile>> GetAlbumListAsync(int count = 50)
            {
                // Use ArtistAlbumService for album listing if available (lazy resolution)
                var albumService = GetArtistAlbumService();
                if (albumService != null)
                {
                    try
                    {
                        var albums = await albumService.GetAlbumsAsync();
                        if (albums != null)
                        {
                            // Filter albums that have tracks within this source's folder path
                            if (!string.IsNullOrEmpty(_folderPath))
                            {
                                albums = albums
                                    .Where(a => a.Tracks.Any(t => t.FilePath != null &&
                                                  t.FilePath.StartsWith(_folderPath, StringComparison.OrdinalIgnoreCase)))
                                    .ToList();
                            }

                            // Return tracks from albums
                            return albums
                                .Take(count)
                                .SelectMany(a => a.Tracks)
                                .ToList();
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"[LocalSourceProvider] GetAlbumListAsync failed: {ex.Message}");
                    }
                }

                // Fallback: use ILibraryService to compute albums from tracks
                if (_libraryService != null)
                {
                    try
                    {
                        var allTracks = await _libraryService.GetAllTracksAsync();
                        if (allTracks != null && allTracks.Count > 0)
                        {
                            if (!string.IsNullOrEmpty(_folderPath))
                            {
                                allTracks = allTracks
                                    .Where(t => t.FilePath != null &&
                                                t.FilePath.StartsWith(_folderPath, StringComparison.OrdinalIgnoreCase))
                                    .ToList();
                            }

                            return allTracks
                                .Where(t => !string.IsNullOrEmpty(t.Album))
                                .GroupBy(t => new { t.Album, t.Artist })
                                .Take(count)
                                .SelectMany(g => g.OrderBy(t => t.TrackNumber))
                                .ToList();
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"[LocalSourceProvider] GetAlbumListAsync fallback failed: {ex.Message}");
                    }
                }

                return new List<MusicFile>();
            }

            public async Task<IReadOnlyList<MusicFile>> GetAlbumAsync(string albumId)
            {
                // Use ArtistAlbumService for album tracks if available (lazy resolution)
                var albumService = GetArtistAlbumService();
                if (albumService != null)
                {
                    try
                    {
                        var album = await albumService.GetAlbumDetailAsync(albumId);
                        if (album != null)
                        {
                            return album.Tracks;
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"[LocalSourceProvider] GetAlbumAsync failed for {albumId}: {ex.Message}");
                    }
                }

                // Fallback: use ILibraryService to search for album tracks
                if (_libraryService != null)
                {
                    try
                    {
                        var allTracks = await _libraryService.GetAllTracksAsync();
                        if (allTracks != null)
                        {
                            if (!string.IsNullOrEmpty(_folderPath))
                            {
                                allTracks = allTracks
                                    .Where(t => t.FilePath != null &&
                                                t.FilePath.StartsWith(_folderPath, StringComparison.OrdinalIgnoreCase))
                                    .ToList();
                            }

                            return allTracks
                                .Where(t => string.Equals(t.Album, albumId, StringComparison.OrdinalIgnoreCase))
                                .OrderBy(t => t.TrackNumber)
                                .ToList();
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"[LocalSourceProvider] GetAlbumAsync fallback failed for {albumId}: {ex.Message}");
                    }
                }

                return new List<MusicFile>();
            }

            public async Task<IReadOnlyList<MusicFile>> GetArtistAsync(string artistId)
            {
                // Use ArtistAlbumService for artist tracks if available (lazy resolution)
                var albumService = GetArtistAlbumService();
                if (albumService != null)
                {
                    try
                    {
                        var artist = await albumService.GetArtistDetailAsync(artistId);
                        if (artist != null)
                        {
                            return artist.Tracks;
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"[LocalSourceProvider] GetArtistAsync failed for {artistId}: {ex.Message}");
                    }
                }

                // Fallback: use ILibraryService to search for artist tracks
                if (_libraryService != null)
                {
                    try
                    {
                        var allTracks = await _libraryService.GetAllTracksAsync();
                        if (allTracks != null)
                        {
                            if (!string.IsNullOrEmpty(_folderPath))
                            {
                                allTracks = allTracks
                                    .Where(t => t.FilePath != null &&
                                                t.FilePath.StartsWith(_folderPath, StringComparison.OrdinalIgnoreCase))
                                    .ToList();
                            }

                            return allTracks
                                .Where(t => string.Equals(t.Artist, artistId, StringComparison.OrdinalIgnoreCase))
                                .OrderBy(t => t.Album).ThenBy(t => t.TrackNumber)
                                .ToList();
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"[LocalSourceProvider] GetArtistAsync fallback failed for {artistId}: {ex.Message}");
                    }
                }

                return new List<MusicFile>();
            }

            public async Task<IReadOnlyList<MusicFile>> GetAllTracksAsync(int limit = 500)
            {
                // Use ILibraryService for all tracks if available
                if (_libraryService != null)
                {
                    try
                    {
                        var allTracks = await _libraryService.GetAllTracksAsync();
                        if (allTracks != null)
                        {
                            if (!string.IsNullOrEmpty(_folderPath))
                            {
                                allTracks = allTracks
                                    .Where(t => t.FilePath != null &&
                                                t.FilePath.StartsWith(_folderPath, StringComparison.OrdinalIgnoreCase))
                                    .ToList();
                            }
                            return allTracks.Take(limit).ToList();
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"[LocalSourceProvider] GetAllTracksAsync failed: {ex.Message}");
                    }
                }

                // Fallback: file system enumeration
                if (!Directory.Exists(_folderPath))
                    return new List<MusicFile>();

                var results = Directory.EnumerateFiles(_folderPath, "*.*", SearchOption.AllDirectories)
                    .Where(f => _extensions.Contains(System.IO.Path.GetExtension(f)))
                    .Take(limit)
                    .Select(f => new MusicFile
                    {
                        Title = Path.GetFileNameWithoutExtension(f),
                        Artist = "Local",
                        FilePath = f,
                        SourceUri = f,
                        SourceType = Local
                    })
                    .ToList();

                return results;
            }

            public void Dispose()
            {
            }
        }
    }
}

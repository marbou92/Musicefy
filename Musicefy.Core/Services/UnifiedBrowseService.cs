using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Musicefy.Core.Interfaces;
using Musicefy.Core.Models;
using static Musicefy.Core.SourceTypes;

namespace Musicefy.Core.Services
{
    /// <summary>
    /// Unified browse service that aggregates content from all connected sources.
    /// Inspired by Echo Music's YouTube.home() and YouTube.explore() but
    /// adapted for Musicefy's multi-source architecture.
    /// </summary>
    public class UnifiedBrowseService : IBrowseService
    {
        private readonly IStreamingSourceManager _sourceManager;
        private readonly ILibraryService _libraryService;
        private readonly ArtistAlbumService _artistAlbumService;

        public UnifiedBrowseService(
            IStreamingSourceManager sourceManager,
            ILibraryService libraryService,
            ArtistAlbumService artistAlbumService)
        {
            _sourceManager = sourceManager ?? throw new ArgumentNullException(nameof(sourceManager));
            _libraryService = libraryService ?? throw new ArgumentNullException(nameof(libraryService));
            _artistAlbumService = artistAlbumService ?? throw new ArgumentNullException(nameof(artistAlbumService));
        }

        /// <summary>
        /// Get the home page with sections from all sources.
        /// Sections include: Quick Picks (favourites), Keep Listening (history),
        /// YouTube home feed, and Subsonic newest albums.
        /// </summary>
        public async Task<BrowsePage> GetHomePageAsync(CancellationToken ct = default)
        {
            var page = new BrowsePage();
            var sections = new List<BrowseSection>();

            try
            {
                // Section 1: Quick Picks — local favourites
                try
                {
                    var favourites = await _libraryService.GetFavouriteTracksAsync(ct);
                    if (favourites?.Count > 0)
                    {
                        sections.Add(new BrowseSection
                        {
                            Title = "Quick Picks",
                            SectionType = "QuickPicks",
                            BaseWeight = 100,
                            SourceType = Local,
                            Items = favourites.Take(20).Cast<object>().ToList()
                        });
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[BrowseService] Failed to load favourites: {ex.Message}");
                }

                // Section 2: Keep Listening — recently played
                try
                {
                    var history = await _libraryService.GetHistoryTracksAsync(20, ct);
                    if (history?.Count > 0)
                    {
                        sections.Add(new BrowseSection
                        {
                            Title = "Keep Listening",
                            SectionType = "KeepListening",
                            BaseWeight = 90,
                            SourceType = Local,
                            Items = history.Cast<object>().ToList()
                        });
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[BrowseService] Failed to load history: {ex.Message}");
                }

                // Section 3: YouTube home feed
                try
                {
                    var ytSources = _sourceManager.Sources
                        .Where(s => s.IsConnected && s.Type == YouTube)
                        .ToList();

                    foreach (var ytSource in ytSources)
                    {
                        var ytSession = _sourceManager.GetYouTubeSession(ytSource.Id);
                        if (ytSession != null)
                        {
                            var ytHome = await ytSession.GetRandomSongsAsync(20);
                            if (ytHome?.Count > 0)
                            {
                                sections.Add(new BrowseSection
                                {
                                    Title = "YouTube Music",
                                    SectionType = "DailyDiscover",
                                    BaseWeight = 80,
                                    SourceId = ytSource.Id,
                                    SourceType = YouTube,
                                    Items = ytHome.Cast<object>().ToList()
                                });
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[BrowseService] Failed to load YouTube home: {ex.Message}");
                }

                // Section 4: Subsonic newest albums
                try
                {
                    var subsonicSources = _sourceManager.Sources
                        .Where(s => s.IsConnected && s.Type == Subsonic)
                        .ToList();

                    foreach (var subSource in subsonicSources)
                    {
                        var session = _sourceManager.GetSession(subSource.Id);
                        if (session != null)
                        {
                            var newestAlbums = await session.GetAlbumListAsync(20);
                            if (newestAlbums?.Count > 0)
                            {
                                sections.Add(new BrowseSection
                                {
                                    Title = $"New on {subSource.Name}",
                                    SectionType = "Albums",
                                    BaseWeight = 70,
                                    SourceId = subSource.Id,
                                    SourceType = Subsonic,
                                    Items = newestAlbums.Cast<object>().ToList()
                                });
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[BrowseService] Failed to load Subsonic albums: {ex.Message}");
                }

                // Sort sections by weight (highest first)
                page.Sections = sections.OrderByDescending(s => s.BaseWeight).ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[BrowseService] GetHomePageAsync failed: {ex.Message}");
            }

            return page;
        }

        /// <summary>
        /// Get new releases and trending content from all sources.
        /// Sections include: YouTube explore/trending, Subsonic random, local artists.
        /// </summary>
        public async Task<BrowsePage> GetExplorePageAsync(CancellationToken ct = default)
        {
            var page = new BrowsePage();
            var sections = new List<BrowseSection>();

            try
            {
                // Section 1: YouTube trending / explore
                try
                {
                    var ytSources = _sourceManager.Sources
                        .Where(s => s.IsConnected && s.Type == YouTube)
                        .ToList();

                    foreach (var ytSource in ytSources)
                    {
                        var ytSession = _sourceManager.GetYouTubeSession(ytSource.Id);
                        if (ytSession != null)
                        {
                            var ytTrending = await ytSession.GetRandomSongsAsync(30);
                            if (ytTrending?.Count > 0)
                            {
                                sections.Add(new BrowseSection
                                {
                                    Title = "Trending on YouTube",
                                    SectionType = "DailyDiscover",
                                    BaseWeight = 90,
                                    SourceId = ytSource.Id,
                                    SourceType = YouTube,
                                    Items = ytTrending.Cast<object>().ToList()
                                });
                            }

                            // Also get album list for browse
                            var ytAlbums = await ytSession.GetAlbumListAsync(20);
                            if (ytAlbums?.Count > 0)
                            {
                                sections.Add(new BrowseSection
                                {
                                    Title = "New Releases on YouTube",
                                    SectionType = "Albums",
                                    BaseWeight = 75,
                                    SourceId = ytSource.Id,
                                    SourceType = YouTube,
                                    Items = ytAlbums.Cast<object>().ToList()
                                });
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[BrowseService] Failed to load YouTube explore: {ex.Message}");
                }

                // Section 2: Subsonic random songs
                try
                {
                    var subsonicSources = _sourceManager.Sources
                        .Where(s => s.IsConnected && s.Type == Subsonic)
                        .ToList();

                    foreach (var subSource in subsonicSources)
                    {
                        var session = _sourceManager.GetSession(subSource.Id);
                        if (session != null)
                        {
                            var randomSongs = await session.GetRandomSongsAsync(30);
                            if (randomSongs?.Count > 0)
                            {
                                sections.Add(new BrowseSection
                                {
                                    Title = $"Discover on {subSource.Name}",
                                    SectionType = "DailyDiscover",
                                    BaseWeight = 80,
                                    SourceId = subSource.Id,
                                    SourceType = Subsonic,
                                    Items = randomSongs.Cast<object>().ToList()
                                });
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[BrowseService] Failed to load Subsonic random: {ex.Message}");
                }

                // Section 3: Local artists
                try
                {
                    var artists = await _artistAlbumService.GetArtistsAsync(ct);
                    if (artists?.Count > 0)
                    {
                        sections.Add(new BrowseSection
                        {
                            Title = "Your Artists",
                            SectionType = "Artists",
                            BaseWeight = 70,
                            SourceType = Local,
                            Items = artists.Take(20).Cast<object>().ToList()
                        });
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[BrowseService] Failed to load local artists: {ex.Message}");
                }

                page.Sections = sections.OrderByDescending(s => s.BaseWeight).ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[BrowseService] GetExplorePageAsync failed: {ex.Message}");
            }

            return page;
        }

        /// <summary>
        /// Get artist detail from the appropriate source session.
        /// </summary>
        public async Task<ArtistInfo> GetArtistDetailAsync(string sourceId, string artistId, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(sourceId) || string.IsNullOrEmpty(artistId))
                return null;

            try
            {
                // For local sources, use ArtistAlbumService
                var source = _sourceManager.GetSource(sourceId);
                if (source?.Type == Local)
                {
                    return await _artistAlbumService.GetArtistDetailAsync(artistId, ct);
                }

                // For remote sources, use the source session
                var session = _sourceManager.GetSession(sourceId);
                if (session != null)
                {
                    var tracks = await session.GetArtistAsync(artistId);
                    if (tracks?.Count > 0)
                    {
                        var artistName = tracks[0].Artist;
                        var albums = tracks
                            .GroupBy(t => t.Album)
                            .Where(g => !string.IsNullOrEmpty(g.Key))
                            .Select(g => new AlbumInfo
                            {
                                Name = g.Key,
                                Artist = artistName,
                                Year = g.Max(t => t.Year),
                                CoverPath = g.FirstOrDefault(t => !string.IsNullOrEmpty(t.CoverPath))?.CoverPath,
                                SourceType = g.FirstOrDefault()?.SourceType,
                                Tracks = g.OrderBy(t => t.TrackNumber).ToList()
                            }).ToList();

                        return new ArtistInfo
                        {
                            Name = artistName,
                            CoverPath = tracks.FirstOrDefault(t => !string.IsNullOrEmpty(t.CoverPath))?.CoverPath,
                            SourceType = source.Type,
                            Tracks = tracks.ToList(),
                            Albums = albums
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[BrowseService] GetArtistDetailAsync failed for {artistId}: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Get album detail from the appropriate source session.
        /// </summary>
        public async Task<AlbumInfo> GetAlbumDetailAsync(string sourceId, string albumId, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(sourceId) || string.IsNullOrEmpty(albumId))
                return null;

            try
            {
                // For local sources, use ArtistAlbumService
                var source = _sourceManager.GetSource(sourceId);
                if (source?.Type == Local)
                {
                    return await _artistAlbumService.GetAlbumDetailAsync(albumId, null, ct);
                }

                // For remote sources, use the source session
                var session = _sourceManager.GetSession(sourceId);
                if (session != null)
                {
                    var tracks = await session.GetAlbumAsync(albumId);
                    if (tracks?.Count > 0)
                    {
                        return new AlbumInfo
                        {
                            Name = tracks[0].Album,
                            Artist = tracks[0].Artist,
                            Year = tracks.Max(t => t.Year),
                            CoverPath = tracks.FirstOrDefault(t => !string.IsNullOrEmpty(t.CoverPath))?.CoverPath,
                            SourceType = source.Type,
                            Tracks = tracks.OrderBy(t => t.TrackNumber).ToList()
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[BrowseService] GetAlbumDetailAsync failed for {albumId}: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Get random songs from all connected sources.
        /// </summary>
        public async Task<List<MusicFile>> GetRandomSongsAsync(int count = 50, CancellationToken ct = default)
        {
            var results = new List<MusicFile>();
            var connectedSources = _sourceManager.Sources.Where(s => s.IsConnected).ToList();

            if (connectedSources.Count == 0)
            {
                // Fallback to local library
                try
                {
                    var allTracks = await _libraryService.GetAllTracksAsync(ct);
                    if (allTracks?.Count > 0)
                    {
                        var random = new Random();
                        results = allTracks.OrderBy(_ => random.Next()).Take(count).ToList();
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[BrowseService] Local random failed: {ex.Message}");
                }

                return results;
            }

            // Distribute count across sources proportionally
            var perSource = Math.Max(count / connectedSources.Count, 10);
            var tasks = connectedSources.Select(async source =>
            {
                try
                {
                    var session = _sourceManager.GetSession(source.Id);
                    if (session != null)
                    {
                        var songs = await session.GetRandomSongsAsync(perSource);
                        return songs?.ToList() ?? new List<MusicFile>();
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[BrowseService] GetRandomSongsAsync failed for {source.Name}: {ex.Message}");
                }
                return new List<MusicFile>();
            });

            try
            {
                var allResults = await Task.WhenAll(tasks);
                foreach (var sourceResults in allResults)
                {
                    results.AddRange(sourceResults);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[BrowseService] GetRandomSongsAsync aggregation failed: {ex.Message}");
            }

            return results.Take(count).ToList();
        }

        /// <summary>
        /// Get albums from all connected sources using ArtistAlbumService.
        /// </summary>
        public async Task<List<AlbumInfo>> GetAlbumsAsync(CancellationToken ct = default)
        {
            try
            {
                return await _artistAlbumService.GetAlbumsAsync(ct);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[BrowseService] GetAlbumsAsync failed: {ex.Message}");
                return new List<AlbumInfo>();
            }
        }

        /// <summary>
        /// Get artists from all connected sources using ArtistAlbumService.
        /// </summary>
        public async Task<List<ArtistInfo>> GetArtistsAsync(CancellationToken ct = default)
        {
            try
            {
                return await _artistAlbumService.GetArtistsAsync(ct);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[BrowseService] GetArtistsAsync failed: {ex.Message}");
                return new List<ArtistInfo>();
            }
        }
    }
}

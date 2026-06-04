using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Musicefy.Core.Interfaces;
using Musicefy.Core.Models;

namespace Musicefy.Core.Services
{
    public class ArtistAlbumService
    {
        private readonly IStreamingSourceManager _sourceManager;
        private List<ArtistInfo> _cachedArtists;
        private List<AlbumInfo> _cachedAlbums;
        private DateTime _cacheTime;
        private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

        public ArtistAlbumService(IStreamingSourceManager sourceManager)
        {
            _sourceManager = sourceManager;
        }

        public async Task<List<ArtistInfo>> GetArtistsAsync(CancellationToken ct = default)
        {
            if (_cachedArtists != null && (DateTime.UtcNow - _cacheTime) < CacheTtl)
                return _cachedArtists;

            var allTracks = await GetAllTracksAsync(ct);

            _cachedArtists = allTracks
                .Where(t => !string.IsNullOrEmpty(t.Artist))
                .GroupBy(t => t.Artist)
                .Select(g => new ArtistInfo
                {
                    // Phase 1: populate stable Id and YouTubeChannelId from track metadata
                    Id = g.FirstOrDefault(t => !string.IsNullOrEmpty(t.ArtistBrowseId))?.ArtistBrowseId
                         ?? $"local_artist:{g.Key}",
                    Name = g.Key,
                    CoverPath = g.FirstOrDefault(t => !string.IsNullOrEmpty(t.CoverPath))?.CoverPath,
                    SourceType = g.FirstOrDefault()?.SourceType,
                    YouTubeChannelId = g.FirstOrDefault(t => !string.IsNullOrEmpty(t.ArtistBrowseId))?.ArtistBrowseId,
                    Tracks = g.OrderBy(t => t.Album).ThenBy(t => t.TrackNumber).ToList(),
                    Albums = g.GroupBy(t => t.Album)
                        .Where(ag => !string.IsNullOrEmpty(ag.Key))
                        .Select(ag => new AlbumInfo
                        {
                            Id = ag.FirstOrDefault(t => !string.IsNullOrEmpty(t.AlbumBrowseId))?.AlbumBrowseId
                                 ?? $"local_album:{ag.Key}:{g.Key}",
                            Name = ag.Key,
                            Artist = g.Key,
                            Year = ag.Max(t => t.Year),
                            CoverPath = ag.FirstOrDefault(t => !string.IsNullOrEmpty(t.CoverPath))?.CoverPath,
                            SourceType = ag.FirstOrDefault()?.SourceType,
                            YouTubeAlbumId = ag.FirstOrDefault(t => !string.IsNullOrEmpty(t.AlbumBrowseId))?.AlbumBrowseId,
                            Tracks = ag.OrderBy(t => t.TrackNumber).ToList()
                        }).ToList()
                })
                .OrderBy(a => a.Name)
                .ToList();

            _cacheTime = DateTime.UtcNow;
            return _cachedArtists;
        }

        public async Task<List<AlbumInfo>> GetAlbumsAsync(CancellationToken ct = default)
        {
            if (_cachedAlbums != null && (DateTime.UtcNow - _cacheTime) < CacheTtl)
                return _cachedAlbums;

            var artists = await GetArtistsAsync(ct);
            _cachedAlbums = artists
                .SelectMany(a => a.Albums)
                .GroupBy(a => new { a.Name, a.Artist })
                .Select(g => g.First())
                .OrderBy(a => a.Artist).ThenBy(a => a.Name)
                .ToList();

            return _cachedAlbums;
        }

        public async Task<ArtistInfo> GetArtistDetailAsync(string artistName, CancellationToken ct = default)
        {
            var artists = await GetArtistsAsync(ct);
            return artists.FirstOrDefault(a => string.Equals(a.Name, artistName, StringComparison.OrdinalIgnoreCase));
        }

        public async Task<AlbumInfo> GetAlbumDetailAsync(string albumName, string artistName = null, CancellationToken ct = default)
        {
            var albums = await GetAlbumsAsync(ct);
            return albums.FirstOrDefault(a =>
                string.Equals(a.Name, albumName, StringComparison.OrdinalIgnoreCase) &&
                (artistName == null || string.Equals(a.Artist, artistName, StringComparison.OrdinalIgnoreCase)));
        }

        /// <summary>
        /// Browse a YouTube Music artist page by channel ID.
        /// Returns rich artist data with top tracks and album references,
        /// each carrying YouTube browse IDs for further navigation.
        /// Falls back to name-based lookup if browse fails.
        /// Inspired by Echo Music's structured artist browsing.
        /// </summary>
        public async Task<ArtistInfo> GetArtistByYouTubeIdAsync(string channelId, string fallbackName = null, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(channelId))
                return fallbackName != null ? await GetArtistDetailAsync(fallbackName, ct) : null;

            try
            {
                var artist = await _sourceManager.BrowseArtistAsync(channelId, ct);
                if (artist != null)
                    return artist;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[ArtistAlbumService] BrowseArtistAsync failed for {channelId}: {ex.Message}");
            }

            // Fallback to name-based lookup
            if (!string.IsNullOrEmpty(fallbackName))
                return await GetArtistDetailAsync(fallbackName, ct);

            return null;
        }

        /// <summary>
        /// Browse a YouTube Music album page by browse ID.
        /// Returns the album's full track list with metadata.
        /// Falls back to name-based lookup if browse fails.
        /// Inspired by Echo Music's two-step album fetch (list → detail).
        /// </summary>
        public async Task<AlbumInfo> GetAlbumByYouTubeIdAsync(string browseId, string fallbackName = null, string fallbackArtist = null, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(browseId))
                return fallbackName != null ? await GetAlbumDetailAsync(fallbackName, fallbackArtist, ct) : null;

            try
            {
                var album = await _sourceManager.BrowseAlbumAsync(browseId, ct);
                if (album != null)
                    return album;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[ArtistAlbumService] BrowseAlbumAsync failed for {browseId}: {ex.Message}");
            }

            // Fallback to name-based lookup
            if (!string.IsNullOrEmpty(fallbackName))
                return await GetAlbumDetailAsync(fallbackName, fallbackArtist, ct);

            return null;
        }

        public void InvalidateCache()
        {
            _cachedArtists = null;
            _cachedAlbums = null;
        }

        private async Task<List<MusicFile>> GetAllTracksAsync(CancellationToken ct)
        {
            var allTracks = new List<MusicFile>();
            var sources = _sourceManager.Sources.Where(s => s.IsConnected).ToList();

            foreach (var source in sources)
            {
                var session = _sourceManager.GetSession(source.Id);
                if (session == null) continue;

                try
                {
                    // Use GetAllTracksAsync instead of SearchAsync("") — semantically correct,
                    // more complete, and avoids the empty-query hack.
                    var tracks = await session.GetAllTracksAsync();
                    if (tracks != null)
                        allTracks.AddRange(tracks);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ArtistAlbumService] Error getting tracks from {source.Name}: {ex.Message}");
                }
            }

            return allTracks;
        }
    }
}

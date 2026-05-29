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
                    Name = g.Key,
                    CoverPath = g.FirstOrDefault(t => !string.IsNullOrEmpty(t.CoverPath))?.CoverPath,
                    SourceType = g.FirstOrDefault()?.SourceType,
                    Tracks = g.OrderBy(t => t.Album).ThenBy(t => t.TrackNumber).ToList(),
                    Albums = g.GroupBy(t => t.Album)
                        .Where(ag => !string.IsNullOrEmpty(ag.Key))
                        .Select(ag => new AlbumInfo
                        {
                            Name = ag.Key,
                            Artist = g.Key,
                            Year = ag.Max(t => t.Year),
                            CoverPath = ag.FirstOrDefault(t => !string.IsNullOrEmpty(t.CoverPath))?.CoverPath,
                            SourceType = ag.FirstOrDefault()?.SourceType,
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
                    var tracks = await session.SearchAsync("", 200);
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

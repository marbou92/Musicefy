using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Musicefy.Core.Models;

namespace Musicefy.Core.Interfaces
{
    /// <summary>
    /// Interface for streaming source management
    /// </summary>
    public interface IStreamingSourceManager
    {
        /// <summary>
        /// All configured sources
        /// </summary>
        IReadOnlyList<StreamingSource> Sources { get; }

        /// <summary>
        /// Add a new streaming source
        /// </summary>
        Task<bool> AddSourceAsync(StreamingSource source, CancellationToken cancellationToken = default);

        /// <summary>
        /// Remove a source by ID
        /// </summary>
        void RemoveSource(string sourceId);

        /// <summary>
        /// Get a specific source by ID
        /// </summary>
        StreamingSource GetSource(string sourceId);

        /// <summary>
        /// Get the active session for a source
        /// </summary>
        IMusicSourceSession GetSession(string sourceId);

        /// <summary>
        /// Get a YouTube-specific session if the source is YouTube.
        /// Enables access to enhanced YouTube features (filtered search, playlists, radio).
        /// </summary>
        IYouTubeSourceSession GetYouTubeSession(string sourceId);

        /// <summary>
        /// Resolve a resource ID to a playable stream URL
        /// </summary>
        Task<string> ResolveStreamUrlAsync(string resourceId);

        /// <summary>
        /// Resolve a cover art resource ID to raw image bytes
        /// </summary>
        Task<byte[]> ResolveCoverArtAsync(string resourceId);

        /// <summary>
        /// Search across all sources (parallel)
        /// </summary>
        Task<List<MusicFile>> SearchAllSourcesAsync(string query, CancellationToken cancellationToken = default);

        /// <summary>
        /// Search YouTube sources with type filter (songs, videos, albums, artists, playlists).
        /// Inspired by Echo Music's filtered search.
        /// </summary>
        Task<List<MusicFile>> SearchYouTubeWithTypeAsync(string query, string resultType, int limit = 50);

        /// <summary>
        /// Get search suggestions from YouTube sources.
        /// Inspired by Echo Music's autocomplete.
        /// </summary>
        Task<List<string>> GetSearchSuggestionsAsync(string query);

        /// <summary>
        /// Test connection to a source
        /// </summary>
        Task<bool> TestConnectionAsync(string sourceId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get all albums from all connected sources
        /// </summary>
        Task<List<AlbumInfo>> GetAllAlbumsAsync(CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Interface for Subsonic API client
    /// </summary>
    public interface ISubsonicClient : IDisposable
    {
        /// <summary>
        /// Test connection to the server
        /// </summary>
        Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Get available music folders
        /// </summary>
        Task<List<string>> GetMusicFoldersAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Search for songs
        /// </summary>
        Task<List<MusicFile>> SearchAsync(string query, int count = 50, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get random songs
        /// </summary>
        Task<List<MusicFile>> GetRandomSongsAsync(int count = 50, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get stream URL for a song
        /// </summary>
        string GetStreamUrl(string songId);

        /// <summary>
        /// Get cover art image bytes
        /// </summary>
        Task<byte[]> GetCoverArtAsync(string coverArtId, int size = 300, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get songs in an album
        /// </summary>
        Task<List<MusicFile>> GetAlbumAsync(string albumId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get artist info and top songs
        /// </summary>
        Task<List<MusicFile>> GetArtistAsync(string artistId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get album list by type (newest, random, recent, frequent, alphabetical)
        /// </summary>
        Task<List<MusicFile>> GetAlbumList2Async(string type = "newest", int size = 50, int offset = 0, CancellationToken cancellationToken = default);
    }
}

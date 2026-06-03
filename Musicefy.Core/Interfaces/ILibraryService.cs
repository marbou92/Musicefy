using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Musicefy.Core.Library;
using Musicefy.Core.Models;

namespace Musicefy.Core.Interfaces
{
    /// <summary>
    /// Interface for music library operations
    /// </summary>
    public interface ILibraryService
    {
        /// <summary>
        /// Initialize database schema
        /// </summary>
        Task EnsureSchemaAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Deep scan of a music folder
        /// </summary>
        Task ScanLibraryDeepAsync(
            string rootPath,
            IProgress<ScanProgressInfo> progress,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Get all tracks from library
        /// </summary>
        Task<List<MusicFile>> GetAllTracksAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Get favourite tracks
        /// </summary>
        Task<List<MusicFile>> GetFavouriteTracksAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Get recently played tracks
        /// </summary>
        Task<List<MusicFile>> GetHistoryTracksAsync(int limit = 100, CancellationToken cancellationToken = default);

        /// <summary>
        /// Toggle favourite status for a track
        /// </summary>
        Task ToggleFavouriteAsync(string filePath, CancellationToken cancellationToken = default);

        /// <summary>
        /// Record that a track was played
        /// </summary>
        Task RecordPlayAsync(string filePath, CancellationToken cancellationToken = default);

        /// <summary>
        /// Scan a single directory (not recursive) for display
        /// </summary>
        List<MusicFile> ScanDirectory(string targetPath);

        /// <summary>
        /// Scan a single directory, skipping files in excludePaths
        /// </summary>
        List<MusicFile> ScanDirectory(string targetPath, ISet<string> excludePaths);

        /// <summary>
        /// Search tracks by query string
        /// </summary>
        Task<List<MusicFile>> SearchAsync(string query, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get artwork cache path for a file
        /// </summary>
        string GetArtworkCachePath(string filePath);

        /// <summary>
        /// Create a new playlist
        /// </summary>
        Task<string> CreatePlaylistAsync(string name);

        /// <summary>
        /// Get most played tracks within the specified number of days
        /// </summary>
        Task<List<MusicFile>> GetMostPlayedAsync(int days, int limit, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get favourite tracks that haven't been played recently (forgotten favorites)
        /// </summary>
        Task<List<MusicFile>> GetForgottenFavoritesAsync(int daysSincePlayed, int limit, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get recently played tracks (wrapper for GetHistoryTracksAsync with default limit)
        /// </summary>
        Task<List<MusicFile>> GetRecentlyPlayedAsync(int limit, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get random favourite tracks for Quick Picks generation
        /// </summary>
        Task<List<MusicFile>> GetRandomFavouriteTracksAsync(int limit, CancellationToken cancellationToken = default);
    }
}

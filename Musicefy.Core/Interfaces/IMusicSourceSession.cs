using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Musicefy.Core.Models;

namespace Musicefy.Core.Interfaces
{
    public interface IMusicSourceSession : IDisposable
    {
        Task<IReadOnlyList<MusicFile>> SearchAsync(string query, int limit = 50);
        Task<string> GetStreamUrlAsync(string trackId);
        Task<byte[]> GetCoverArtAsync(string coverId);
        Task<IReadOnlyList<MusicFile>> GetRandomSongsAsync(int count = 50);
        Task<IReadOnlyList<MusicFile>> GetAlbumListAsync(int count = 50);
        Task<IReadOnlyList<MusicFile>> GetAlbumAsync(string albumId);
        Task<IReadOnlyList<MusicFile>> GetArtistAsync(string artistId);
    }

    /// <summary>
    /// Extended interface for YouTube-specific operations.
    /// Inspired by Echo Music's rich YouTube integration capabilities.
    /// Sessions that support YouTube can implement this for additional features.
    /// </summary>
    public interface IYouTubeSourceSession : IMusicSourceSession
    {
        /// <summary>
        /// Search with type filter (songs, videos, albums, artists, playlists).
        /// Inspired by Echo Music's filtered search.
        /// </summary>
        Task<IReadOnlyList<MusicFile>> SearchWithTypeAsync(string query, string resultType, int limit = 50);

        /// <summary>
        /// Get search suggestions for autocomplete.
        /// Inspired by Echo Music's getSearchSuggestions.
        /// </summary>
        Task<List<string>> GetSearchSuggestionsAsync(string query);

        /// <summary>
        /// Get tracks from a YouTube playlist.
        /// Inspired by Echo Music's playlist browsing.
        /// </summary>
        Task<IReadOnlyList<MusicFile>> GetPlaylistAsync(string playlistId, int limit = 100);

        /// <summary>
        /// Get related songs / radio for a video.
        /// Inspired by Echo Music's "next" endpoint for radio generation.
        /// </summary>
        Task<IReadOnlyList<MusicFile>> GetRadioAsync(string videoId, string playlistId = null);

        /// <summary>
        /// Search across all categories and return grouped results.
        /// Inspired by Echo Music's searchSummary.
        /// </summary>
        Task<Dictionary<string, List<MusicFile>>> SearchSummaryAsync(string query, int perTypeLimit = 5);
    }
}

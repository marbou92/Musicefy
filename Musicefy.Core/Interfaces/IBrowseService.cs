using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Musicefy.Core.Models;

namespace Musicefy.Core.Interfaces
{
    /// <summary>
    /// Unified browse API that aggregates content from all connected sources.
    /// Inspired by Echo Music's YouTube.home() and YouTube.explore() but
    /// adapted for Musicefy's multi-source architecture.
    /// </summary>
    public interface IBrowseService
    {
        /// <summary>Get the home page with sections from all sources</summary>
        Task<BrowsePage> GetHomePageAsync(CancellationToken ct = default);

        /// <summary>Get new releases and trending content from all sources</summary>
        Task<BrowsePage> GetExplorePageAsync(CancellationToken ct = default);

        /// <summary>Get artist detail from the appropriate source</summary>
        Task<ArtistInfo> GetArtistDetailAsync(string sourceId, string artistId, CancellationToken ct = default);

        /// <summary>Get album detail from the appropriate source</summary>
        Task<AlbumInfo> GetAlbumDetailAsync(string sourceId, string albumId, CancellationToken ct = default);

        /// <summary>Get random songs from all connected sources</summary>
        Task<List<MusicFile>> GetRandomSongsAsync(int count = 50, CancellationToken ct = default);

        /// <summary>Get albums from all connected sources</summary>
        Task<List<AlbumInfo>> GetAlbumsAsync(CancellationToken ct = default);

        /// <summary>Get artists from all connected sources</summary>
        Task<List<ArtistInfo>> GetArtistsAsync(CancellationToken ct = default);
    }
}

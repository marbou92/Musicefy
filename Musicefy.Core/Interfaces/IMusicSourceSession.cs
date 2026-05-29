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
}

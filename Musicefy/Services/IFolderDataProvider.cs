using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Musicefy.Core.Models;

namespace Musicefy.Services
{
    public interface IFolderDataProvider
    {
        Task<List<MusicFile>> GetAllTracksAsync(CancellationToken cancellationToken = default);
        Task<List<MusicFile>> GetTracksByDirectoryAsync(string directoryPath, CancellationToken cancellationToken = default);
    }
}

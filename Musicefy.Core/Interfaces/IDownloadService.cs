using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Musicefy.Core.Models;

namespace Musicefy.Core.Interfaces
{
    /// <summary>
    /// Interface for download operations
    /// </summary>
    public interface IDownloadService
    {
        /// <summary>
        /// Download a file with progress reporting
        /// </summary>
        Task<bool> DownloadFileAsync(
            string url,
            string fileName,
            Action<int, long> progress = null,
            CancellationToken cancellationToken = default,
            bool resume = false);

        /// <summary>
        /// Get the current cache size in bytes
        /// </summary>
        long GetCacheSize();

        /// <summary>
        /// Clear the download cache
        /// </summary>
        void ClearCache();

        /// <summary>
        /// Cancel a specific download
        /// </summary>
        void CancelDownload(string url);

        /// <summary>
        /// Get all downloaded files
        /// </summary>
        List<string> GetDownloadedFiles();

        /// <summary>
        /// Check if a file is downloaded
        /// </summary>
        bool IsFileDownloaded(string fileName);

        /// <summary>
        /// Delete a downloaded file
        /// </summary>
        bool DeleteDownloadedFile(string fileName);
    }
}
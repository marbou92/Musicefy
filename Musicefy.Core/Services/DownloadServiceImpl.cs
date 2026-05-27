using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Musicefy.Core.Interfaces;

namespace Musicefy.Core.Services
{
    /// <summary>
    /// Download service implementation with caching support
    /// </summary>
    public class DownloadServiceImpl : IDownloadService
    {
        private const long MaxDownloadSizeBytes = 500L * 1024L * 1024L; // 500 MB
        private readonly string _downloadsPath;
        private static readonly HttpClient _httpClient = new HttpClient();
        private readonly Dictionary<string, CancellationTokenSource> _activeDownloads = new Dictionary<string, CancellationTokenSource>();
        private long? _lastKnownCacheSize;
        private DateTime _lastCacheCheckTime;

        public DownloadServiceImpl()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            _downloadsPath = Path.Combine(appData, "Musicefy", "Downloads");

            if (!Directory.Exists(_downloadsPath))
                Directory.CreateDirectory(_downloadsPath);
        }

        public async Task<bool> DownloadFileAsync(
            string url,
            string fileName,
            Action<int, long> progress = null,
            CancellationToken cancellationToken = default,
            bool resume = false)
        {
            CancellationTokenSource downloadCts = null;
            try
            {
                downloadCts = new CancellationTokenSource();
                lock (_activeDownloads)
                    _activeDownloads[url] = downloadCts;

                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, downloadCts.Token);
                var combinedToken = linkedCts.Token;

                var userDownloadsPath = GetUserDownloadsPath();
                if (!Directory.Exists(userDownloadsPath))
                    Directory.CreateDirectory(userDownloadsPath);

                // Check cache limits
                var maxCacheSize = GetMaxCacheSize();
                var warningThreshold = GetWarningThreshold();

                long currentCacheSize = GetCacheSize();
                if (currentCacheSize >= maxCacheSize)
                {
                    LogMessage($"Cache limit reached ({maxCacheSize / (1024.0 * 1024.0 * 1024.0):F0} GB). Downloads blocked.");
                    return false;
                }
                else if (currentCacheSize > warningThreshold)
                {
                    LogMessage($"Cache size exceeds threshold ({warningThreshold / (1024.0 * 1024.0):F0} MB).");
                }

                var targetPath = Path.Combine(userDownloadsPath, fileName);
                long existingLength = resume && File.Exists(targetPath) ? new FileInfo(targetPath).Length : 0;

                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                if (resume && existingLength > 0)
                    request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(existingLength, null);

                using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, combinedToken);
                response.EnsureSuccessStatusCode();

                long? contentLength = response.Content.Headers.ContentLength;
                long totalLength = (contentLength ?? 0) + existingLength;

                if (totalLength > MaxDownloadSizeBytes)
                {
                    LogMessage("File larger than 500 MB. Download blocked.");
                    return false;
                }

                using var stream = await response.Content.ReadAsStreamAsync();
                using var fs = new FileStream(targetPath, resume ? FileMode.Append : FileMode.Create, FileAccess.Write);

                byte[] buffer = new byte[81920];
                int read;
                long totalBytes = existingLength;

                while ((read = await stream.ReadAsync(buffer, 0, buffer.Length, combinedToken)) > 0)
                {
                    totalBytes += read;

                    if (totalBytes > MaxDownloadSizeBytes)
                    {
                        fs.Close();
                        File.Delete(targetPath);
                        LogMessage("Download exceeded 500 MB limit.");
                        return false;
                    }

                    if (currentCacheSize + totalBytes > maxCacheSize)
                    {
                        fs.Close();
                        File.Delete(targetPath);
                        LogMessage("Cache limit reached. Download cancelled.");
                        return false;
                    }

                    await fs.WriteAsync(buffer, 0, read, combinedToken);

                    if (totalLength > 0)
                    {
                        int percent = (int)((double)totalBytes / totalLength * 100);
                        progress?.Invoke(percent, totalBytes);
                    }
                }

                LogMessage("Download completed.");
                return true;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            catch (Exception ex)
            {
                LogMessage($"Download failed: {ex.Message}");
                return false;
            }
            finally
            {
                if (downloadCts != null)
                {
                    lock (_activeDownloads)
                        _activeDownloads.Remove(url);
                    downloadCts.Dispose();
                }
            }
        }

        public long GetCacheSize()
        {
            if ((DateTime.UtcNow - _lastCacheCheckTime).TotalSeconds < 10 && _lastKnownCacheSize.HasValue)
                return _lastKnownCacheSize.Value;

            if (!Directory.Exists(_downloadsPath)) return 0;

            long size = 0;
            try
            {
                foreach (var file in Directory.GetFiles(_downloadsPath, "*", SearchOption.AllDirectories))
                    size += new FileInfo(file).Length;
            }
            catch
            {
                // Best-effort cache size calculation
            }
            _lastKnownCacheSize = size;
            _lastCacheCheckTime = DateTime.UtcNow;
            return size;
        }

        public void ClearCache()
        {
            try
            {
                if (Directory.Exists(_downloadsPath))
                {
                    foreach (var file in Directory.GetFiles(_downloadsPath))
                        File.Delete(file);
                    foreach (var dir in Directory.GetDirectories(_downloadsPath))
                        Directory.Delete(dir, true);
                }
                LogMessage("Cache cleared.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Clear cache failed: {ex.Message}");
            }
        }

        public void CancelDownload(string url)
        {
            lock (_activeDownloads)
            {
                if (_activeDownloads.TryGetValue(url, out var cts))
                {
                    cts.Cancel();
                    _activeDownloads.Remove(url);
                }
            }
        }

        public List<string> GetDownloadedFiles()
        {
            var files = new List<string>();
            if (Directory.Exists(_downloadsPath))
            {
                files.AddRange(Directory.GetFiles(_downloadsPath));
            }
            return files;
        }

        public bool IsFileDownloaded(string fileName)
        {
            var path = Path.Combine(_downloadsPath, fileName);
            return File.Exists(path);
        }

        public bool DeleteDownloadedFile(string fileName)
        {
            var path = Path.Combine(_downloadsPath, fileName);
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                    return true;
                }
            }
            catch
            {
                // File may be in use; deletion failed silently
            }
            return false;
        }

        #region Private Helpers

        private static string GetUserDownloadsPath()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Musicefy", "Downloads");
        }

        private static long GetMaxCacheSize()
        {
            // Default 10 GB
            return 10L * 1024L * 1024L * 1024L;
        }

        private static long GetWarningThreshold()
        {
            // Default 8 GB
            return 8L * 1024L * 1024L * 1024L;
        }

        private static void LogMessage(string message)
        {
            System.Diagnostics.Debug.WriteLine($"[DownloadService] {message}");
        }

        #endregion
    }
}

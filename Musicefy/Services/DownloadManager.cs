using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;

namespace Musicefy.Services
{
    public static class DownloadManager
    {
        private const long MaxDownloadSizeBytes = 500L * 1024L * 1024L; // 500 MB
        private const long MaxCacheSizeBytes = 2L * 1024L * 1024L * 1024L; // 2 GB
        private const long WarningThresholdBytes = 400L * 1024L * 1024L; // 400 MB

        /// <summary>
        /// Downloads a file with progress reporting, cancellation, and pause/resume support.
        /// </summary>
        /// <param name="url">File URL</param>
        /// <param name="fileName">Target file name</param>
        /// <param name="progress">Callback reporting percentage (0-100) and bytes downloaded</param>
        /// <param name="cancellationToken">Token to cancel download at any time</param>
        /// <param name="resume">If true, resume from partial file using HTTP Range</param>
        public static async Task<bool> DownloadFileAsync(
            string url,
            string fileName,
            Action<int, long>? progress = null,
            CancellationToken cancellationToken = default,
            bool resume = false)
        {
            try
            {
                string downloadsPath = Musicefy.Properties.Settings.Default.DownloadsPath;
                if (string.IsNullOrWhiteSpace(downloadsPath))
                {
                    string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                    downloadsPath = Path.Combine(appData, "Musicefy", "Downloads");
                }

                if (!Directory.Exists(downloadsPath))
                    Directory.CreateDirectory(downloadsPath);

                long currentCacheSize = GetDirectorySize(downloadsPath);
                if (currentCacheSize >= MaxCacheSizeBytes)
                {
                    ToastService.ShowToast("❌ Cache limit reached (2 GB). Downloads blocked until you clear space.", Brushes.OrangeRed);
                    return false;
                }
                else if (currentCacheSize > WarningThresholdBytes)
                {
                    ToastService.ShowToast("⚠ Cache size exceeds 400 MB. Consider clearing to free space.", Brushes.Goldenrod);
                }

                string targetPath = Path.Combine(downloadsPath, fileName);
                long existingLength = resume && File.Exists(targetPath) ? new FileInfo(targetPath).Length : 0;

                using (var client = new HttpClient())
                {
                    if (resume && existingLength > 0)
                        client.DefaultRequestHeaders.Range = new System.Net.Http.Headers.RangeHeaderValue(existingLength, null);

                    using (var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
                    {
                        response.EnsureSuccessStatusCode();

                        long? contentLength = response.Content.Headers.ContentLength;
                        long totalLength = (contentLength ?? 0) + existingLength;

                        if (Musicefy.Properties.Settings.Default.LimitDownloadSize &&
                            totalLength > MaxDownloadSizeBytes)
                        {
                            ToastService.ShowToast("❌ File larger than 500 MB. Download blocked.", Brushes.OrangeRed);
                            return false;
                        }

                        using (var stream = await response.Content.ReadAsStreamAsync(cancellationToken))
                        using (var fs = new FileStream(targetPath, resume ? FileMode.Append : FileMode.Create, FileAccess.Write))
                        {
                            byte[] buffer = new byte[81920];
                            int read;
                            long totalBytes = existingLength;

                            while ((read = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                            {
                                totalBytes += read;

                                if (cancellationToken.IsCancellationRequested)
                                {
                                    ToastService.ShowToast("⏸ Download paused.", Brushes.Goldenrod);
                                    return false;
                                }

                                // Enforce per-file limit
                                if (Musicefy.Properties.Settings.Default.LimitDownloadSize &&
                                    totalBytes > MaxDownloadSizeBytes)
                                {
                                    fs.Close();
                                    File.Delete(targetPath);
                                    ToastService.ShowToast("⚠ Download exceeded 500 MB limit and was cancelled.", Brushes.Goldenrod);
                                    return false;
                                }

                                // Enforce global cache limit
                                if (currentCacheSize + totalBytes > MaxCacheSizeBytes)
                                {
                                    fs.Close();
                                    File.Delete(targetPath);
                                    ToastService.ShowToast("❌ Cache limit reached (2 GB). Download cancelled.", Brushes.OrangeRed);
                                    return false;
                                }

                                await fs.WriteAsync(buffer, 0, read, cancellationToken);

                                // Report progress
                                if (totalLength > 0)
                                {
                                    int percent = (int)((double)totalBytes / totalLength * 100);
                                    progress?.Invoke(percent, totalBytes);
                                }
                                else
                                {
                                    progress?.Invoke(0, totalBytes);
                                }
                            }
                        }
                    }
                }

                if (Musicefy.Properties.Settings.Default.AutoClearCache)
                {
                    ClearCache(downloadsPath);
                    ToastService.ShowToast("🗑 Cache auto-cleared after download.", Brushes.Gray);
                }

                ToastService.ShowToast("✅ Download completed successfully.", Brushes.ForestGreen);
                return true;
            }
            catch (OperationCanceledException)
            {
                ToastService.ShowToast("⏸ Download paused.", Brushes.Goldenrod);
                return false;
            }
            catch (Exception ex)
            {
                ToastService.ShowToast($"❌ Download failed: {ex.Message}", Brushes.OrangeRed);
                return false;
            }
        }

        private static void ClearCache(string downloadsPath)
        {
            try
            {
                if (Directory.Exists(downloadsPath))
                {
                    foreach (var file in Directory.GetFiles(downloadsPath))
                        File.Delete(file);

                    foreach (var dir in Directory.GetDirectories(downloadsPath))
                        Directory.Delete(dir, true);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Auto-clear failed: {ex.Message}");
            }
        }

        private static long GetDirectorySize(string path)
        {
            if (!Directory.Exists(path)) return 0;

            long size = 0;
            try
            {
                foreach (var file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
                    size += new FileInfo(file).Length;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error calculating cache size: {ex.Message}");
            }
            return size;
        }
    }
}

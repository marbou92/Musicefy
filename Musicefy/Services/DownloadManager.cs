using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;

namespace Musicefy.Services
{
    public static class DownloadManager
    {
        private const long MaxDownloadSizeBytes = 500L * 1024L * 1024L; // 500 MB
        private const long MaxCacheSizeBytes = 2L * 1024L * 1024L * 1024L; // 2 GB
        private const long WarningThresholdBytes = 400L * 1024L * 1024L; // 400 MB

        public static async Task<bool> DownloadFileAsync(string url, string fileName)
        {
            try
            {
                // Resolve target path from settings
                string downloadsPath = Musicefy.Properties.Settings.Default.DownloadsPath;
                if (string.IsNullOrWhiteSpace(downloadsPath))
                {
                    string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                    downloadsPath = Path.Combine(appData, "Musicefy", "Downloads");
                }

                if (!Directory.Exists(downloadsPath))
                    Directory.CreateDirectory(downloadsPath);

                // Check current cache size before starting
                long currentCacheSize = GetDirectorySize(downloadsPath);
                if (currentCacheSize >= MaxCacheSizeBytes)
                {
                    MessageBox.Show("Cache limit reached (2 GB). Downloads are blocked until you clear space.",
                                    "Cache Limit",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Error);
                    return false;
                }
                else if (currentCacheSize > WarningThresholdBytes)
                {
                    MessageBox.Show("Warning: Cache size exceeds 400 MB. Consider clearing to free space.",
                                    "Cache Warning",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Warning);
                }

                string targetPath = Path.Combine(downloadsPath, fileName);

                using (var client = new HttpClient())
                {
                    var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                    response.EnsureSuccessStatusCode();

                    // Check Content-Length header
                    if (Musicefy.Properties.Settings.Default.LimitDownloadSize &&
                        response.Content.Headers.ContentLength.HasValue &&
                        response.Content.Headers.ContentLength.Value > MaxDownloadSizeBytes)
                    {
                        MessageBox.Show("This file is larger than 500 MB and cannot be downloaded.",
                                        "Download Blocked",
                                        MessageBoxButton.OK,
                                        MessageBoxImage.Warning);
                        return false;
                    }

                    using (var stream = await response.Content.ReadAsStreamAsync())
                    using (var fs = new FileStream(targetPath, FileMode.Create, FileAccess.Write))
                    {
                        byte[] buffer = new byte[81920];
                        int read;
                        long totalBytes = 0;

                        while ((read = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            totalBytes += read;

                            // Enforce per-file limit
                            if (Musicefy.Properties.Settings.Default.LimitDownloadSize &&
                                totalBytes > MaxDownloadSizeBytes)
                            {
                                fs.Close();
                                File.Delete(targetPath);

                                MessageBox.Show("Download exceeded 500 MB limit and was cancelled.",
                                                "Download Cancelled",
                                                MessageBoxButton.OK,
                                                MessageBoxImage.Warning);
                                return false;
                            }

                            // Enforce global cache limit
                            if (currentCacheSize + totalBytes > MaxCacheSizeBytes)
                            {
                                fs.Close();
                                File.Delete(targetPath);

                                MessageBox.Show("Cache limit reached (2 GB). Download cancelled.",
                                                "Cache Limit",
                                                MessageBoxButton.OK,
                                                MessageBoxImage.Error);
                                return false;
                            }

                            await fs.WriteAsync(buffer, 0, read);
                        }
                    }
                }

                // Auto-clear cache immediately after download if enabled
                if (Musicefy.Properties.Settings.Default.AutoClearCache)
                {
                    ClearCache(downloadsPath);
                }

                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Download failed: {ex.Message}",
                                "Error",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
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
                    {
                        File.Delete(file);
                    }
                    foreach (var dir in Directory.GetDirectories(downloadsPath))
                    {
                        Directory.Delete(dir, true);
                    }
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
                {
                    size += new FileInfo(file).Length;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error calculating cache size: {ex.Message}");
            }
            return size;
        }
    }
}

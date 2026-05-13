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
    }
}

using System;
using System.Linq;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Extensions.DependencyInjection;
using Musicefy.Core.Interfaces;

namespace Musicefy.Converters
{
    public class PathToImageConverter : IValueConverter
    {
        private static readonly ConcurrentDictionary<string, BitmapImage> _cache
            = new ConcurrentDictionary<string, BitmapImage>(StringComparer.OrdinalIgnoreCase);

        private static readonly ConcurrentDictionary<string, byte> _inFlight
            = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);

        private static readonly ConcurrentQueue<string> _cacheOrder = new ConcurrentQueue<string>();

        private static readonly SemaphoreSlim _throttle = new SemaphoreSlim(4, 4);

        private static readonly ImageSource _fallbackImage;
        private static IStreamingSourceManager _sourceManager;

        private const int MaxCacheEntries = 800;
        private const int ListDecodeWidth  = 48;
        private const int GridDecodeWidth  = 200;

        // WebP byte signature: "RIFF" at offset 0, "WEBP" at offset 8
        private static readonly byte[] WebP_Riff = { 0x52, 0x49, 0x46, 0x46 }; // "RIFF"
        private static readonly byte[] WebP_Webp = { 0x57, 0x45, 0x42, 0x50 }; // "WEBP"

        // AVIF byte signature: "ftyp" at offset 4, "avif" or "avis" at offset 8
        private static readonly byte[] Avif_Ftyp = { 0x66, 0x74, 0x79, 0x70 }; // "ftyp"

        static PathToImageConverter()
        {
            _fallbackImage = CreateFallbackIcon();
            try { _sourceManager = App.Services.GetService<IStreamingSourceManager>(); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[PathToImageConverter] Failed to resolve IStreamingSourceManager: {ex.Message}"); }
        }

        private static ImageSource CreateFallbackIcon()
        {
            var geometry = Geometry.Parse(
                "M9 6.5C9 7.33 8.33 8 7.5 8S6 7.33 6 6.5 6.67 5 7.5 5 9 5.67 9 6.5zM16 7h-2v.82c-.42-.52-1.07-.82-1.82-.82C11.01 7 10 8.01 10 9.25c0 1.24 1.01 2.25 2.25 2.25.68 0 1.28-.3 1.7-.78.02.01.04.02.05.03V14H9V6.5C9 5.12 7.88 4 6.5 4S4 5.12 4 6.5 5.12 9 6.5 9c.38 0 .74-.07 1.07-.2.02.06.04.13.06.2h.01C7.6 10.17 7 11.27 7 12.5c0 2.48 2.02 4.5 4.5 4.5s4.5-2.02 4.5-4.5c0-1.23-.6-2.33-1.64-3 .01-.01.03-.02.04-.03.33.13.69.2 1.07.2 1.38 0 2.5-1.12 2.5-2.5S17.38 4 16 4s-2.5 1.12-2.5 2.5c0 .09.01.18.02.27.13.15.28.28.45.4L16 7.78V9h-1v3.55c-.59-.34-1.27-.55-2-.55-2.21 0-4 1.79-4 4s1.79 4 4 4 4-1.79 4-4V7h-1z");
            var penBrush = new SolidColorBrush(Color.FromArgb(60, 128, 128, 128));
            penBrush.Freeze();
            var pen = new Pen(penBrush, 1.2);
            pen.Freeze();
            return new DrawingImage(new GeometryDrawing
            {
                Geometry = geometry,
                Pen = pen,
            });
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string path = value as string;
            if (string.IsNullOrEmpty(path))
                return _fallbackImage;

            // Pre-process URLs to request JPEG format from YouTube/Google CDNs
            path = SanitizeImageUrl(path);

            if (_cache.TryGetValue(path, out var cached))
                return cached;

            if (_inFlight.TryAdd(path, 0))
            {
                bool isGrid = string.Equals(parameter?.ToString(), "grid",
                    StringComparison.OrdinalIgnoreCase);
                int decodeWidth = isGrid ? GridDecodeWidth : ListDecodeWidth;
                _ = LoadAsync(path, decodeWidth);
            }

            return _fallbackImage;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Transforms YouTube/Google CDN thumbnail URLs to request JPEG format
        /// instead of WebP, which WPF cannot decode natively.
        /// </summary>
        private static string SanitizeImageUrl(string url)
        {
            if (string.IsNullOrEmpty(url) || !url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                return url;

            try
            {
                // YouTube i.ytimg.com — convert /vi_webp/ paths to /vi/ with .jpg extension
                // e.g. https://i.ytimg.com/vi_webp/VIDEO_ID/mqdefault.webp
                //   → https://i.ytimg.com/vi/VIDEO_ID/mqdefault.jpg
                if (url.Contains("i.ytimg.com", StringComparison.OrdinalIgnoreCase))
                {
                    url = System.Text.RegularExpressions.Regex.Replace(url, "/vi_webp/", "/vi/", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    if (url.EndsWith(".webp", StringComparison.OrdinalIgnoreCase))
                        url = url.Substring(0, url.Length - 5) + ".jpg";
                    return url;
                }

                // Google CDN (lh3.googleusercontent.com, yt3.ggpht.com, etc.)
                // These URLs use suffix parameters like =w120-h120-l90-rj (JPEG) or =w120-h120-l90-rw (WebP)
                // Replace -rw, -rw-p, or similar WebP suffixes with -rj (JPEG)
                if (url.Contains("googleusercontent.com", StringComparison.OrdinalIgnoreCase) ||
                    url.Contains("ggpht.com", StringComparison.OrdinalIgnoreCase))
                {
                    // Replace WebP-indicating suffixes with JPEG suffix
                    // Patterns: -rw-p, -rw, -no-rw, -no-rw-p
                    // Target: -rj (JPEG)
                    if (url.Contains("-rw", StringComparison.OrdinalIgnoreCase))
                    {
                        url = System.Text.RegularExpressions.Regex.Replace(
                            url, @"-rw(?:-p)?(?=[-\s/&?]|$)", "-rj",
                            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    }

                    // If URL ends with =sN-c-k-c0x00ffffff-no-rw or similar, fix it
                    if (url.Contains("-no-rw", StringComparison.OrdinalIgnoreCase))
                    {
                        url = System.Text.RegularExpressions.Regex.Replace(url, "-no-rw", "-no-rj", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    }

                    return url;
                }

                // YouTube img.youtube.com — same as ytimg
                if (url.Contains("youtube.com", StringComparison.OrdinalIgnoreCase) && url.EndsWith(".webp", StringComparison.OrdinalIgnoreCase))
                {
                    url = url.Substring(0, url.Length - 5) + ".jpg";
                    return url;
                }
            }
            catch
            {
                // If URL manipulation fails, just use the original URL
            }

            return url;
        }

        /// <summary>
        /// Checks if the byte array represents an image format that WPF cannot decode natively,
        /// such as WebP or AVIF.
        /// </summary>
        private static bool IsUnsupportedImageFormat(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 12)
                return false;

            // Check for WebP: bytes 0-3 = "RIFF", bytes 8-11 = "WEBP"
            bool isRiff = bytes[0] == WebP_Riff[0] && bytes[1] == WebP_Riff[1]
                       && bytes[2] == WebP_Riff[2] && bytes[3] == WebP_Riff[3];
            bool isWebp = bytes[8] == WebP_Webp[0] && bytes[9] == WebP_Webp[1]
                       && bytes[10] == WebP_Webp[2] && bytes[11] == WebP_Webp[3];
            if (isRiff && isWebp)
                return true;

            // Check for AVIF: bytes 4-7 = "ftyp", bytes 8-11 = "avif" or "avis"
            if (bytes.Length >= 12)
            {
                bool isFtyp = bytes[4] == Avif_Ftyp[0] && bytes[5] == Avif_Ftyp[1]
                           && bytes[6] == Avif_Ftyp[2] && bytes[7] == Avif_Ftyp[3];
                if (isFtyp)
                {
                    string brand = System.Text.Encoding.ASCII.GetString(bytes, 8, 4);
                    if (brand.Equals("avif", StringComparison.OrdinalIgnoreCase) ||
                        brand.Equals("avis", StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }

            return false;
        }

        private static async System.Threading.Tasks.Task LoadAsync(string coverPath, int decodeWidth)
        {
            try
            {
                await _throttle.WaitAsync();
            }
            catch (ObjectDisposedException)
            {
                _inFlight.TryRemove(coverPath, out _);
                return;
            }

            BitmapImage bitmap = null;
            try
            {
                // Streaming source cover ID — fetches bytes asynchronously, creates BitmapImage on UI thread
                if (coverPath.Contains(":cover:"))
                {
                    bitmap = await LoadStreamingCoverAsync(coverPath, decodeWidth);
                }
                else if (coverPath.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                {
                    // Download bytes on BG thread with JPEG-preferencing Accept header
                    var bytes = await System.Threading.Tasks.Task.Run(() =>
                    {
                        try
                        {
                            using var client = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(8) };
                            // Prefer JPEG/PNG over WebP/AVIF since WPF cannot decode those natively
                            client.DefaultRequestHeaders.Accept.ParseAdd("image/jpeg, image/png;q=0.9, image/*;q=0.5");
                            // Some CDN servers use User-Agent sniffing to serve WebP to modern browsers
                            client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/4.0 (compatible; MSIE 7.0; Windows NT 10.0)");
                            var response = client.GetAsync(coverPath).Result;
                            response.EnsureSuccessStatusCode();
                            return response.Content.ReadAsByteArrayAsync().Result;
                        }
                        catch { return null; }
                    });

                    if (bytes != null && bytes.Length > 0)
                    {
                        // Check for unsupported formats (WebP, AVIF) before attempting decode
                        if (IsUnsupportedImageFormat(bytes))
                        {
                            System.Diagnostics.Debug.WriteLine(
                                $"[PathToImageConverter] Skipping unsupported image format at {coverPath}");

                            // Try to re-download with a modified URL that forces JPEG
                            var jpegBytes = await TryFetchJpegFallbackAsync(coverPath);
                            if (jpegBytes != null && jpegBytes.Length > 0 && !IsUnsupportedImageFormat(jpegBytes))
                                bitmap = await CreateBitmapOnUiThread(jpegBytes, decodeWidth);
                            // If JPEG fallback also fails or returns unsupported format, bitmap stays null → fallback shown
                        }
                        else
                        {
                            bitmap = await CreateBitmapOnUiThread(bytes, decodeWidth);
                        }
                    }
                }
                else
                {
                    // Local file — read bytes on BG thread, create BitmapImage on UI thread
                    var bytes = await System.Threading.Tasks.Task.Run(() =>
                    {
                        try { return System.IO.File.ReadAllBytes(coverPath); }
                        catch { return null; }
                    });

                    if (bytes != null && bytes.Length > 0)
                    {
                        // Local files shouldn't be WebP, but check anyway
                        if (IsUnsupportedImageFormat(bytes))
                        {
                            System.Diagnostics.Debug.WriteLine(
                                $"[PathToImageConverter] Local file {coverPath} is in unsupported format (WebP/AVIF)");
                        }
                        else
                        {
                            bitmap = await CreateBitmapOnUiThread(bytes, decodeWidth);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Swallow all exceptions to prevent unobserved Task exceptions
                // The fallback image will remain displayed
                System.Diagnostics.Debug.WriteLine(
                    $"[PathToImageConverter] Failed to load image '{coverPath}': {ex.Message}");
            }
            finally
            {
                try { _throttle.Release(); }
                catch (ObjectDisposedException) { }
            }

            if (bitmap != null)
            {
                _cache[coverPath] = bitmap;
                _cacheOrder.Enqueue(coverPath);
                EvictIfNeeded();
            }

            _inFlight.TryRemove(coverPath, out _);

            try
            {
                Application.Current?.Dispatcher.BeginInvoke(
                    (Action)(() => RefreshImages(coverPath)),
                    System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            }
            catch { /* Dispatcher may be shutdown during app exit */ }
        }

        /// <summary>
        /// When a URL returns WebP despite our Accept header, attempt to re-fetch
        /// with aggressive URL manipulation to force JPEG from YouTube/Google CDNs.
        /// </summary>
        private static async System.Threading.Tasks.Task<byte[]> TryFetchJpegFallbackAsync(string originalUrl)
        {
            // For YouTube/Google CDN URLs, try harder to get JPEG
            if (!originalUrl.Contains("ytimg.com", StringComparison.OrdinalIgnoreCase) &&
                !originalUrl.Contains("googleusercontent.com", StringComparison.OrdinalIgnoreCase) &&
                !originalUrl.Contains("ggpht.com", StringComparison.OrdinalIgnoreCase) &&
                !originalUrl.Contains("youtube.com", StringComparison.OrdinalIgnoreCase))
            {
                return null; // No known JPEG fallback strategy for other CDNs
            }

            string jpegUrl = originalUrl;

            // Strategy 1: Replace .webp extension with .jpg
            if (jpegUrl.EndsWith(".webp", StringComparison.OrdinalIgnoreCase))
                jpegUrl = jpegUrl.Substring(0, jpegUrl.Length - 5) + ".jpg";

            // Strategy 2: For ytimg.com, force /vi/ path and .jpg extension
            if (jpegUrl.Contains("ytimg.com", StringComparison.OrdinalIgnoreCase))
            {
                jpegUrl = System.Text.RegularExpressions.Regex.Replace(jpegUrl, "/vi_webp/", "/vi/", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (!jpegUrl.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) &&
                    !jpegUrl.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                {
                    // Extract video ID and construct a standard JPEG thumbnail URL
                    var match = System.Text.RegularExpressions.Regex.Match(
                        jpegUrl, @"/vi/([\w-]{11})/");
                    if (match.Success)
                    {
                        string videoId = match.Groups[1].Value;
                        jpegUrl = $"https://i.ytimg.com/vi/{videoId}/mqdefault.jpg";
                    }
                }
            }

            // Strategy 3: For Google CDN URLs, force -rj suffix (JPEG)
            if (jpegUrl.Contains("googleusercontent.com", StringComparison.OrdinalIgnoreCase) ||
                jpegUrl.Contains("ggpht.com", StringComparison.OrdinalIgnoreCase))
            {
                // Remove any trailing format parameters and add -rj
                if (!jpegUrl.Contains("-rj", StringComparison.OrdinalIgnoreCase))
                {
                    // Append -rj to the URL's size parameter
                    var lastEq = jpegUrl.LastIndexOf('=');
                    if (lastEq >= 0)
                    {
                        jpegUrl = jpegUrl.Substring(0, lastEq + 1) + "s0-rj";
                    }
                }
            }

            // If the URL was modified, try fetching the JPEG version
            if (jpegUrl == originalUrl)
                return null;

            return await System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    using var client = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                    client.DefaultRequestHeaders.Accept.ParseAdd("image/jpeg, image/png;q=0.9");
                    client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/4.0 (compatible; MSIE 7.0; Windows NT 10.0)");
                    var response = client.GetAsync(jpegUrl).Result;
                    response.EnsureSuccessStatusCode();
                    return response.Content.ReadAsByteArrayAsync().Result;
                }
                catch { return null; }
            });
        }

        private static System.Threading.Tasks.Task<BitmapImage> CreateBitmapOnUiThread(byte[] bytes, int decodeWidth)
        {
            var tcs = new System.Threading.Tasks.TaskCompletionSource<BitmapImage>();

            try
            {
                Application.Current?.Dispatcher.BeginInvoke((Action)(() =>
                {
                    try
                    {
                        var bmp = new BitmapImage();
                        using (var ms = new MemoryStream(bytes))
                        {
                            bmp.BeginInit();
                            bmp.CacheOption = BitmapCacheOption.OnLoad;
                            bmp.DecodePixelWidth = decodeWidth;
                            bmp.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
                            bmp.StreamSource = ms;
                            bmp.EndInit();
                        }
                        bmp.Freeze();
                        tcs.SetResult(bmp);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"[PathToImageConverter] BitmapImage creation failed: {ex.Message}");
                        tcs.SetResult(null); // Return null instead of throwing to prevent unobserved exceptions
                    }
                }), System.Windows.Threading.DispatcherPriority.Normal);
            }
            catch (Exception ex)
            {
                // Dispatcher might be shutdown
                System.Diagnostics.Debug.WriteLine(
                    $"[PathToImageConverter] Failed to dispatch bitmap creation: {ex.Message}");
                tcs.SetResult(null);
            }

            return tcs.Task;
        }

        private static void EvictIfNeeded()
        {
            if (_cache.Count <= MaxCacheEntries) return;
            int toRemove = MaxCacheEntries / 5;
            for (int i = 0; i < toRemove; i++)
            {
                if (_cacheOrder.TryDequeue(out var key))
                    _cache.TryRemove(key, out _);
            }
        }

        private static void RefreshImages(string coverPath)
        {
            if (Application.Current == null) return;
            foreach (Window window in Application.Current.Windows)
            {
                if (!window.IsVisible) continue;
                var content = window.Content as FrameworkElement;
                if (content != null)
                    WalkAndUpdate(content, coverPath, 0);
            }
        }

        private static void WalkAndUpdate(DependencyObject parent, string coverPath, int depth)
        {
            if (depth > 20) return;
            int children = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < children; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is Image img && (ReferenceEquals(img.Source, _fallbackImage) || img.Source == null))
                {
                    var dc = (child as FrameworkElement)?.DataContext as Musicefy.Core.Models.MusicFile;
                    if (dc?.CoverPath != null &&
                        string.Equals(dc.CoverPath, coverPath, StringComparison.OrdinalIgnoreCase))
                    {
                        if (_cache.TryGetValue(coverPath, out var bmp))
                            img.Source = bmp;
                    }
                }
                if (child is System.Windows.Controls.Panel || child is System.Windows.Controls.ContentPresenter ||
                    child is System.Windows.Controls.ItemsPresenter || child is System.Windows.Controls.Border)
                    WalkAndUpdate(child, coverPath, depth + 1);
            }
        }

        private static async System.Threading.Tasks.Task<BitmapImage> LoadStreamingCoverAsync(string coverId, int decodeWidth)
        {
            if (_sourceManager == null) return null;

            try
            {
                var bytes = await _sourceManager.ResolveCoverArtAsync(coverId);

                if (bytes == null || bytes.Length == 0) return null;

                // Check for unsupported format before attempting decode
                if (IsUnsupportedImageFormat(bytes))
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[PathToImageConverter] Streaming cover {coverId} is in unsupported format (WebP/AVIF)");
                    return null;
                }

                return await CreateBitmapOnUiThread(bytes, decodeWidth);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[PathToImageConverter] Failed to load streaming cover '{coverId}': {ex.Message}");
                return null;
            }
        }

    }
}

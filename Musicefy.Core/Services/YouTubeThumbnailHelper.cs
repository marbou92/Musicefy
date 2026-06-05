using System;
using System.Linq;

namespace Musicefy.Core.Services
{
    /// <summary>
    /// Utility for picking the best YouTube thumbnail URL based on target render size.
    ///
    /// YouTube stores thumbnails at multiple resolutions for each video:
    ///   maxresdefault  — 1280×720 (not always available)
    ///   sddefault      — 640×480
    ///   hqdefault      — 480×360
    ///   mqdefault      — 320×180
    ///   default        — 120×90
    ///
    /// Echo Music's resize() utility picks the best thumbnail with a fallback chain.
    /// This helper replicates that behavior: pick the smallest size that still looks
    /// sharp at the target render resolution, with a fallback chain for unavailable sizes.
    ///
    /// Also handles Google CDN URLs (artist avatars, playlist covers) by adjusting
    /// the =sN size parameter.
    /// </summary>
    public static class YouTubeThumbnailHelper
    {
        /// <summary>
        /// Target render size categories — determines which thumbnail resolution to request.
        /// </summary>
        public enum ThumbnailSize
        {
            /// <summary>Small list items (~48px render, ~96px decode for HiDPI)</summary>
            List,
            /// <summary>Grid cards (~160px render, ~320px decode for HiDPI)</summary>
            Grid,
            /// <summary>Album/artist hero view (~340px render, ~800px decode for HiDPI)</summary>
            Hero,
            /// <summary>Full-screen display (~720px+ render, 1280px decode for HiDPI)</summary>
            Full
        }

        /// <summary>
        /// Returns the best thumbnail URL for the given original URL and target render size.
        /// If the URL is not a YouTube/Google CDN URL, returns it unchanged.
        /// </summary>
        /// <param name="originalUrl">The thumbnail URL from the API</param>
        /// <param name="targetSize">The render context (list, grid, hero, full)</param>
        /// <returns>The best thumbnail URL for the target size</returns>
        public static string GetBestUrl(string originalUrl, ThumbnailSize targetSize)
        {
            if (string.IsNullOrEmpty(originalUrl) || !originalUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                return originalUrl;

            // YouTube i.ytimg.com thumbnails
            if (originalUrl.Contains("i.ytimg.com", StringComparison.OrdinalIgnoreCase))
                return GetBestYtimgUrl(originalUrl, targetSize);

            // Google CDN (artist avatars, playlist covers)
            if (originalUrl.Contains("googleusercontent.com", StringComparison.OrdinalIgnoreCase) ||
                originalUrl.Contains("ggpht.com", StringComparison.OrdinalIgnoreCase))
                return GetBestGoogleCdnUrl(originalUrl, targetSize);

            // Not a known CDN — return as-is
            return originalUrl;
        }

        /// <summary>
        /// Returns the best thumbnail URL for a YouTube video given its video ID.
        /// This is the preferred method when you have the video ID, as it constructs
        /// the URL from scratch rather than trying to modify an existing URL.
        /// </summary>
        /// <param name="videoId">YouTube video ID (11 characters)</param>
        /// <param name="targetSize">The render context</param>
        /// <returns>A thumbnail URL appropriate for the target size</returns>
        public static string GetUrlFromVideoId(string videoId, ThumbnailSize targetSize)
        {
            if (string.IsNullOrEmpty(videoId))
                return null;

            // Pick the best known-available size based on render context
            // mqdefault is always available; maxresdefault is often missing
            string quality = targetSize switch
            {
                ThumbnailSize.List => "default",     // 120×90 — fine for 48px list items
                ThumbnailSize.Grid => "mqdefault",   // 320×180 — looks sharp at 160px
                ThumbnailSize.Hero => "hqdefault",   // 480×360 — decent for 340px hero
                ThumbnailSize.Full => "sddefault",   // 640×480 — good for full-screen
                _ => "mqdefault"
            };

            return $"https://i.ytimg.com/vi/{videoId}/{quality}.jpg";
        }

        /// <summary>
        /// Returns a fallback chain of thumbnail URLs for progressive loading.
        /// Load the first URL; if it fails (404), try the next, etc.
        /// Ordered from highest quality to lowest.
        /// </summary>
        public static string[] GetFallbackChain(string videoId)
        {
            if (string.IsNullOrEmpty(videoId))
                return Array.Empty<string>();

            return new[]
            {
                $"https://i.ytimg.com/vi/{videoId}/maxresdefault.jpg",
                $"https://i.ytimg.com/vi/{videoId}/sddefault.jpg",
                $"https://i.ytimg.com/vi/{videoId}/hqdefault.jpg",
                $"https://i.ytimg.com/vi/{videoId}/mqdefault.jpg",
                $"https://i.ytimg.com/vi/{videoId}/default.jpg",
            };
        }

        /// <summary>
        /// Extracts a YouTube video ID from a ytimg.com URL.
        /// Returns null if the URL doesn't contain a valid video ID.
        /// </summary>
        public static string ExtractVideoId(string url)
        {
            if (string.IsNullOrEmpty(url))
                return null;

            // Match /vi/VIDEO_ID/ or /vi_webp/VIDEO_ID/ patterns
            var match = System.Text.RegularExpressions.Regex.Match(
                url, @"/vi(?:_webp)?/([\w-]{11})/", 
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            if (match.Success)
                return match.Groups[1].Value;

            return null;
        }

        private static string GetBestYtimgUrl(string url, ThumbnailSize targetSize)
        {
            // Try to extract video ID — if found, construct clean URL from scratch
            var videoId = ExtractVideoId(url);
            if (!string.IsNullOrEmpty(videoId))
                return GetUrlFromVideoId(videoId, targetSize);

            // Fallback: try to replace the quality suffix in the existing URL
            string targetQuality = targetSize switch
            {
                ThumbnailSize.List => "default",
                ThumbnailSize.Grid => "mqdefault",
                ThumbnailSize.Hero => "hqdefault",
                ThumbnailSize.Full => "sddefault",
                _ => "mqdefault"
            };

            // Replace existing quality name with target quality
            var qualities = new[] { "maxresdefault", "sddefault", "hqdefault", "mqdefault", "default" };
            foreach (var q in qualities)
            {
                if (url.Contains(q, StringComparison.OrdinalIgnoreCase))
                {
                    url = System.Text.RegularExpressions.Regex.Replace(
                        url, System.Text.RegularExpressions.Regex.Escape(q),
                        targetQuality,
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    break;
                }
            }

            // Ensure .jpg extension (not .webp)
            if (url.EndsWith(".webp", StringComparison.OrdinalIgnoreCase))
                url = url.Substring(0, url.Length - 5) + ".jpg";

            // Ensure /vi/ path (not /vi_webp/)
            url = System.Text.RegularExpressions.Regex.Replace(
                url, "/vi_webp/", "/vi/",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            return url;
        }

        private static string GetBestGoogleCdnUrl(string url, ThumbnailSize targetSize)
        {
            // Google CDN URLs have size parameters like =w120-h120-l90-rj or =s0-rj
            // The =sN or =wN-hN parameter controls the output size
            // -rw = WebP, -rj = JPEG

            int targetPixelSize = targetSize switch
            {
                ThumbnailSize.List => 96,
                ThumbnailSize.Grid => 320,
                ThumbnailSize.Hero => 800,
                ThumbnailSize.Full => 1280,
                _ => 320
            };

            string sizeParam = $"=w{targetPixelSize}-h{targetPixelSize}-l90-rj";

            // Find the last = in the URL (this is the size parameter)
            int lastEq = url.LastIndexOf('=');
            if (lastEq >= 0)
            {
                // Check if there's a ? before the = (query string param, not size param)
                int lastQuestion = url.LastIndexOf('?');
                if (lastQuestion < lastEq)
                {
                    // This looks like a CDN size parameter
                    url = url.Substring(0, lastEq) + sizeParam;
                }
                else
                {
                    // No size parameter found — append one
                    url = url + sizeParam;
                }
            }
            else
            {
                // No = found — append size parameter
                url = url + sizeParam;
            }

            return url;
        }
    }
}

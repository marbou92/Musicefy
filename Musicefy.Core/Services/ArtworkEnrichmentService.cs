using System;
using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Musicefy.Core.Services
{
    /// <summary>
    /// Background service that enriches local music files with high-resolution artwork
    /// from MusicBrainz Cover Art Archive and Last.fm.
    ///
    /// Workflow:
    ///   1. Read MBReleaseID from ID3 tags (if present)
    ///   2. Query Cover Art Archive: https://coverartarchive.org/release/{mbid}/front-500
    ///   3. Fall back to Last.fm album.getInfo API if no MBReleaseID
    ///   4. Save the downloaded art to a local cache folder and update the DB
    ///
    /// This service runs as a low-priority background operation after library scan,
    /// never blocking the UI thread. It processes items one at a time with configurable
    /// delay between requests to respect rate limits.
    /// </summary>
    public class ArtworkEnrichmentService
    {
        private readonly HttpClient _httpClient;
        private readonly string _cacheDir;
        private readonly TimeSpan _requestDelay;

        /// <summary>
        /// Fired when a single artwork enrichment completes (success or failure).
        /// </summary>
        public event EventHandler<ArtworkEnrichedEventArgs> ArtworkEnriched;

        /// <summary>
        /// Fired when the entire enrichment batch completes.
        /// </summary>
        public event EventHandler<BatchEnrichedEventArgs> BatchCompleted;

        public ArtworkEnrichmentService(string cacheDirectory = null, TimeSpan? requestDelay = null)
        {
            _cacheDir = cacheDirectory ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Musicefy", "ArtworkCache");

            _requestDelay = requestDelay ?? TimeSpan.FromMilliseconds(500);

            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Musicefy/1.0 (https://github.com/marbou92/Musicefy)");
            _httpClient.Timeout = TimeSpan.FromSeconds(10);
        }

        /// <summary>
        /// Attempts to enrich a single album's artwork using MusicBrainz Cover Art Archive.
        /// Requires a valid MusicBrainz Release ID (MBID).
        /// </summary>
        /// <param name="mbReleaseId">MusicBrainz Release ID (UUID format)</param>
        /// <param name="albumName">Album name for cache filename</param>
        /// <param name="artistName">Artist name for cache filename</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>Path to the downloaded artwork file, or null if not found</returns>
        public async Task<string> EnrichFromMusicBrainzAsync(
            string mbReleaseId,
            string albumName,
            string artistName,
            CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(mbReleaseId))
                return null;

            try
            {
                // Check cache first
                string cacheFile = GetCachePath(albumName, artistName, "mb", mbReleaseId);
                if (File.Exists(cacheFile))
                    return cacheFile;

                // Query Cover Art Archive for the front cover at 500px
                var url = $"https://coverartarchive.org/release/{mbReleaseId}/front-500";

                var response = await _httpClient.GetAsync(url, ct);
                if (response.IsSuccessStatusCode)
                {
                    var bytes = await response.Content.ReadAsByteArrayAsync();
                    if (bytes?.Length > 1000) // Sanity check: real images are > 1KB
                    {
                        Directory.CreateDirectory(_cacheDir);
                        File.WriteAllBytes(cacheFile, bytes);
                        return cacheFile;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[ArtworkEnrichment] MusicBrainz failed for {albumName}: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Attempts to enrich album artwork using Last.fm album.getInfo API.
        /// Used as a fallback when MusicBrainz ID is not available.
        /// Requires a Last.fm API key.
        /// </summary>
        /// <param name="apiKey">Last.fm API key</param>
        /// <param name="artistName">Artist name</param>
        /// <param name="albumName">Album name</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>Path to the downloaded artwork file, or null if not found</returns>
        public async Task<string> EnrichFromLastFmAsync(
            string apiKey,
            string artistName,
            string albumName,
            CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(artistName) || string.IsNullOrEmpty(albumName))
                return null;

            try
            {
                // Check cache first
                string cacheKey = $"{artistName}_{albumName}".GetHashCode().ToString("x8");
                string cacheFile = GetCachePath(albumName, artistName, "lfm", cacheKey);
                if (File.Exists(cacheFile))
                    return cacheFile;

                // Query Last.fm API
                var url = $"https://ws.audioscrobbler.com/2.0/?method=album.getinfo" +
                          $"&api_key={apiKey}&artist={Uri.EscapeDataString(artistName)}" +
                          $"&album={Uri.EscapeDataString(albumName)}&format=json";

                var response = await _httpClient.GetAsync(url, ct);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();

                    // Extract the largest image URL from the JSON response
                    // Last.fm returns image URLs in order: small, medium, large, extralarge, mega
                    var imageUrl = ExtractLargestImageUrl(json);
                    if (!string.IsNullOrEmpty(imageUrl))
                    {
                        await Task.Delay(_requestDelay, ct); // Rate limit

                        var imgResponse = await _httpClient.GetAsync(imageUrl, ct);
                        if (imgResponse.IsSuccessStatusCode)
                        {
                            var bytes = await imgResponse.Content.ReadAsByteArrayAsync();
                            if (bytes?.Length > 1000)
                            {
                                Directory.CreateDirectory(_cacheDir);
                                File.WriteAllBytes(cacheFile, bytes);
                                return cacheFile;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[ArtworkEnrichment] Last.fm failed for {albumName}: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Enriches artwork for a batch of items. Runs in the background with
        /// configurable delay between requests to respect rate limits.
        /// </summary>
        public async Task EnrichBatchAsync(
            System.Collections.Generic.List<EnrichmentItem> items,
            string lastFmApiKey = null,
            CancellationToken ct = default)
        {
            int successCount = 0;
            int failCount = 0;

            foreach (var item in items)
            {
                if (ct.IsCancellationRequested) break;

                string result = null;

                // Try MusicBrainz first (more reliable, higher quality)
                if (!string.IsNullOrEmpty(item.MbReleaseId))
                {
                    result = await EnrichFromMusicBrainzAsync(
                        item.MbReleaseId, item.AlbumName, item.ArtistName, ct);
                }

                // Fallback to Last.fm
                if (result == null && !string.IsNullOrEmpty(lastFmApiKey))
                {
                    result = await EnrichFromLastFmAsync(
                        lastFmApiKey, item.ArtistName, item.AlbumName, ct);
                }

                if (result != null)
                {
                    successCount++;
                    ArtworkEnriched?.Invoke(this, new ArtworkEnrichedEventArgs
                    {
                        Item = item,
                        ArtworkPath = result,
                        Success = true
                    });
                }
                else
                {
                    failCount++;
                    ArtworkEnriched?.Invoke(this, new ArtworkEnrichedEventArgs
                    {
                        Item = item,
                        ArtworkPath = null,
                        Success = false
                    });
                }

                // Rate limiting delay between requests
                await Task.Delay(_requestDelay, ct);
            }

            BatchCompleted?.Invoke(this, new BatchEnrichedEventArgs
            {
                TotalProcessed = items.Count,
                SuccessCount = successCount,
                FailCount = failCount
            });
        }

        /// <summary>
        /// Extracts the largest image URL from Last.fm JSON response.
        /// Last.fm returns images in a nested array with size attributes.
        /// </summary>
        private string ExtractLargestImageUrl(string json)
        {
            // Priority order: mega > extralarge > large
            var sizeOrder = new[] { "mega", "extralarge", "large" };
            foreach (var size in sizeOrder)
            {
                var pattern = $"\"size\":\"{size}\".*?\"#text\":\"(https?://[^\"]+)\"";
                var match = Regex.Match(json, pattern, RegexOptions.IgnoreCase);
                if (match.Success)
                    return match.Groups[1].Value;
            }

            // Fallback: find any image URL in the JSON
            var urlMatch = Regex.Match(json, "\"#text\":\"(https?://[^\"]+)\"");
            return urlMatch.Success ? urlMatch.Groups[1].Value : null;
        }

        private string GetCachePath(string albumName, string artistName, string source, string id)
        {
            // Sanitize filename
            var safeName = $"{artistName}_{albumName}_{source}_{id}"
                .Replace(" ", "_")
                .Trim(Path.GetInvalidFileNameChars());
            return Path.Combine(_cacheDir, $"{safeName}.jpg");
        }
    }

    /// <summary>
    /// Represents a single item to be enriched with artwork.
    /// </summary>
    public class EnrichmentItem
    {
        public string AlbumName { get; set; }
        public string ArtistName { get; set; }
        public string MbReleaseId { get; set; }
        public string CurrentCoverPath { get; set; }
        public object Tag { get; set; } // Optional tag for tracking back to DB record
    }

    public class ArtworkEnrichedEventArgs : EventArgs
    {
        public EnrichmentItem Item { get; set; }
        public string ArtworkPath { get; set; }
        public bool Success { get; set; }
    }

    public class BatchEnrichedEventArgs : EventArgs
    {
        public int TotalProcessed { get; set; }
        public int SuccessCount { get; set; }
        public int FailCount { get; set; }
    }
}

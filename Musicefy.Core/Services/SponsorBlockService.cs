using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Musicefy.Core.Services
{
    /// <summary>
    /// Sprint 4: SponsorBlock integration for YouTube content.
    ///
    /// Fetches sponsor/intro/outro/self-promo/interaction segments from the
    /// free, public SponsorBlock API at sponsor.ajay.app. No API key required.
    ///
    /// API docs: https://wiki.sponsor.ajay.app/w/API_Docs
    /// Endpoint: GET https://sponsor.ajay.app/api/skipSegments?videoID={id}
    ///
    /// This service is settings-agnostic — callers pass in the categories to
    /// fetch. The app's PlaybackService reads Settings.SponsorBlock* values
    /// and passes them in.
    /// </summary>
    public class SponsorBlockService
    {
        private const string ApiBaseUrl = "https://sponsor.ajay.app/api/skipSegments";
        private static readonly HttpClient _client;

        static SponsorBlockService()
        {
            _client = new HttpClient();
            _client.DefaultRequestHeaders.Add("User-Agent", "Musicefy/1.0 (https://github.com/marbou92/Musicefy)");
            _client.Timeout = TimeSpan.FromSeconds(5);
        }

        /// <summary>
        /// In-memory cache of video ID → segments. SponsorBlock segments never
        /// change for a given video, so we can cache indefinitely.
        /// Key is "{videoId}|{cat1,cat2,...}" so different category sets don't collide.
        /// </summary>
        private static readonly Dictionary<string, List<SponsorSegment>> _cache =
            new Dictionary<string, List<SponsorSegment>>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Fetch the sponsor segments for a YouTube video ID.
        /// Returns an empty list if the API call fails or the video has no segments.
        /// </summary>
        /// <param name="videoId">YouTube video ID (e.g. "dQw4w9WgXcQ")</param>
        /// <param name="categories">Categories to fetch: "sponsor", "intro", "outro", "selfpromo", "interaction", "music_offtopic"</param>
        public async Task<List<SponsorSegment>> GetSegmentsAsync(string videoId, IEnumerable<string> categories)
        {
            if (string.IsNullOrEmpty(videoId) || categories == null)
                return new List<SponsorSegment>();

            var catList = categories.Distinct().OrderBy(c => c).ToList();
            if (catList.Count == 0)
                return new List<SponsorSegment>();

            var cacheKey = $"{videoId}|{string.Join(",", catList)}";

            // Check cache first
            lock (_cache)
            {
                if (_cache.TryGetValue(cacheKey, out var cached))
                    return cached;
            }

            try
            {
                var categoriesParam = "[" + string.Join(",", catList.Select(c => $"\"{c}\"")) + "]";
                var url = $"{ApiBaseUrl}?videoID={Uri.EscapeDataString(videoId)}&categories={Uri.EscapeDataString(categoriesParam)}";

                var response = await _client.GetStringAsync(url);
                var json = JArray.Parse(response);

                var segments = new List<SponsorSegment>();
                foreach (var item in json)
                {
                    var seg = item["segment"];
                    if (seg != null && seg.Count() >= 2)
                    {
                        segments.Add(new SponsorSegment
                        {
                            StartTime = seg[0].Value<double>(),
                            EndTime = seg[1].Value<double>(),
                            Category = item["category"]?.Value<string>() ?? "",
                            Action = item["actionType"]?.Value<string>() ?? "skip"
                        });
                    }
                }

                // Cache the result
                lock (_cache)
                {
                    _cache[cacheKey] = segments;
                }

                return segments;
            }
            catch (HttpRequestException ex)
            {
                // 404 is normal — means no segments for this video. Don't log it.
                if (!ex.Message.Contains("404"))
                    System.Diagnostics.Debug.WriteLine($"[SponsorBlock] API failed for {videoId}: {ex.Message}");
                return new List<SponsorSegment>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SponsorBlock] Failed for {videoId}: {ex.Message}");
                return new List<SponsorSegment>();
            }
        }

        /// <summary>
        /// Returns true if the given position (in seconds) falls within a
        /// skip-able segment. If so, returns the segment so the caller can
        /// seek to EndTime.
        /// </summary>
        public bool ShouldSkip(double currentPositionSeconds, List<SponsorSegment> segments, out SponsorSegment segmentToSkip)
        {
            segmentToSkip = null;
            if (segments == null || segments.Count == 0) return false;

            foreach (var seg in segments)
            {
                if (seg.Action != "skip") continue;
                if (currentPositionSeconds >= seg.StartTime && currentPositionSeconds < seg.EndTime)
                {
                    // Only skip if the segment is at least 1 second long (avoid micro-segments)
                    if (seg.EndTime - seg.StartTime >= 1.0)
                    {
                        segmentToSkip = seg;
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Clear the in-memory cache. Call this when the user changes
        /// SponsorBlock settings to force re-fetching.
        /// </summary>
        public void ClearCache()
        {
            lock (_cache)
            {
                _cache.Clear();
            }
        }

        /// <summary>
        /// Helper: returns the standard SponsorBlock category names.
        /// </summary>
        public static readonly IReadOnlyList<string> AllCategories =
            new List<string> { "sponsor", "intro", "outro", "selfpromo", "interaction", "music_offtopic" };
    }

    /// <summary>
    /// A single SponsorBlock segment.
    /// </summary>
    public class SponsorSegment
    {
        /// <summary>Start time in seconds.</summary>
        public double StartTime { get; set; }

        /// <summary>End time in seconds.</summary>
        public double EndTime { get; set; }

        /// <summary>
        /// Category: "sponsor", "intro", "outro", "selfpromo", "interaction",
        /// "music_offtopic".
        /// </summary>
        public string Category { get; set; }

        /// <summary>
        /// Action type: "skip" (seek past), "mute" (mute audio), "full" (skip entire video).
        /// We only handle "skip" for now.
        /// </summary>
        public string Action { get; set; }

        public double Duration => EndTime - StartTime;

        public override string ToString() => $"[{Category}] {StartTime:F1}s → {EndTime:F1}s";
    }
}

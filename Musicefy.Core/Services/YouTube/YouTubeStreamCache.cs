using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Musicefy.Core.Services.YouTube
{
    /// <summary>
    /// Thread-safe cache for resolved YouTube stream URLs with expiration tracking.
    /// Inspired by Echo Music's songUrlCache (HashMap with expiration timestamps).
    /// Stream URLs from YouTube expire after ~6 hours (21540 seconds by default).
    /// </summary>
    public class YouTubeStreamCache
    {
        private readonly ConcurrentDictionary<string, CachedStreamEntry> _cache = new ConcurrentDictionary<string, CachedStreamEntry>();
        private readonly TimeSpan _defaultExpiration = TimeSpan.FromHours(5); // Slightly less than YouTube's 6hr

        private class CachedStreamEntry
        {
            public string StreamUrl { get; set; }
            public DateTime ExpiresAt { get; set; }
            public string MimeType { get; set; }
            public int Bitrate { get; set; }
            public string VideoId { get; set; }
        }

        /// <summary>
        /// Cache a resolved stream URL with expiration.
        /// </summary>
        public void Put(string videoId, string streamUrl, string mimeType = null,
            int bitrate = 0, int expiresInSeconds = 21540)
        {
            if (string.IsNullOrEmpty(videoId) || string.IsNullOrEmpty(streamUrl))
                return;

            // Use the lesser of our default expiration or the YouTube-provided one
            var expiresInSecondsSafe = Math.Min(expiresInSeconds, (int)_defaultExpiration.TotalSeconds);

            var entry = new CachedStreamEntry
            {
                StreamUrl = streamUrl,
                ExpiresAt = DateTime.UtcNow.AddSeconds(expiresInSecondsSafe),
                MimeType = mimeType,
                Bitrate = bitrate,
                VideoId = videoId
            };

            _cache.AddOrUpdate(videoId, entry, (key, old) => entry);
        }

        /// <summary>
        /// Try to get a cached stream URL. Returns null if not found or expired.
        /// </summary>
        public string TryGet(string videoId)
        {
            if (string.IsNullOrEmpty(videoId))
                return null;

            if (_cache.TryGetValue(videoId, out var entry))
            {
                if (entry.ExpiresAt > DateTime.UtcNow)
                    return entry.StreamUrl;

                // Remove expired entry
                _cache.TryRemove(videoId, out _);
            }

            return null;
        }

        /// <summary>
        /// Try to get a cached stream URL with full metadata.
        /// </summary>
        public bool TryGetWithMetadata(string videoId, out string streamUrl, out string mimeType, out int bitrate)
        {
            streamUrl = null;
            mimeType = null;
            bitrate = 0;

            if (string.IsNullOrEmpty(videoId))
                return false;

            if (_cache.TryGetValue(videoId, out var entry))
            {
                if (entry.ExpiresAt > DateTime.UtcNow)
                {
                    streamUrl = entry.StreamUrl;
                    mimeType = entry.MimeType;
                    bitrate = entry.Bitrate;
                    return true;
                }

                _cache.TryRemove(videoId, out _);
            }

            return false;
        }

        /// <summary>
        /// Remove a specific cached entry.
        /// </summary>
        public void Remove(string videoId)
        {
            _cache.TryRemove(videoId, out _);
        }

        /// <summary>
        /// Clear all expired entries from the cache.
        /// Inspired by Echo Music's cache management.
        /// </summary>
        public void PurgeExpired()
        {
            var now = DateTime.UtcNow;
            foreach (var kvp in _cache)
            {
                if (kvp.Value.ExpiresAt <= now)
                    _cache.TryRemove(kvp.Key, out _);
            }
        }

        /// <summary>
        /// Clear the entire cache.
        /// </summary>
        public void Clear()
        {
            _cache.Clear();
        }

        /// <summary>
        /// Get the current number of cached entries (including expired ones).
        /// </summary>
        public int Count => _cache.Count;
    }

    /// <summary>
    /// Cache for YouTube Music metadata (search results, album info, artist info).
    /// Inspired by Echo Music's HTTP cache (50MB disk) and in-memory caching.
    /// </summary>
    public class YouTubeMetadataCache
    {
        private readonly ConcurrentDictionary<string, MetadataCacheEntry> _cache = new ConcurrentDictionary<string, MetadataCacheEntry>();
        private readonly TimeSpan _searchCacheDuration = TimeSpan.FromMinutes(30);
        private readonly TimeSpan _browseCacheDuration = TimeSpan.FromHours(2);

        private class MetadataCacheEntry
        {
            public object Data { get; set; }
            public DateTime ExpiresAt { get; set; }
            public string Key { get; set; }
        }

        /// <summary>
        /// Cache search results.
        /// </summary>
        public void PutSearchResults(string query, InnerTubeClient.SearchResponse results)
        {
            if (string.IsNullOrEmpty(query) || results == null) return;

            var key = $"search:{query}";
            _cache[key] = new MetadataCacheEntry
            {
                Data = results,
                ExpiresAt = DateTime.UtcNow.Add(_searchCacheDuration),
                Key = key
            };
        }

        /// <summary>
        /// Try to get cached search results.
        /// </summary>
        public bool TryGetSearchResults(string query, out InnerTubeClient.SearchResponse results)
        {
            results = null;
            var key = $"search:{query}";

            if (_cache.TryGetValue(key, out var entry) && entry.ExpiresAt > DateTime.UtcNow)
            {
                results = entry.Data as InnerTubeClient.SearchResponse;
                return results != null;
            }

            _cache.TryRemove(key, out _);
            return false;
        }

        /// <summary>
        /// Cache browse results (albums, artists, playlists).
        /// </summary>
        public void PutBrowseResults(string browseId, InnerTubeClient.BrowseResponse results)
        {
            if (string.IsNullOrEmpty(browseId) || results == null) return;

            var key = $"browse:{browseId}";
            _cache[key] = new MetadataCacheEntry
            {
                Data = results,
                ExpiresAt = DateTime.UtcNow.Add(_browseCacheDuration),
                Key = key
            };
        }

        /// <summary>
        /// Try to get cached browse results.
        /// </summary>
        public bool TryGetBrowseResults(string browseId, out InnerTubeClient.BrowseResponse results)
        {
            results = null;
            var key = $"browse:{browseId}";

            if (_cache.TryGetValue(key, out var entry) && entry.ExpiresAt > DateTime.UtcNow)
            {
                results = entry.Data as InnerTubeClient.BrowseResponse;
                return results != null;
            }

            _cache.TryRemove(key, out _);
            return false;
        }

        /// <summary>
        /// Clear all expired entries.
        /// </summary>
        public void PurgeExpired()
        {
            var now = DateTime.UtcNow;
            foreach (var kvp in _cache)
            {
                if (kvp.Value.ExpiresAt <= now)
                    _cache.TryRemove(kvp.Key, out _);
            }
        }

        public void Clear() => _cache.Clear();
        public int Count => _cache.Count;
    }
}

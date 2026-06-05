using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Windows.Media.Imaging;

namespace Musicefy.Core.Services
{
    /// <summary>
    /// Singleton image cache service with LRU eviction policy.
    /// Centralizes decoded BitmapImage storage so that multiple controls binding
    /// to the same CoverPath share the same decoded instance rather than each
    /// independently decoding the image.
    ///
    /// Features:
    ///   - Thread-safe via ConcurrentDictionary
    ///   - LRU eviction when capacity is reached
    ///   - Access tracking via linked list (most-recently-used at tail)
    ///   - WeakReference secondary cache for memory efficiency
    ///   - Configurable max capacity (default 200 entries)
    ///
    /// Usage:
    ///   Register as singleton in DI:
    ///     services.AddSingleton&lt;ImageCacheService&gt;()
    ///
    ///   Inject and use in converters:
    ///     var cached = imageCache.Get(path);
    ///     if (cached != null) return cached;
    ///     // ... decode image ...
    ///     imageCache.Put(path, bitmap);
    /// </summary>
    public class ImageCacheService
    {
        private readonly int _maxCapacity;
        private readonly ConcurrentDictionary<string, CacheEntry> _cache;
        private readonly ConcurrentDictionary<string, WeakReference<BitmapImage>> _weakCache;
        private readonly object _evictionLock = new object();

        /// <summary>
        /// Creates a new ImageCacheService with the specified maximum capacity.
        /// When the cache reaches capacity, the least-recently-used entries are evicted.
        /// </summary>
        /// <param name="maxCapacity">Maximum number of strong references to hold (default 200)</param>
        public ImageCacheService(int maxCapacity = 200)
        {
            _maxCapacity = maxCapacity;
            _cache = new ConcurrentDictionary<string, CacheEntry>(StringComparer.OrdinalIgnoreCase);
            _weakCache = new ConcurrentDictionary<string, WeakReference<BitmapImage>>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Number of strong references currently in the cache.
        /// </summary>
        public int Count => _cache.Count;

        /// <summary>
        /// Retrieves a cached BitmapImage by key. Returns null if not cached.
        /// If the strong cache doesn't have it, checks the weak cache.
        /// Accessing an entry promotes it to most-recently-used.
        /// </summary>
        /// <param name="key">Cache key (typically the cover file path)</param>
        /// <returns>The cached BitmapImage, or null if not found</returns>
        public BitmapImage Get(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;

            // Check strong cache first
            if (_cache.TryGetValue(key, out var entry))
            {
                entry.LastAccess = DateTime.UtcNow;
                return entry.Image;
            }

            // Check weak cache (may still be alive)
            if (_weakCache.TryGetValue(key, out var weakRef) &&
                weakRef.TryGetTarget(out var weakBmp) &&
                weakBmp != null)
            {
                // Promote back to strong cache if space available
                Put(key, weakBmp);
                return weakBmp;
            }

            // Clean up dead weak reference
            if (weakRef != null)
                _weakCache.TryRemove(key, out _);

            return null;
        }

        /// <summary>
        /// Retrieves a cached BitmapImage with size-specific key.
        /// Combines the key with the decode width to create a unique cache entry.
        /// </summary>
        public BitmapImage Get(string key, int decodeWidth)
        {
            return Get($"{decodeWidth}:{key}");
        }

        /// <summary>
        /// Stores a BitmapImage in the cache. If the cache is at capacity,
        /// evicts the least-recently-used entry.
        /// </summary>
        /// <param name="key">Cache key (typically the cover file path)</param>
        /// <param name="image">The decoded BitmapImage to cache</param>
        public void Put(string key, BitmapImage image)
        {
            if (string.IsNullOrEmpty(key) || image == null) return;

            var entry = new CacheEntry
            {
                Key = key,
                Image = image,
                LastAccess = DateTime.UtcNow
            };

            _cache[key] = entry;

            // Also keep a weak reference so the image can survive eviction
            _weakCache[key] = new WeakReference<BitmapImage>(image);

            // Evict if over capacity
            if (_cache.Count > _maxCapacity)
            {
                EvictLeastRecentlyUsed();
            }
        }

        /// <summary>
        /// Stores a BitmapImage with size-specific key.
        /// </summary>
        public void Put(string key, int decodeWidth, BitmapImage image)
        {
            Put($"{decodeWidth}:{key}", image);
        }

        /// <summary>
        /// Checks whether a key exists in the cache (strong or weak).
        /// </summary>
        public bool Contains(string key)
        {
            if (_cache.ContainsKey(key)) return true;
            if (_weakCache.TryGetValue(key, out var weakRef) &&
                weakRef.TryGetTarget(out var bmp) && bmp != null)
                return true;
            return false;
        }

        /// <summary>
        /// Removes a specific key from the cache.
        /// </summary>
        public void Remove(string key)
        {
            _cache.TryRemove(key, out _);
            _weakCache.TryRemove(key, out _);
        }

        /// <summary>
        /// Clears all entries from both strong and weak caches.
        /// </summary>
        public void Clear()
        {
            _cache.Clear();
            _weakCache.Clear();
        }

        /// <summary>
        /// Evicts dead weak references and the least-recently-used strong entries
        /// until the cache is under capacity. Uses a simple scan-and-remove approach
        /// which is efficient for the typical cache size (200 entries).
        /// </summary>
        private void EvictLeastRecentlyUsed()
        {
            lock (_evictionLock)
            {
                // Remove dead weak references first
                foreach (var kvp in _weakCache.ToList())
                {
                    if (!kvp.Value.TryGetTarget(out var bmp) || bmp == null)
                        _weakCache.TryRemove(kvp.Key, out _);
                }

                // If still over capacity, evict LRU entries
                while (_cache.Count > _maxCapacity)
                {
                    var lru = _cache.OrderBy(kvp => kvp.Value.LastAccess).FirstOrDefault();
                    if (lru.Key != null)
                    {
                        _cache.TryRemove(lru.Key, out _);
                        // Note: we keep the weak reference so the image can still be
                        // accessed if it hasn't been GC'd yet
                    }
                    else
                    {
                        break;
                    }
                }
            }
        }

        private class CacheEntry
        {
            public string Key { get; set; }
            public BitmapImage Image { get; set; }
            public DateTime LastAccess { get; set; }
        }
    }
}

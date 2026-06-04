using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Musicefy.Core.Interfaces;
using Musicefy.Core.Models;
using Newtonsoft.Json;
using static Musicefy.Core.SourceTypes;

namespace Musicefy.Core.Services
{
    /// <summary>
    /// Streaming source manager implementation using provider registry.
    /// Enhanced with YouTube-specific session support and improved stream resolution
    /// inspired by Echo Music's architecture.
    /// </summary>
    public class StreamingSourceManagerImpl : IStreamingSourceManager
    {
        private readonly List<StreamingSource> _sources = new List<StreamingSource>();
        private readonly Dictionary<string, IMusicSourceSession> _activeSessions = new Dictionary<string, IMusicSourceSession>();
        private readonly Dictionary<string, IMusicSourceProvider> _providers = new Dictionary<string, IMusicSourceProvider>();
        private readonly string _storageFilePath;
        private readonly IServiceProvider _serviceProvider;
        private readonly object _lock = new object();
        private IReadOnlyList<StreamingSource> _sourcesSnapshot;
        private static readonly HttpClient _httpClient = new HttpClient();

        public event EventHandler SourceAdded;

        public IReadOnlyList<StreamingSource> Sources
        {
            get
            {
                lock (_lock)
                {
                    if (_sourcesSnapshot == null)
                        _sourcesSnapshot = _sources.ToList().AsReadOnly();
                    return _sourcesSnapshot;
                }
            }
        }

        public StreamingSourceManagerImpl(IServiceProvider serviceProvider, IEnumerable<IMusicSourceProvider> providers)
        {
            _serviceProvider = serviceProvider;

            foreach (var provider in providers)
            {
                _providers[provider.SourceType] = provider;
            }

            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var musicefyPath = Path.Combine(appDataPath, "Musicefy");

            if (!Directory.Exists(musicefyPath))
                Directory.CreateDirectory(musicefyPath);

            _storageFilePath = Path.Combine(musicefyPath, "sources.json");
            LoadSources();
            RestoreSessions();
        }

        public async Task<bool> AddSourceAsync(StreamingSource source, CancellationToken cancellationToken = default)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            if (string.IsNullOrEmpty(source.Id))
                source.Id = Guid.NewGuid().ToString();

            source.EnsureConfiguration();

            if (!_providers.TryGetValue(source.Type, out var provider))
                throw new InvalidOperationException($"Unsupported source type: {source.Type}");

            if (source.Type == Local)
            {
                var path = GetConfig(source, "folderPath");
                if (string.IsNullOrEmpty(path))
                    path = source.Url;
                if (!string.IsNullOrEmpty(path) && !Directory.Exists(path))
                    throw new InvalidOperationException("Local folder not found.");

                source.IsConnected = true;
                var session = provider.CreateSession(source.Configuration, source.Id);
                lock (_lock)
                {
                    _sources.Add(source);
                    _sourcesSnapshot = null;
                    _activeSessions[source.Id] = session;
                }
                SaveSources();
                OnSourceAdded();
                return true;
            }
            else
            {
                var connected = await provider.TestConnectionAsync(source.Configuration);
                if (!connected)
                    throw new InvalidOperationException($"Failed to connect to {provider.DisplayName}.");

                source.IsConnected = true;
                var session = provider.CreateSession(source.Configuration, source.Id);
                lock (_lock)
                {
                    _sources.Add(source);
                    _sourcesSnapshot = null;
                    _activeSessions[source.Id] = session;
                }
                SaveSources();
                OnSourceAdded();
                return true;
            }
        }

        public void RemoveSource(string sourceId)
        {
            IMusicSourceSession session = null;
            lock (_lock)
            {
                var source = _sources.FirstOrDefault(s => s.Id == sourceId);
                if (source == null) return;

                _sources.Remove(source);
                _sourcesSnapshot = null;

                if (_activeSessions.TryGetValue(sourceId, out session))
                {
                    _activeSessions.Remove(sourceId);
                }
            }

            session?.Dispose();
            SaveSources();
        }

        public StreamingSource GetSource(string sourceId)
        {
            lock (_lock) return _sources.FirstOrDefault(s => s.Id == sourceId);
        }

        public IMusicSourceSession GetSession(string sourceId)
        {
            lock (_lock)
            {
                _activeSessions.TryGetValue(sourceId, out var session);
                return session;
            }
        }

        /// <summary>
        /// Get a YouTube-specific session if the source is YouTube.
        /// Enables access to enhanced YouTube features (filtered search, playlists, radio).
        /// </summary>
        public IYouTubeSourceSession GetYouTubeSession(string sourceId)
        {
            lock (_lock)
            {
                if (_activeSessions.TryGetValue(sourceId, out var session) && session is IYouTubeSourceSession ytSession)
                    return ytSession;
                return null;
            }
        }

        public async Task<List<MusicFile>> SearchAllSourcesAsync(string query, CancellationToken cancellationToken = default)
        {
            List<KeyValuePair<string, IMusicSourceSession>> activeSessions;
            lock (_lock) activeSessions = _activeSessions.Where(s => _sources.Any(src => src.Id == s.Key && src.IsConnected)).ToList();

            if (activeSessions.Count == 0)
                return new List<MusicFile>();

            var tasks = activeSessions.Select(kvp => SearchSourceWithTimeoutAsync(kvp.Key, kvp.Value, query, cancellationToken));

            var allTask = Task.WhenAll(tasks);
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(8));
            var overallTimeout = Task.Delay(-1, timeoutCts.Token);

            var completed = await Task.WhenAny(allTask, overallTimeout);

            if (completed == overallTimeout)
            {
                System.Diagnostics.Debug.WriteLine("[Search] Overall search timed out after 8s — returning partial results");
            }

            return tasks.Where(t => t.IsCompleted && t.Status == TaskStatus.RanToCompletion)
                        .SelectMany(t => t.Result)
                        .Where(r => r != null)
                        .ToList();
        }

        /// <summary>
        /// Search YouTube sources with type filter.
        /// Inspired by Echo Music's filtered search capability.
        /// </summary>
        public async Task<List<MusicFile>> SearchYouTubeWithTypeAsync(string query, string resultType, int limit = 50)
        {
            var results = new List<MusicFile>();
            List<KeyValuePair<string, IMusicSourceSession>> activeSessions;
            lock (_lock) activeSessions = _activeSessions
                .Where(s => _sources.Any(src => src.Id == s.Key && src.IsConnected && src.Type == YouTube))
                .ToList();

            foreach (var kvp in activeSessions)
            {
                if (kvp.Value is IYouTubeSourceSession ytSession)
                {
                    try
                    {
                        var searchResults = await ytSession.SearchWithTypeAsync(query, resultType, limit);
                        results.AddRange(searchResults);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[Search] YouTube filtered search failed: {ex.Message}");
                    }
                }
            }

            return results;
        }

        /// <summary>
        /// Get search suggestions from all YouTube sources.
        /// Inspired by Echo Music's autocomplete feature.
        /// </summary>
        public async Task<List<string>> GetSearchSuggestionsAsync(string query)
        {
            var suggestions = new List<string>();
            List<KeyValuePair<string, IMusicSourceSession>> activeSessions;
            lock (_lock) activeSessions = _activeSessions
                .Where(s => _sources.Any(src => src.Id == s.Key && src.IsConnected && src.Type == YouTube))
                .ToList();

            foreach (var kvp in activeSessions)
            {
                if (kvp.Value is IYouTubeSourceSession ytSession)
                {
                    try
                    {
                        var ytSuggestions = await ytSession.GetSearchSuggestionsAsync(query);
                        suggestions.AddRange(ytSuggestions);
                    }
                    catch { }
                }
            }

            return suggestions.Distinct().Take(10).ToList();
        }

        private async Task<List<MusicFile>> SearchSourceWithTimeoutAsync(string sourceId, IMusicSourceSession session, string query, CancellationToken cancellationToken)
        {
            var searchTask = SearchSourceAsync(sourceId, session, query, cancellationToken);
            var timeoutTask = Task.Delay(5000, cancellationToken);
            var completed = await Task.WhenAny(searchTask, timeoutTask);

            if (completed == timeoutTask)
            {
                System.Diagnostics.Debug.WriteLine($"[Search] Source {sourceId} timed out after 5s");
                return new List<MusicFile>();
            }

            return await searchTask;
        }

        private async Task<List<MusicFile>> SearchSourceAsync(string sourceId, IMusicSourceSession session, string query, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
                return new List<MusicFile>();

            try
            {
                var songs = await session.SearchAsync(query, 50);
                return songs?.ToList() ?? new List<MusicFile>();
            }
            catch (OperationCanceledException)
            {
                return new List<MusicFile>();
            }
            catch (Exception ex)
            {
                var source = GetSource(sourceId);
                System.Diagnostics.Debug.WriteLine($"Error searching {source?.Name ?? sourceId}: {ex.Message}");
                return new List<MusicFile>();
            }
        }

        /// <summary>
        /// Resolve a stream URL with improved error handling and retry logic.
        /// Inspired by Echo Music's multi-layer stream resolution with validation.
        /// </summary>
        public async Task<string> ResolveStreamUrlAsync(string resourceId)
        {
            if (string.IsNullOrEmpty(resourceId) || resourceId.StartsWith("http"))
                return resourceId;

            var parts = resourceId.Split(':');
            if (parts.Length < 2)
                return resourceId;

            var sourceId = parts[0];
            var trackId = string.Join(":", parts.Skip(1));

            IMusicSourceSession session;
            lock (_lock)
            {
                _activeSessions.TryGetValue(sourceId, out session);
            }

            if (session != null)
            {
                // Retry logic inspired by Echo Music's withRetry (3 attempts, exponential backoff)
                for (int attempt = 0; attempt < 3; attempt++)
                {
                    try
                    {
                        var url = await session.GetStreamUrlAsync(trackId);
                        if (!string.IsNullOrEmpty(url))
                            return url;

                        // If URL is empty, wait and retry (inspired by Echo Music's backoff)
                        if (attempt < 2)
                            await Task.Delay((int)(500 * Math.Pow(2, attempt)));
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"[StreamResolve] Attempt {attempt + 1} failed for {resourceId}: {ex.Message}");

                        if (attempt < 2)
                            await Task.Delay((int)(500 * Math.Pow(2, attempt)));
                    }
                }
            }

            return resourceId;
        }

        public async Task<byte[]> ResolveCoverArtAsync(string resourceId)
        {
            if (string.IsNullOrEmpty(resourceId))
                return null;

            if (resourceId.StartsWith("http"))
            {
                try
                {
                    using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
                    var response = await _httpClient.GetAsync(resourceId, timeoutCts.Token);
                    response.EnsureSuccessStatusCode();
                    return await response.Content.ReadAsByteArrayAsync();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[StreamingSourceManager] ResolveCoverArtAsync HTTP failed: {ex.Message}");
                    return null;
                }
            }

            var parts = resourceId.Split(':');
            if (parts.Length < 3 || parts[1] != "cover")
                return null;

            var sourceId = parts[0];
            var coverId = parts[2];

            IMusicSourceSession session;
            lock (_lock)
            {
                _activeSessions.TryGetValue(sourceId, out session);
            }

            if (session != null)
            {
                try
                {
                    return await session.GetCoverArtAsync(resourceId);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to resolve cover art: {ex.Message}");
                }
            }

            return null;
        }

        public async Task<bool> TestConnectionAsync(string sourceId, CancellationToken cancellationToken = default)
        {
            var source = GetSource(sourceId);
            if (source == null) return false;

            source.EnsureConfiguration();

            if (_providers.TryGetValue(source.Type, out var provider))
            {
                return await provider.TestConnectionAsync(source.Configuration);
            }

            return false;
        }

        public async Task<List<AlbumInfo>> GetAllAlbumsAsync(CancellationToken cancellationToken = default)
        {
            List<KeyValuePair<string, IMusicSourceSession>> activeSessions;
            lock (_lock) activeSessions = _activeSessions.Where(s => _sources.Any(src => src.Id == s.Key && src.IsConnected)).ToList();

            if (activeSessions.Count == 0)
                return new List<AlbumInfo>();

            var allTracks = new List<MusicFile>();
            var tasks = activeSessions.Select(kvp => GetTracksFromSourceAsync(kvp.Key, kvp.Value, cancellationToken));
            var results = await Task.WhenAll(tasks);

            foreach (var tracks in results.Where(r => r != null))
                allTracks.AddRange(tracks);

            return allTracks
                .Where(t => !string.IsNullOrEmpty(t.Album))
                .GroupBy(t => new { t.Album, t.Artist })
                .Select(g => new AlbumInfo
                {
                    Name = g.Key.Album,
                    Artist = g.Key.Artist,
                    Year = g.Max(t => t.Year),
                    CoverPath = g.FirstOrDefault(t => !string.IsNullOrEmpty(t.CoverPath))?.CoverPath,
                    SourceType = g.FirstOrDefault()?.SourceType,
                    Tracks = g.OrderBy(t => t.TrackNumber).ToList()
                })
                .OrderBy(a => a.Artist).ThenBy(a => a.Name)
                .ToList();
        }

        private async Task<List<MusicFile>> GetTracksFromSourceAsync(string sourceId, IMusicSourceSession session, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
                return new List<MusicFile>();

            try
            {
                // For YouTube sources, use GetAlbumListAsync instead of empty search
                var source = GetSource(sourceId);
                if (source?.Type == YouTube)
                {
                    var albumTracks = await session.GetAlbumListAsync(200);
                    if (albumTracks?.Count > 0)
                        return albumTracks.ToList();
                }

                var songs = await session.SearchAsync("", 200);
                return songs?.ToList() ?? new List<MusicFile>();
            }
            catch (OperationCanceledException)
            {
                return new List<MusicFile>();
            }
            catch (Exception ex)
            {
                var source = GetSource(sourceId);
                System.Diagnostics.Debug.WriteLine($"Error getting tracks from {source?.Name ?? sourceId}: {ex.Message}");
                return new List<MusicFile>();
            }
        }

        private static string GetConfig(StreamingSource source, string key)
        {
            if (source.Configuration != null && source.Configuration.TryGetValue(key, out var val))
                return val ?? "";
            return "";
        }

        private void RestoreSessions()
        {
            lock (_lock)
            {
                foreach (var source in _sources)
                {
                    if (!source.IsConnected) continue;

                    source.EnsureConfiguration();

                    if (_providers.TryGetValue(source.Type, out var provider))
                    {
                        try
                        {
                            var session = provider.CreateSession(source.Configuration, source.Id);
                            _activeSessions[source.Id] = session;
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[StreamingSourceManager] Failed to restore session for {source.Name}: {ex.Message}");
                        }
                    }
                }
            }
        }

        private static string EncryptPassword(string plainText)
        {
            if (string.IsNullOrEmpty(plainText)) return string.Empty;
            var plainBytes = Encoding.UTF8.GetBytes(plainText);
            var encryptedBytes = ProtectedData.Protect(plainBytes, null, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(encryptedBytes);
        }

        private static string DecryptPassword(string cipherText)
        {
            if (string.IsNullOrEmpty(cipherText)) return string.Empty;
            try
            {
                var encryptedBytes = Convert.FromBase64String(cipherText);
                var plainBytes = ProtectedData.Unprotect(encryptedBytes, null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(plainBytes);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[StreamingSourceManager] DecryptPassword failed: {ex.Message}");
                return string.Empty;
            }
        }

        private static bool TryParseBool(object value, out bool result)
        {
            if (value is bool b) { result = b; return true; }
            if (value is string s) return bool.TryParse(s, out result);
            if (value is int i) { result = i != 0; return true; }
            result = false;
            return false;
        }

        private void SaveSources()
        {
            try
            {
                List<StreamingSource> sourcesSnapshot;
                lock (_lock) sourcesSnapshot = _sources.ToList();

                var sourcesToSave = sourcesSnapshot.Select(s => new
                {
                    s.Id,
                    s.Name,
                    s.Type,
                    s.Url,
                    s.Username,
                    EncryptedPassword = EncryptPassword(s.Password),
                    s.IsConnected,
                    Configuration = s.Configuration != null
                        ? s.Configuration.Where(kvp => !string.Equals(kvp.Key, "password", System.StringComparison.OrdinalIgnoreCase)).ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
                        : null
                }).ToList();

                var json = JsonConvert.SerializeObject(sourcesToSave, Formatting.Indented);
                File.WriteAllText(_storageFilePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving sources: {ex.Message}");
            }
        }

        protected virtual void OnSourceAdded()
        {
            try { SourceAdded?.Invoke(this, EventArgs.Empty); } catch { }
        }

        private void LoadSources()
        {
            try
            {
                if (!File.Exists(_storageFilePath)) return;

                var json = File.ReadAllText(_storageFilePath);
                var loadedSources = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(json);

                if (loadedSources == null) return;

                lock (_lock)
                {
                    foreach (var sourceData in loadedSources)
                    {
                        var source = new StreamingSource
                        {
                            Id = sourceData.ContainsKey("Id") ? sourceData["Id"].ToString() : Guid.NewGuid().ToString(),
                            Name = sourceData.ContainsKey("Name") ? sourceData["Name"].ToString() : "Unknown",
                            Type = sourceData.ContainsKey("Type") ? sourceData["Type"].ToString() : SourceTypes.Local,
                            Url = sourceData.ContainsKey("Url") ? sourceData["Url"].ToString() : "",
                            Username = sourceData.ContainsKey("Username") ? sourceData["Username"].ToString() : "",
                            Password = sourceData.ContainsKey("EncryptedPassword") ? DecryptPassword(sourceData["EncryptedPassword"].ToString()) : "",
                            IsConnected = sourceData.ContainsKey("IsConnected") && TryParseBool(sourceData["IsConnected"], out var connected) && connected
                        };

                        if (sourceData.ContainsKey("Configuration") && sourceData["Configuration"] is Newtonsoft.Json.Linq.JObject configObj)
                        {
                            source.Configuration = configObj.ToObject<Dictionary<string, string>>() ?? new Dictionary<string, string>();
                        }

                        source.EnsureConfiguration();
                        _sources.Add(source);
                    }
                    _sourcesSnapshot = null;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading sources: {ex.Message}");
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    /// Streaming source manager implementation using provider registry
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

        [Obsolete("GetClient is deprecated. Use GetSession instead.")]
        public ISubsonicClient GetClient(string sourceId)
        {
            return null;
        }

        public async Task<List<MusicFile>> SearchAllSourcesAsync(string query, CancellationToken cancellationToken = default)
        {
            List<KeyValuePair<string, IMusicSourceSession>> activeSessions;
            lock (_lock) activeSessions = _activeSessions.Where(s => _sources.Any(src => src.Id == s.Key && src.IsConnected)).ToList();

            if (activeSessions.Count == 0)
                return new List<MusicFile>();

            var tasks = activeSessions.Select(kvp => SearchSourceAsync(kvp.Key, kvp.Value, query, cancellationToken));
            var results = await Task.WhenAll(tasks);

            return results.Where(r => r != null).SelectMany(r => r).ToList();
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
                try
                {
                    return await session.GetStreamUrlAsync(trackId);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to resolve stream URL: {ex.Message}");
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
                    using var client = new System.Net.Http.HttpClient();
                    var response = await client.GetAsync(resourceId);
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
                        ? new Dictionary<string, string>(s.Configuration.Where(kvp => !string.Equals(kvp.Key, "password", System.StringComparison.OrdinalIgnoreCase)))
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
                            Id = sourceData["Id"].ToString(),
                            Name = sourceData["Name"].ToString(),
                            Type = sourceData["Type"].ToString(),
                            Url = sourceData.ContainsKey("Url") ? sourceData["Url"].ToString() : "",
                            Username = sourceData.ContainsKey("Username") ? sourceData["Username"].ToString() : "",
                            Password = sourceData.ContainsKey("EncryptedPassword") ? DecryptPassword(sourceData["EncryptedPassword"].ToString()) : "",
                            IsConnected = sourceData.ContainsKey("IsConnected") && (bool)sourceData["IsConnected"]
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

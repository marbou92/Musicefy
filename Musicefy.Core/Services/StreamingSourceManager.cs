using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Musicefy.Core.Models;
using Newtonsoft.Json;

namespace Musicefy.Core.Services
{
    /// <summary>
    /// Manages streaming sources and their connections (Subsonic + Local)
    /// </summary>
    public class StreamingSourceManager
    {
        private List<StreamingSource> sources;
        private readonly string sourcesFilePath;
        private readonly Dictionary<string, SubsonicClient> activeClients;

        public StreamingSourceManager()
        {
            sources = new List<StreamingSource>();
            activeClients = new Dictionary<string, SubsonicClient>();
            sourcesFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Musicefy", "sources.json");
            LoadSources();
        }

        /// <summary>
        /// Add a new source (Subsonic or Local)
        /// </summary>
        public async Task<bool> AddSourceAsync(StreamingSource source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            if (string.IsNullOrEmpty(source.Id))
                source.Id = Guid.NewGuid().ToString();

            if (source.Type == "Local")
            {
                if (!Directory.Exists(source.Url))
                    throw new InvalidOperationException("Local folder not found.");

                source.IsConnected = true;
                sources.Add(source);
                SaveSources();
                return true;
            }
            else if (source.Type == "Subsonic")
            {
                var client = new SubsonicClient(source);
                var connected = await client.TestConnectionAsync();

                if (!connected)
                    throw new InvalidOperationException("Failed to connect to the streaming service. Please check your credentials.");

                source.IsConnected = true;
                sources.Add(source);
                activeClients[source.Id] = client;
                SaveSources();
                return true;
            }

            throw new InvalidOperationException("Unsupported source type.");
        }

        public void RemoveSource(string sourceId)
        {
            var source = sources.FirstOrDefault(s => s.Id == sourceId);
            if (source != null)
            {
                sources.Remove(source);
                if (activeClients.ContainsKey(sourceId))
                {
                    activeClients[sourceId].Dispose();
                    activeClients.Remove(sourceId);
                }
                SaveSources();
            }
        }

        public List<StreamingSource> GetAllSources() => new List<StreamingSource>(sources);

        public StreamingSource GetSource(string sourceId) => sources.FirstOrDefault(s => s.Id == sourceId);

        public SubsonicClient GetClient(string sourceId)
        {
            activeClients.TryGetValue(sourceId, out var client);
            return client;
        }

        /// <summary>
        /// Search across all sources (Subsonic + Local)
        /// </summary>
        public async Task<List<MusicFile>> SearchAllSourcesAsync(string query)
        {
            var allSongs = new List<MusicFile>();

            foreach (var source in sources.Where(s => s.IsConnected))
            {
                try
                {
                    if (source.Type == "Local")
                    {
                        var localSongs = SearchLocalFiles(source.Url, query);
                        allSongs.AddRange(localSongs);
                    }
                    else if (source.Type == "Subsonic")
                    {
                        var client = GetClient(source.Id);
                        if (client != null)
                        {
                            var songs = await client.SearchAsync(query);
                            allSongs.AddRange(songs);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error searching {source.Name}: {ex.Message}");
                }
            }

            return allSongs;
        }

        /// <summary>
        /// Scan local folder for audio files
        /// </summary>
        private List<MusicFile> SearchLocalFiles(string folderPath, string query)
        {
            var files = Directory.EnumerateFiles(folderPath, "*.*", SearchOption.AllDirectories)
                                 .Where(f => f.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase) ||
                                             f.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) ||
                                             f.EndsWith(".flac", StringComparison.OrdinalIgnoreCase));

            var results = new List<MusicFile>();

            foreach (var file in files)
            {
                var name = Path.GetFileNameWithoutExtension(file);

                // ✅ FIX: use IndexOf instead of Contains with StringComparison
                if (string.IsNullOrEmpty(query) || name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    results.Add(new MusicFile
                    {
                        Id = Guid.NewGuid().ToString(),
                        Title = name,
                        Artist = "Local",
                        Album = "",
                        FilePath = file,   // ✅ FIX: use FilePath property
                        SourceUri = file,
                        SourceType = "Local"
                    });
                }
            }

            return results;
        }

        private void SaveSources()
        {
            try
            {
                var directory = Path.GetDirectoryName(sourcesFilePath);
                if (!Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                var sourcesToSave = sources.Select(s => new
                {
                    s.Id,
                    s.Name,
                    s.Type,
                    s.Url,
                    s.Username,
                    s.IsConnected
                }).ToList();

                var json = JsonConvert.SerializeObject(sourcesToSave, Formatting.Indented);
                File.WriteAllText(sourcesFilePath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving sources: {ex.Message}");
            }
        }

        private void LoadSources()
        {
            try
            {
                if (File.Exists(sourcesFilePath))
                {
                    var json = File.ReadAllText(sourcesFilePath);
                    var loadedSources = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(json);

                    if (loadedSources != null)
                    {
                        foreach (var sourceData in loadedSources)
                        {
                            sources.Add(new StreamingSource
                            {
                                Id = sourceData["Id"].ToString(),
                                Name = sourceData["Name"].ToString(),
                                Type = sourceData["Type"].ToString(),
                                Url = sourceData["Url"].ToString(),
                                Username = sourceData.ContainsKey("Username") ? sourceData["Username"].ToString() : "",
                                IsConnected = sourceData.ContainsKey("IsConnected") && (bool)sourceData["IsConnected"]
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading sources: {ex.Message}");
            }
        }
    }
}

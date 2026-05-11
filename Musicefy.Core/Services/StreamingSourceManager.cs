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
    /// Manages streaming sources and their connections
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

        public async Task<bool> AddSourceAsync(StreamingSource source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            if (string.IsNullOrEmpty(source.Id))
                source.Id = Guid.NewGuid().ToString();

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

        public async Task<List<MusicFile>> SearchAllSourcesAsync(string query)
        {
            var allSongs = new List<MusicFile>();

            foreach (var source in sources.Where(s => s.IsConnected))
            {
                try
                {
                    var client = GetClient(source.Id);
                    if (client != null)
                    {
                        var songs = await client.SearchAsync(query);
                        allSongs.AddRange(songs);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error searching {source.Name}: {ex.Message}");
                }
            }

            return allSongs;
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
                                Username = sourceData["Username"].ToString(),
                                IsConnected = false
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

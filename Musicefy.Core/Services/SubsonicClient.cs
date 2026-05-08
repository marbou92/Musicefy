using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Musicefy.Core.Models;

namespace Musicefy.Core.Services
{
    /// <summary>
    /// Subsonic API client for streaming services like Squidify
    /// </summary>
    public class SubsonicClient : IDisposable
    {
        private readonly HttpClient httpClient;
        private readonly StreamingSource source;
        private const string ApiVersion = "1.16.1";
        private const string ClientName = "Musicefy";

        public SubsonicClient(StreamingSource source)
        {
            this.source = source ?? throw new ArgumentNullException(nameof(source));
            this.httpClient = new HttpClient();
        }

        /// <summary>
        /// Test connection to the streaming service
        /// </summary>
        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                var response = await MakeSubsonicRequestAsync("ping", null);
                return response != null && response.Attribute("status")?.Value == "ok";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Connection test failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Get all music folders from the streaming service
        /// </summary>
        public async Task<List<string>> GetMusicFoldersAsync()
        {
            try
            {
                var response = await MakeSubsonicRequestAsync("getMusicFolders", null);
                var folders = new List<string>();

                var musicFolders = response?.Element("musicFolders");
                if (musicFolders != null)
                {
                    foreach (var folder in musicFolders.Elements("musicFolder"))
                    {
                        var id = folder.Attribute("id")?.Value;
                        var name = folder.Attribute("name")?.Value;
                        if (id != null)
                        {
                            folders.Add(name ?? $"Folder {id}");
                        }
                    }
                }

                return folders;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to get music folders: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Search for songs on the streaming service
        /// </summary>
        public async Task<List<MusicFile>> SearchAsync(string query, int count = 50)
        {
            try
            {
                var parameters = new Dictionary<string, string>
                {
                    { "query", query },
                    { "songCount", count.ToString() }
                };

                var response = await MakeSubsonicRequestAsync("search3", parameters);
                var songs = new List<MusicFile>();

                var searchResult = response?.Element("searchResult3");
                if (searchResult != null)
                {
                    foreach (var song in searchResult.Elements("song"))
                    {
                        songs.Add(ParseSongFromXml(song));
                    }
                }

                return songs;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Search failed: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Get random songs from the streaming service
        /// </summary>
        public async Task<List<MusicFile>> GetRandomSongsAsync(int count = 50)
        {
            try
            {
                var parameters = new Dictionary<string, string>
                {
                    { "size", count.ToString() }
                };

                var response = await MakeSubsonicRequestAsync("getRandomSongs", parameters);
                var songs = new List<MusicFile>();

                if (response != null)
                {
                    foreach (var song in response.Elements("song"))
                    {
                        songs.Add(ParseSongFromXml(song));
                    }
                }

                return songs;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to get random songs: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Get the streaming URL for a song
        /// </summary>
        public string GetStreamUrl(string songId)
        {
            var salt = Guid.NewGuid().ToString().Substring(0, 8);
            var tokenHash = GenerateTokenHash(source.Password, salt);

            return $"{source.Url.TrimEnd('/')}/rest/stream" +
                   $"?u={source.Username}" +
                   $"&t={tokenHash}" +
                   $"&s={salt}" +
                   $"&c={ClientName}" +
                   $"&v={ApiVersion}" +
                   $"&id={songId}";
        }

        /// <summary>
        /// Parse song information from XML response
        /// </summary>
        private MusicFile ParseSongFromXml(XElement songElement)
        {
            return new MusicFile
            {
                FilePath = $"{source.Id}:{songElement.Attribute("id")?.Value}", // Use source:songId format
                Title = songElement.Attribute("title")?.Value ?? "Unknown",
                Artist = songElement.Attribute("artist")?.Value ?? "Unknown Artist",
                Album = songElement.Attribute("album")?.Value ?? "Unknown Album",
                Genre = songElement.Attribute("genre")?.Value ?? "Unknown",
                Duration = ParseDuration(songElement.Attribute("duration")?.Value),
                Year = int.TryParse(songElement.Attribute("year")?.Value, out var year) ? year : 0,
                TrackNumber = int.TryParse(songElement.Attribute("track")?.Value, out var track) ? track : 0
            };
        }

        /// <summary>
        /// Parse duration from seconds to TimeSpan
        /// </summary>
        private TimeSpan ParseDuration(string durationStr)
        {
            if (int.TryParse(durationStr, out var seconds))
            {
                return TimeSpan.FromSeconds(seconds);
            }
            return TimeSpan.Zero;
        }

        /// <summary>
        /// Generate MD5 token hash for Subsonic API authentication
        /// </summary>
        private string GenerateTokenHash(string password, string salt)
        {
            var combined = password + salt;
            using (var md5 = System.Security.Cryptography.MD5.Create())
            {
                var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(combined));
                return BitConverter.ToString(hash).Replace("-", "").ToLower();
            }
        }

        /// <summary>
        /// Make a generic Subsonic API request
        /// </summary>
        private async Task<XElement> MakeSubsonicRequestAsync(string method, Dictionary<string, string> parameters)
        {
            var salt = Guid.NewGuid().ToString().Substring(0, 8);
            var tokenHash = GenerateTokenHash(source.Password, salt);

            var url = $"{source.Url.TrimEnd('/')}/rest/{method}" +
                      $"?u={source.Username}" +
                      $"&t={tokenHash}" +
                      $"&s={salt}" +
                      $"&c={ClientName}" +
                      $"&v={ApiVersion}" +
                      $"&f=xml";

            if (parameters != null)
            {
                foreach (var param in parameters)
                {
                    url += $"&{param.Key}={Uri.EscapeDataString(param.Value)}";
                }
            }

            try
            {
                var response = await httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                var doc = XDocument.Parse(content);
                return doc.Root?.Element("response") ?? doc.Root;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"API request failed: {ex.Message}", ex);
            }
        }

        public void Dispose()
        {
            httpClient?.Dispose();
        }
    }
}

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Musicefy.Core.Interfaces;
using Musicefy.Core.Models;
using static Musicefy.Core.SourceTypes;

namespace Musicefy.Core.Services
{
    /// <summary>
    /// Subsonic API client implementation
    /// </summary>
    public class SubsonicClientImpl : ISubsonicClient
    {
        private readonly HttpClient _httpClient;
        private readonly bool _ownsHttpClient;
        private readonly StreamingSource _source;
        private readonly SecureString _password;
        private readonly string _apiVersion;
        private readonly string _clientName;

        public SubsonicClientImpl(StreamingSource source, HttpClient httpClient = null)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _ownsHttpClient = httpClient == null;
            _httpClient = httpClient ?? new HttpClient();
            _apiVersion = "1.16.1";
            _clientName = "Musicefy";

            ValidateServerUrl();

            if (!string.IsNullOrEmpty(source.Password))
            {
                _password = new SecureString();
                foreach (char c in source.Password)
                    _password.AppendChar(c);
                _password.MakeReadOnly();
            }
        }

        public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var response = await MakeRequestAsync("ping", null, cancellationToken);
                return response != null && response.Attribute("status")?.Value == "ok";
            }
            catch
            {
                return false;
            }
        }

        public async Task<List<string>> GetMusicFoldersAsync(CancellationToken cancellationToken = default)
        {
            var response = await MakeRequestAsync("getMusicFolders", null, cancellationToken);
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

        public async Task<List<MusicFile>> SearchAsync(string query, int count = 50, CancellationToken cancellationToken = default)
        {
            var parameters = new Dictionary<string, string>
            {
                { "query", query },
                { "songCount", count.ToString() }
            };

            var response = await MakeRequestAsync("search3", parameters, cancellationToken);
            var songs = new List<MusicFile>();

            var searchResult = response?.Element("searchResult3");
            if (searchResult != null)
            {
                foreach (var song in searchResult.Elements("song"))
                {
                    songs.Add(ParseSong(song));
                }
            }

            return songs;
        }

        public async Task<List<MusicFile>> GetRandomSongsAsync(int count = 50, CancellationToken cancellationToken = default)
        {
            var parameters = new Dictionary<string, string>
            {
                { "size", count.ToString() }
            };

            var response = await MakeRequestAsync("getRandomSongs", parameters, cancellationToken);
            var songs = new List<MusicFile>();

            if (response != null)
            {
                foreach (var song in response.Elements("song"))
                {
                    songs.Add(ParseSong(song));
                }
            }

            return songs;
        }

        public string GetStreamUrl(string songId)
        {
            var salt = Guid.NewGuid().ToString("N").Substring(0, 8);
            var tokenHash = GenerateToken(GetPlainPassword(), salt);

            return $"{_source.Url.TrimEnd('/')}/rest/stream" +
                   $"?u={_source.Username}" +
                   $"&t={tokenHash}" +
                   $"&s={salt}" +
                   $"&c={_clientName}" +
                   $"&v={_apiVersion}" +
                   $"&id={songId}";
        }

        public async Task<byte[]> GetCoverArtAsync(string coverArtId, int size = 300, CancellationToken cancellationToken = default)
        {
            var parameters = new Dictionary<string, string>
            {
                { "id", coverArtId },
                { "size", size.ToString() }
            };

            var salt = Guid.NewGuid().ToString("N").Substring(0, 8);
            var tokenHash = GenerateToken(GetPlainPassword(), salt);

            var url = $"{_source.Url.TrimEnd('/')}/rest/getCoverArt" +
                      $"?u={_source.Username}" +
                      $"&t={tokenHash}" +
                      $"&s={salt}" +
                      $"&c={_clientName}" +
                      $"&v={_apiVersion}" +
                      $"&id={coverArtId}" +
                      $"&size={size}";

            try
            {
                var response = await _httpClient.GetAsync(url, cancellationToken);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsByteArrayAsync();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to fetch cover art: {ex.Message}");
                return null;
            }
        }

        public async Task<List<MusicFile>> GetAlbumAsync(string albumId, CancellationToken cancellationToken = default)
        {
            var parameters = new Dictionary<string, string> { { "id", albumId } };
            var response = await MakeRequestAsync("getAlbum", parameters, cancellationToken);
            var songs = new List<MusicFile>();

            var album = response?.Element("album");
            if (album != null)
            {
                foreach (var song in album.Elements("song"))
                {
                    var musicFile = ParseSong(song);
                    var coverAttr = album.Attribute("coverArt")?.Value;
                    if (coverAttr != null)
                        musicFile.CoverPath = $"{_source.Id}:cover:{coverAttr}";
                    songs.Add(musicFile);
                }
            }

            return songs;
        }

        public async Task<List<MusicFile>> GetArtistAsync(string artistId, CancellationToken cancellationToken = default)
        {
            var parameters = new Dictionary<string, string> { { "id", artistId } };
            var response = await MakeRequestAsync("getArtist", parameters, cancellationToken);
            var songs = new List<MusicFile>();

            var artist = response?.Element("artist");
            if (artist != null)
            {
                foreach (var album in artist.Elements("album"))
                {
                    var albumId = album.Attribute("id")?.Value;
                    if (albumId != null)
                    {
                        try
                        {
                            var albumSongs = await GetAlbumAsync(albumId, cancellationToken);
                            songs.AddRange(albumSongs);
                        }
                        catch
                        {
                            // Skip albums that fail to load
                        }
                    }
                }
            }

            return songs;
        }

        public async Task<List<MusicFile>> GetAlbumList2Async(string type = "newest", int size = 50, int offset = 0, CancellationToken cancellationToken = default)
        {
            var parameters = new Dictionary<string, string>
            {
                { "type", type },
                { "size", size.ToString() },
                { "offset", offset.ToString() }
            };

            var response = await MakeRequestAsync("getAlbumList2", parameters, cancellationToken);
            var songs = new List<MusicFile>();

            var albumList = response?.Element("albumList2");
            if (albumList != null)
            {
                foreach (var album in albumList.Elements("album"))
                {
                    var albumId = album.Attribute("id")?.Value;
                    if (albumId != null)
                    {
                        try
                        {
                            var albumSongs = await GetAlbumAsync(albumId, cancellationToken);
                            songs.AddRange(albumSongs);
                        }
                        catch
                        {
                            // Skip albums that fail to load
                        }
                    }
                }
            }

            return songs;
        }

        private MusicFile ParseSong(XElement element)
        {
            var coverAttr = element.Attribute("coverArt")?.Value;
            return new MusicFile
            {
                FilePath = $"{_source.Id}:{element.Attribute("id")?.Value}",
                Title = element.Attribute("title")?.Value ?? "Unknown",
                Artist = element.Attribute("artist")?.Value ?? "Unknown Artist",
                Album = element.Attribute("album")?.Value ?? "Unknown Album",
                Genre = element.Attribute("genre")?.Value ?? "Unknown",
                Duration = ParseDuration(element.Attribute("duration")?.Value),
                Year = int.TryParse(element.Attribute("year")?.Value, out var year) ? year : 0,
                TrackNumber = int.TryParse(element.Attribute("track")?.Value, out var track) ? track : 0,
                SourceType = Subsonic,
                CoverPath = coverAttr != null ? $"{_source.Id}:cover:{coverAttr}" : null
            };
        }

        private static TimeSpan ParseDuration(string durationStr)
        {
            if (int.TryParse(durationStr, out var seconds))
                return TimeSpan.FromSeconds(seconds);
            return TimeSpan.Zero;
        }

        private string GenerateToken(string password, string salt)
        {
            var combined = password + salt;
            using (var md5 = System.Security.Cryptography.MD5.Create())
            {
                var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(combined));
                return BitConverter.ToString(hash).Replace("-", "").ToLower();
            }
        }

        private async Task<XElement> MakeRequestAsync(
            string method,
            Dictionary<string, string> parameters,
            CancellationToken cancellationToken)
        {
            var salt = Guid.NewGuid().ToString("N").Substring(0, 8);
            var tokenHash = GenerateToken(GetPlainPassword(), salt);

            var url = $"{_source.Url.TrimEnd('/')}/rest/{method}" +
                      $"?u={_source.Username}" +
                      $"&t={tokenHash}" +
                      $"&s={salt}" +
                      $"&c={_clientName}" +
                      $"&v={_apiVersion}" +
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
                var response = await _httpClient.GetAsync(url, cancellationToken);
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                var doc = XDocument.Parse(content);
                return doc.Root?.Element("response") ?? doc.Root;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"API request failed: {ex.Message}", ex);
            }
        }

        private void ValidateServerUrl()
        {
            if (Uri.TryCreate(_source.Url, UriKind.Absolute, out var uri))
            {
                bool isLocalhost = uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
                                   uri.Host.Equals("127.0.0.1") ||
                                   uri.Host.Equals("::1");
                if (!isLocalhost && !string.Equals(uri.Scheme, "https", StringComparison.OrdinalIgnoreCase))
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"WARNING: Subsonic server URL '{_source.Url}' does not use HTTPS. " +
                        "Authentication tokens will be sent in plaintext over the network.");
                }
            }
        }

        private string GetPlainPassword()
        {
            if (_password == null) return string.Empty;
            IntPtr ptr = Marshal.SecureStringToBSTR(_password);
            try
            {
                return Marshal.PtrToStringBSTR(ptr);
            }
            finally
            {
                Marshal.ZeroFreeBSTR(ptr);
            }
        }

        public void Dispose()
        {
            _password?.Dispose();
            if (_ownsHttpClient)
                _httpClient?.Dispose();
        }
    }
}

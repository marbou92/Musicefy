using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Musicefy.Core.Interfaces;
using Musicefy.Core.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Musicefy.Core.Services
{
    public class YouTubeSourceProvider : IMusicSourceProvider
    {
        public string SourceType => "YouTube";
        public string DisplayName => "YouTube Music";
        public string Description => "Search and play music from YouTube";
        public string IconGlyph => "▶️";

        public IReadOnlyList<SourceConfigField> ConfigurationFields { get; } = new List<SourceConfigField>
        {
            new SourceConfigField
            {
                Key = "apiKey",
                Label = "YouTube API Key (optional)",
                Description = "Google API key for higher search quotas. Leave empty for anonymous mode.",
                IsRequired = false,
                IsPassword = true,
                Placeholder = "AIzaSy..."
            }
        };

        public Task<bool> TestConnectionAsync(IReadOnlyDictionary<string, string> config)
        {
            return Task.FromResult(true);
        }

        public IMusicSourceSession CreateSession(IReadOnlyDictionary<string, string> config, string sourceId)
        {
            return new YouTubeSourceSession(config, sourceId);
        }

        private static string GetConfig(IReadOnlyDictionary<string, string> config, string key)
        {
            return config.TryGetValue(key, out var val) ? val ?? "" : "";
        }

        private class YouTubeSourceSession : IMusicSourceSession
        {
            private readonly HttpClient _httpClient;
            private readonly string _apiKey;
            private readonly string _sourceId;
            private const string YtmBaseUrl = "https://www.youtube.com/youtubei/v1";

            static YouTubeSourceSession()
            {
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            }

            public YouTubeSourceSession(IReadOnlyDictionary<string, string> config, string sourceId)
            {
                _sourceId = sourceId;
                _apiKey = GetConfig(config, "apiKey");
                _httpClient = new HttpClient();
                _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
            }

            public async Task<IReadOnlyList<MusicFile>> SearchAsync(string query, int limit = 50)
            {
                if (string.IsNullOrWhiteSpace(query))
                    return new List<MusicFile>();

                if (!string.IsNullOrEmpty(_apiKey))
                    return await SearchWithApiKeyAsync(query, limit);
                else
                    return await SearchWithInnerTubeAsync(query, limit);
            }

            private async Task<IReadOnlyList<MusicFile>> SearchWithApiKeyAsync(string query, int limit)
            {
                try
                {
                    var url = $"https://www.googleapis.com/youtube/v3/search" +
                              $"?part=snippet" +
                              $"&q={Uri.EscapeDataString(query)}" +
                              $"&type=video" +
                              $"&videoCategoryId=10" +
                              $"&maxResults={limit}" +
                              $"&key={_apiKey}";

                    var response = await _httpClient.GetStringAsync(url);
                    var data = JObject.Parse(response);
                    var items = data["items"] as JArray;

                    if (items == null) return new List<MusicFile>();

                    return items.Select(item => new MusicFile
                    {
                        FilePath = $"{_sourceId}:{item["id"]?["videoId"]?.ToString()}",
                        Title = item["snippet"]?["title"]?.ToString() ?? "Unknown",
                        Artist = item["snippet"]?["channelTitle"]?.ToString() ?? "YouTube",
                        Album = "YouTube Music",
                        Genre = "Music",
                        SourceType = "YouTube",
                        CoverPath = item["snippet"]?["thumbnails"]?["high"]?["url"]?.ToString()
                    }).ToList();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"YouTube API search failed: {ex.Message}");
                    return new List<MusicFile>();
                }
            }

            private async Task<IReadOnlyList<MusicFile>> SearchWithInnerTubeAsync(string query, int limit)
            {
                try
                {
                    var endpoint = $"{YtmBaseUrl}/search";

                    var payload = new
                    {
                        context = new
                        {
                            client = new
                            {
                                clientName = "ANDROID",
                                clientVersion = "17.31.35",
                                androidSdkVersion = 31
                            }
                        },
                        query = query
                    };

                    var jsonPayload = JsonConvert.SerializeObject(payload);
                    var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                    var response = await _httpClient.PostAsync(endpoint, content);
                    response.EnsureSuccessStatusCode();

                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    var data = JObject.Parse(jsonResponse);
                    var results = ParseInnerTubeResults(data, limit);

                    return results;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"InnerTube search failed: {ex.Message}");
                    return new List<MusicFile>();
                }
            }

            private List<MusicFile> ParseInnerTubeResults(JObject data, int limit)
            {
                var results = new List<MusicFile>();

                try
                {
                    var contents = data["contents"]?["twoColumnSearchResultsRenderer"]?
                        ["primaryContents"]?["sectionListRenderer"]?["contents"];

                    if (contents == null) return results;

                    foreach (var section in contents)
                    {
                        var items = section["itemSectionRenderer"]?["contents"];
                        if (items == null) continue;

                        foreach (var item in items)
                        {
                            if (results.Count >= limit) break;

                            var video = item["videoRenderer"];
                            if (video == null) continue;

                            var videoId = video["videoId"]?.ToString();
                            if (string.IsNullOrEmpty(videoId)) continue;

                            var title = video["title"]?["runs"]?.FirstOrDefault()?["text"]?.ToString()
                                        ?? video["title"]?["simpleText"]?.ToString() ?? "Unknown";

                            var artist = video["ownerText"]?["runs"]?.FirstOrDefault()?["text"]?.ToString()
                                         ?? "YouTube";

                            var thumbnail = video["thumbnail"]?["thumbnails"]?.LastOrDefault()?["url"]?.ToString();

                            results.Add(new MusicFile
                            {
                                FilePath = $"{_sourceId}:{videoId}",
                                Title = title,
                                Artist = artist,
                                Album = "YouTube Music",
                                Genre = "Music",
                                SourceType = "YouTube",
                                CoverPath = thumbnail
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to parse InnerTube results: {ex.Message}");
                }

                return results;
            }

            public async Task<string> GetStreamUrlAsync(string trackId)
            {
                try
                {
                    var endpoint = $"{YtmBaseUrl}/player";

                    var payload = new
                    {
                        context = new
                        {
                            client = new
                            {
                                clientName = "ANDROID",
                                clientVersion = "17.31.35",
                                androidSdkVersion = 31
                            }
                        },
                        videoId = trackId
                    };

                    var jsonPayload = JsonConvert.SerializeObject(payload);
                    var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                    var response = await _httpClient.PostAsync(endpoint, content);
                    response.EnsureSuccessStatusCode();

                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    var data = JObject.Parse(jsonResponse);

                    var streamingData = data["streamingData"];
                    if (streamingData == null) return null;

                    var formats = streamingData["adaptiveFormats"] as JArray;
                    if (formats == null)
                        formats = streamingData["formats"] as JArray;
                    if (formats == null) return null;

                    JToken bestAudio = null;
                    int bestBitrate = -1;
                    foreach (var fmt in formats)
                    {
                        var mimeType = fmt["mimeType"]?.ToString() ?? "";
                        if (mimeType.StartsWith("audio/mp4"))
                        {
                            int bitrate = fmt["bitrate"]?.Value<int>() ?? 0;
                            if (bitrate > bestBitrate)
                            {
                                bestBitrate = bitrate;
                                bestAudio = fmt;
                            }
                        }
                    }

                    if (bestAudio == null && formats.Count > 0)
                        bestAudio = formats[0];

                    if (bestAudio == null) return null;

                    return bestAudio["url"]?.ToString();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to get YouTube stream URL: {ex.Message}");
                    return null;
                }
            }

            public async Task<byte[]> GetCoverArtAsync(string coverId)
            {
                if (string.IsNullOrEmpty(coverId))
                    return null;

                try
                {
                    var response = await _httpClient.GetAsync(coverId);
                    response.EnsureSuccessStatusCode();
                    return await response.Content.ReadAsByteArrayAsync();
                }
                catch
                {
                    return null;
                }
            }

            public async Task<IReadOnlyList<MusicFile>> GetRandomSongsAsync(int count = 50)
            {
                var queries = new[] { "popular music", "top hits", "new songs", "music mix" };
                var random = new Random();
                var query = queries[random.Next(queries.Length)];
                return await SearchAsync(query, count);
            }

            public void Dispose()
            {
                _httpClient?.Dispose();
            }
        }
    }
}

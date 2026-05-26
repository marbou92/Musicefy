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
                _httpClient.DefaultRequestHeaders.Add("User-Agent",
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/125.0.0.0 Safari/537.36");
                _httpClient.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");
                _httpClient.DefaultRequestHeaders.Add("Origin", "https://www.youtube.com");
            }

            public async Task<IReadOnlyList<MusicFile>> SearchAsync(string query, int limit = 50)
            {
                if (string.IsNullOrWhiteSpace(query))
                    return new List<MusicFile>();

                return await SearchWithInnerTubeAsync(query, limit);
            }

            private async Task<IReadOnlyList<MusicFile>> SearchWithInnerTubeAsync(string query, int limit)
            {
                try
                {
                    var endpoint = $"{YtmBaseUrl}/search?prettyPrint=false";

                    var payload = new
                    {
                        context = new
                        {
                            client = new
                            {
                                clientName = "WEB",
                                clientVersion = "2.20250101.00.00",
                                hl = "en",
                                gl = "US"
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
                    var results = ParseSearchResults(data, limit);

                    return results;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"InnerTube search failed: {ex.Message}");
                    return new List<MusicFile>();
                }
            }

            private List<MusicFile> ParseSearchResults(JObject data, int limit)
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

                            var video = ParseVideoItem(item);
                            if (video != null)
                                results.Add(video);
                        }

                        if (results.Count >= limit) break;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to parse search results: {ex.Message}");
                }

                return results;
            }

            private MusicFile ParseVideoItem(JToken item)
            {
                var videoRenderer = item["videoRenderer"];
                if (videoRenderer == null) return null;

                var videoId = videoRenderer["videoId"]?.ToString();
                if (string.IsNullOrEmpty(videoId) || videoId == "undefined") return null;

                var title = videoRenderer["title"]?["runs"]?.FirstOrDefault()?["text"]?.ToString()
                            ?? videoRenderer["title"]?["simpleText"]?.ToString() ?? "Unknown";

                var artist = videoRenderer["ownerText"]?["runs"]?.FirstOrDefault()?["text"]?.ToString()
                             ?? "YouTube";

                var thumbnail = videoRenderer["thumbnail"]?["thumbnails"]?.LastOrDefault()?["url"]?.ToString();

                var lengthStr = videoRenderer["lengthText"]?["simpleText"]?.ToString() ?? "0:00";
                var duration = ParseDuration(lengthStr);

                return new MusicFile
                {
                    FilePath = $"{_sourceId}:{videoId}",
                    Title = title,
                    Artist = artist,
                    Album = "YouTube Music",
                    Genre = "Music",
                    SourceType = "YouTube",
                    CoverPath = thumbnail,
                    Duration = duration
                };
            }

            private static TimeSpan ParseDuration(string length)
            {
                try
                {
                    var parts = length.Split(':').Select(int.Parse).ToArray();
                    if (parts.Length == 2)
                        return TimeSpan.FromMinutes(parts[0]) + TimeSpan.FromSeconds(parts[1]);
                    if (parts.Length == 3)
                        return TimeSpan.FromHours(parts[0]) + TimeSpan.FromMinutes(parts[1]) + TimeSpan.FromSeconds(parts[2]);
                }
                catch { }
                return TimeSpan.Zero;
            }

            public async Task<string> GetStreamUrlAsync(string trackId)
            {
                try
                {
                    var url = await GetStreamWithPlayerEndpoint(trackId);
                    if (url != null) return url;

                    url = await GetStreamWithWatchPage(trackId);
                    if (url != null) return url;

                    return null;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to get YouTube stream URL: {ex.Message}");
                    return null;
                }
            }

            private async Task<string> GetStreamWithPlayerEndpoint(string trackId)
            {
                try
                {
                    var endpoint = $"{YtmBaseUrl}/player?prettyPrint=false";

                    var payload = new
                    {
                        context = new
                        {
                            client = new
                            {
                                clientName = "WEB",
                                clientVersion = "2.20250101.00.00",
                                hl = "en",
                                gl = "US"
                            },
                            thirdParty = new { }
                        },
                        videoId = trackId,
                        contentCheckOk = true,
                        racyCheckOk = true
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
                        if (mimeType.Contains("audio/mp4") || mimeType.Contains("audio/webm"))
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

                    var streamUrl = bestAudio["url"]?.ToString();
                    if (!string.IsNullOrEmpty(streamUrl))
                        return streamUrl;

                    var cipher = bestAudio["cipher"]?.ToString()
                                 ?? bestAudio["signatureCipher"]?.ToString();
                    if (!string.IsNullOrEmpty(cipher))
                    {
                        var parts = cipher.Split('&');
                        foreach (var part in parts)
                        {
                            var kv = part.Split(new[] { '=' }, 2);
                            if (kv.Length == 2 && kv[0] == "url")
                                streamUrl = Uri.UnescapeDataString(kv[1]);
                        }
                        if (!string.IsNullOrEmpty(streamUrl))
                            return streamUrl;
                    }

                    return null;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Player endpoint failed: {ex.Message}");
                    return null;
                }
            }

            private async Task<string> GetStreamWithWatchPage(string trackId)
            {
                try
                {
                    var watchUrl = $"https://www.youtube.com/watch?v={trackId}";
                    var request = new HttpRequestMessage(HttpMethod.Get, watchUrl);
                    request.Headers.Add("User-Agent",
                        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/125.0.0.0 Safari/537.36");

                    var response = await _httpClient.SendAsync(request);
                    response.EnsureSuccessStatusCode();

                    var html = await response.Content.ReadAsStringAsync();

                    var ytInitialData = ExtractJsonFromHtml(html, "var ytInitialPlayerResponse = ");
                    if (string.IsNullOrEmpty(ytInitialData))
                        ytInitialData = ExtractJsonFromHtml(html, "window.ytInitialPlayerResponse = ");

                    if (string.IsNullOrEmpty(ytInitialData)) return null;

                    var data = JObject.Parse(ytInitialData);
                    var streamingData = data["streamingData"];
                    if (streamingData == null) return null;

                    var formats = streamingData["formats"] as JArray;
                    var adaptiveFormats = streamingData["adaptiveFormats"] as JArray;

                    JToken bestAudio = null;
                    int bestBitrate = -1;

                    if (adaptiveFormats != null)
                    {
                        foreach (var fmt in adaptiveFormats)
                        {
                            var mimeType = fmt["mimeType"]?.ToString() ?? "";
                            if (mimeType.Contains("audio/mp4") || mimeType.Contains("audio/webm"))
                            {
                                int bitrate = fmt["bitrate"]?.Value<int>() ?? 0;
                                if (bitrate > bestBitrate)
                                {
                                    bestBitrate = bitrate;
                                    bestAudio = fmt;
                                }
                            }
                        }
                    }

                    if (bestAudio == null && formats != null && formats.Count > 0)
                        bestAudio = formats[0];

                    if (bestAudio == null) return null;

                    var url = bestAudio["url"]?.ToString();
                    if (!string.IsNullOrEmpty(url))
                        return url;

                    return null;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Watch page fallback failed: {ex.Message}");
                    return null;
                }
            }

            private static string ExtractJsonFromHtml(string html, string prefix)
            {
                var start = html.IndexOf(prefix);
                if (start < 0) return null;

                start += prefix.Length;
                var braceStart = html.IndexOf('{', start);
                if (braceStart < 0) return null;

                int depth = 0;
                int end = braceStart;
                for (int i = braceStart; i < html.Length; i++)
                {
                    if (html[i] == '{') depth++;
                    else if (html[i] == '}') depth--;
                    if (depth == 0) { end = i + 1; break; }
                }

                return html.Substring(braceStart, end - braceStart);
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
                var queries = new[] { "popular music", "top hits", "new songs", "music mix", "trending music" };
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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Musicefy.Core.Interfaces;
using Musicefy.Core.Models;
using YoutubeExplode;
using YoutubeExplode.Search;
using YoutubeExplode.Videos.Streams;

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
            return new YouTubeSourceSession(sourceId);
        }

        private class YouTubeSourceSession : IMusicSourceSession
        {
            private readonly YoutubeClient _youtube;
            private readonly string _sourceId;

            public YouTubeSourceSession(string sourceId)
            {
                _sourceId = sourceId;
                _youtube = new YoutubeClient();
            }

            public async Task<IReadOnlyList<MusicFile>> SearchAsync(string query, int limit = 50)
            {
                var results = new List<MusicFile>();
                try
                {
                    await foreach (var result in _youtube.Search.GetResultsAsync(query))
                    {
                        if (results.Count >= limit) break;

                        if (result is VideoSearchResult video)
                        {
                            results.Add(new MusicFile
                            {
                                FilePath = $"{_sourceId}:{video.Id}",
                                Title = video.Title,
                                Artist = video.Author?.ChannelTitle ?? "YouTube",
                                Album = "YouTube Music",
                                Genre = "Music",
                                SourceType = "YouTube",
                                CoverPath = video.Thumbnails?.FirstOrDefault()?.Url,
                                Duration = video.Duration ?? TimeSpan.Zero
                            });
                        }
                    }
                }
                catch
                {
                    // Search failed — return whatever we have
                }
                return results;
            }

            public async Task<string> GetStreamUrlAsync(string trackId)
            {
                try
                {
                    var manifest = await _youtube.Videos.Streams.GetManifestAsync(trackId);
                    var audioStream = manifest.GetAudioOnlyStreams()
                        .OrderByDescending(s => s.Bitrate)
                        .FirstOrDefault();

                    return audioStream?.Url?.ToString();
                }
                catch
                {
                    return null;
                }
            }

            public async Task<byte[]> GetCoverArtAsync(string coverId)
            {
                if (string.IsNullOrEmpty(coverId))
                    return null;

                try
                {
                    using var client = new System.Net.Http.HttpClient();
                    var response = await client.GetAsync(coverId);
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
                var shuffled = queries.OrderBy(_ => random.Next()).Take(3).ToArray();
                var tasks = shuffled.Select(q => SearchAsync(q, count / shuffled.Length));
                var results = await Task.WhenAll(tasks);
                return results.SelectMany(r => r).Distinct().Take(count).ToList();
            }

            public void Dispose()
            {
                _youtube?.Dispose();
            }
        }
    }
}

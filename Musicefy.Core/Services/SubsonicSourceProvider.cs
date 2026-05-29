using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Musicefy.Core.Interfaces;
using Musicefy.Core.Models;
using static Musicefy.Core.SourceTypes;

namespace Musicefy.Core.Services
{
    public class SubsonicSourceProvider : IMusicSourceProvider
    {
        public string SourceType => Subsonic;
        public string DisplayName => "Subsonic API";
        public string Description => "Subsonic-compatible server (Navidrome, Airsonic, Squidify, etc.)";
        public string IconGlyph => "🔗";

        public IReadOnlyList<SourceConfigField> ConfigurationFields { get; } = new List<SourceConfigField>
        {
            new SourceConfigField
            {
                Key = "url",
                Label = "Server URL",
                Description = "Base URL of your Subsonic-compatible server",
                IsRequired = true,
                Placeholder = "https://music.example.com"
            },
            new SourceConfigField
            {
                Key = "username",
                Label = "Username",
                Description = "Your Subsonic account username",
                IsRequired = true
            },
            new SourceConfigField
            {
                Key = "password",
                Label = "Password",
                Description = "Your Subsonic account password",
                IsRequired = true,
                IsPassword = true
            }
        };

        public Task<bool> TestConnectionAsync(IReadOnlyDictionary<string, string> config)
        {
            var source = CreateSource(config);
            using (var client = new SubsonicClientImpl(source))
            {
                return client.TestConnectionAsync();
            }
        }

        public IMusicSourceSession CreateSession(IReadOnlyDictionary<string, string> config, string sourceId)
        {
            return new SubsonicSourceSession(config, sourceId);
        }

        private static StreamingSource CreateSource(IReadOnlyDictionary<string, string> config)
        {
            return new StreamingSource
            {
                Id = Guid.NewGuid().ToString(),
                Type = Subsonic,
                Url = GetConfig(config, "url"),
                Username = GetConfig(config, "username"),
                Password = GetConfig(config, "password")
            };
        }

        private static string GetConfig(IReadOnlyDictionary<string, string> config, string key)
        {
            return config.TryGetValue(key, out var val) ? val ?? "" : "";
        }

        private class SubsonicSourceSession : IMusicSourceSession
        {
            private readonly SubsonicClientImpl _client;
            private readonly string _sourceId;

            public SubsonicSourceSession(IReadOnlyDictionary<string, string> config, string sourceId)
            {
                _sourceId = sourceId;
                var source = new StreamingSource
                {
                    Id = sourceId,
                    Type = Subsonic,
                    Url = GetConfig(config, "url"),
                    Username = GetConfig(config, "username"),
                    Password = GetConfig(config, "password")
                };
                _client = new SubsonicClientImpl(source);
            }

            public async Task<IReadOnlyList<MusicFile>> SearchAsync(string query, int limit = 50)
            {
                return await _client.SearchAsync(query, limit);
            }

            public Task<string> GetStreamUrlAsync(string trackId)
            {
                return Task.FromResult(_client.GetStreamUrl(trackId));
            }

            public async Task<byte[]> GetCoverArtAsync(string coverId)
            {
                if (string.IsNullOrEmpty(coverId))
                    return null;

                var actualId = coverId;
                var prefix = $"{_sourceId}:cover:";
                if (actualId.StartsWith(prefix))
                    actualId = actualId.Substring(prefix.Length);

                return await _client.GetCoverArtAsync(actualId);
            }

            public async Task<IReadOnlyList<MusicFile>> GetRandomSongsAsync(int count = 50)
            {
                return await _client.GetRandomSongsAsync(count);
            }

            public async Task<IReadOnlyList<MusicFile>> GetAlbumListAsync(int count = 50)
            {
                return await _client.GetAlbumList2Async("newest", count);
            }

            public async Task<IReadOnlyList<MusicFile>> GetAlbumAsync(string albumId)
            {
                return await _client.GetAlbumAsync(albumId);
            }

            public async Task<IReadOnlyList<MusicFile>> GetArtistAsync(string artistId)
            {
                return await _client.GetArtistAsync(artistId);
            }

            public void Dispose()
            {
                _client?.Dispose();
            }

            private static string GetConfig(IReadOnlyDictionary<string, string> config, string key)
            {
                return config.TryGetValue(key, out var val) ? val ?? "" : "";
            }
        }
    }
}

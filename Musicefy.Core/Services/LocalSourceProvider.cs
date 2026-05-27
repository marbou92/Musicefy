using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Musicefy.Core.Interfaces;
using Musicefy.Core.Models;
using static Musicefy.Core.SourceTypes;

namespace Musicefy.Core.Services
{
    public class LocalSourceProvider : IMusicSourceProvider
    {
        public string SourceType => Local;
        public string DisplayName => "Local Folder";
        public string Description => "Music files stored on your local disk";
        public string IconGlyph => "💻";

        public IReadOnlyList<SourceConfigField> ConfigurationFields { get; } = new List<SourceConfigField>
        {
            new SourceConfigField
            {
                Key = "folderPath",
                Label = "Folder Path",
                Description = "Path to a local folder containing music files",
                IsRequired = true,
                Placeholder = @"C:\Users\You\Music"
            }
        };

        public Task<bool> TestConnectionAsync(IReadOnlyDictionary<string, string> config)
        {
            var path = GetConfig(config, "folderPath");
            return Task.FromResult(!string.IsNullOrEmpty(path) && Directory.Exists(path));
        }

        public IMusicSourceSession CreateSession(IReadOnlyDictionary<string, string> config, string sourceId)
        {
            return new LocalSourceSession(config, sourceId);
        }

        private static string GetConfig(IReadOnlyDictionary<string, string> config, string key)
        {
            return config.TryGetValue(key, out var val) ? val ?? "" : "";
        }

        private class LocalSourceSession : IMusicSourceSession
        {
            private readonly string _folderPath;
            private readonly string _sourceId;
            private static readonly HashSet<string> _extensions = MusicFileExtensions.All;

            public LocalSourceSession(IReadOnlyDictionary<string, string> config, string sourceId)
            {
                _sourceId = sourceId;
                _folderPath = GetConfig(config, "folderPath");
            }

            public Task<IReadOnlyList<MusicFile>> SearchAsync(string query, int limit = 50)
            {
                var results = new List<MusicFile>();

                if (!Directory.Exists(_folderPath))
                    return Task.FromResult<IReadOnlyList<MusicFile>>(results);

                var files = Directory.EnumerateFiles(_folderPath, "*.*", SearchOption.AllDirectories)
                    .Where(f => _extensions.Contains(System.IO.Path.GetExtension(f)));

                foreach (var file in files)
                {
                    if (results.Count >= limit)
                        break;

                    var name = Path.GetFileNameWithoutExtension(file);
                    if (string.IsNullOrEmpty(query) || name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        results.Add(new MusicFile
                        {
                            Title = name,
                            Artist = "Local",
                            FilePath = file,
                            SourceUri = file,
                            SourceType = Local
                        });
                    }
                }

                return Task.FromResult<IReadOnlyList<MusicFile>>(results);
            }

            public Task<string> GetStreamUrlAsync(string trackId)
            {
                return Task.FromResult(trackId);
            }

            public Task<byte[]> GetCoverArtAsync(string coverId)
            {
                return Task.FromResult<byte[]>(null);
            }

            public Task<IReadOnlyList<MusicFile>> GetRandomSongsAsync(int count = 50)
            {
                var results = new List<MusicFile>();

                if (!Directory.Exists(_folderPath))
                    return Task.FromResult<IReadOnlyList<MusicFile>>(results);

                var files = Directory.EnumerateFiles(_folderPath, "*.*", SearchOption.AllDirectories)
                    .Where(f => _extensions.Any(e => f.EndsWith(e, StringComparison.OrdinalIgnoreCase)))
                    .OrderBy(_ => Guid.NewGuid())
                    .Take(count);

                foreach (var file in files)
                {
                    results.Add(new MusicFile
                    {
                        Title = Path.GetFileNameWithoutExtension(file),
                        Artist = "Local",
                        FilePath = file,
                        SourceUri = file,
                        SourceType = Local
                    });
                }

                return Task.FromResult<IReadOnlyList<MusicFile>>(results);
            }

            public void Dispose()
            {
            }
        }
    }
}

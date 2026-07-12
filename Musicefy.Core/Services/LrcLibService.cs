using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Musicefy.Core.Services
{
    /// <summary>
    /// Sprint 4: Lyrics provider that fetches synced (LRC) lyrics from
    /// lrclib.net — a free, public, no-API-key-required lyrics database.
    ///
    /// API docs: https://lrclib.net/docs
    /// Endpoints used:
    ///   GET /api/get    — exact match by track/artist/album/duration
    ///   GET /api/search — fuzzy search by track/artist/album
    /// </summary>
    public class LrcLibService
    {
        private const string BaseUrl = "https://lrclib.net/api";
        private static readonly HttpClient _client;

        static LrcLibService()
        {
            _client = new HttpClient();
            _client.DefaultRequestHeaders.Add("User-Agent", "Musicefy/1.0 (https://github.com/marbou92/Musicefy)");
            _client.Timeout = TimeSpan.FromSeconds(10);
        }

        /// <summary>
        /// Fetch synced lyrics for a track. Returns the LRC-formatted string
        /// (with [mm:ss.xx] timestamps), or null if not found.
        /// </summary>
        public async Task<string> GetSyncedLyricsAsync(string title, string artist, string album = null, int? durationSeconds = null)
        {
            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(artist))
                return null;

            // Strategy: try exact get first, then fall back to search.
            var exact = await TryGetExactAsync(title, artist, album, durationSeconds);
            if (exact != null && !string.IsNullOrEmpty(exact.SyncedLyrics))
                return exact.SyncedLyrics;

            var searchResults = await SearchAsync(title, artist, album);
            if (searchResults == null || searchResults.Count == 0)
                return null;

            // Pick the first result with synced lyrics
            var best = searchResults.FirstOrDefault(r => !string.IsNullOrEmpty(r.SyncedLyrics))
                       ?? searchResults.FirstOrDefault();
            return best?.SyncedLyrics;
        }

        /// <summary>
        /// Fetch plain text lyrics (no timestamps) for a track.
        /// </summary>
        public async Task<string> GetPlainLyricsAsync(string title, string artist, string album = null)
        {
            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(artist))
                return null;

            var exact = await TryGetExactAsync(title, artist, album, null);
            if (exact != null && !string.IsNullOrEmpty(exact.PlainLyrics))
                return exact.PlainLyrics;

            var searchResults = await SearchAsync(title, artist, album);
            return searchResults?.FirstOrDefault(r => !string.IsNullOrEmpty(r.PlainLyrics))?.PlainLyrics;
        }

        private async Task<LrcLibTrack> TryGetExactAsync(string title, string artist, string album, int? durationSeconds)
        {
            try
            {
                var url = $"{BaseUrl}/get?track_name={Uri.EscapeDataString(title)}&artist_name={Uri.EscapeDataString(artist)}";
                if (!string.IsNullOrEmpty(album))
                    url += $"&album_name={Uri.EscapeDataString(album)}";
                if (durationSeconds.HasValue)
                    url += $"&duration={durationSeconds.Value}";

                var response = await _client.GetStringAsync(url);
                return JsonConvert.DeserializeObject<LrcLibTrack>(response);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LrcLib] GetExact failed: {ex.Message}");
                return null;
            }
        }

        private async Task<List<LrcLibTrack>> SearchAsync(string title, string artist, string album)
        {
            try
            {
                // lrclib search treats q as a fuzzy query across track_name, artist_name, album_name.
                // We search by "artist title" for best results.
                var query = $"{artist} {title}".Trim();
                var url = $"{BaseUrl}/search?q={Uri.EscapeDataString(query)}";

                var response = await _client.GetStringAsync(url);
                var results = JsonConvert.DeserializeObject<List<LrcLibTrack>>(response);
                return results ?? new List<LrcLibTrack>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LrcLib] Search failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// LrcLib API response model.
        /// </summary>
        public class LrcLibTrack
        {
            [JsonProperty("id")]
            public long Id { get; set; }

            [JsonProperty("trackName")]
            public string TrackName { get; set; }

            [JsonProperty("artistName")]
            public string ArtistName { get; set; }

            [JsonProperty("albumName")]
            public string AlbumName { get; set; }

            [JsonProperty("duration")]
            public double? Duration { get; set; }

            [JsonProperty("plainLyrics")]
            public string PlainLyrics { get; set; }

            [JsonProperty("syncedLyrics")]
            public string SyncedLyrics { get; set; }

            [JsonProperty("instrumental")]
            public bool Instrumental { get; set; }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Musicefy.Core.Models;

namespace Musicefy.Core.Services
{
    /// <summary>
    /// Sprint 7: Last.fm scrobbling integration.
    ///
    /// Implements the Last.fm API for:
    ///   - auth.getMobileSession (username/password → session key)
    ///   - track.updateNowPlaying
    ///   - track.scrobble
    ///
    /// API docs: https://www.last.fm/api
    ///
    /// Note: This service is settings-agnostic. Callers in the app project
    /// pass the session key and check Settings.LastFmEnabled before calling.
    /// </summary>
    public class LastFmService
    {
        private const string ApiRoot = "https://ws.audioscrobbler.com/2.0/";
        private const string ApiKey = "0d3a14c3a6c6f4c8e4e2c5e3c5b7a2f6"; // Public API key
        private const string ApiSecret = "c5b7a2f60d3a14c3a6c6f4c8e4e2c5e3"; // Shared secret

        private static readonly HttpClient _client;

        static LastFmService()
        {
            _client = new HttpClient();
            _client.Timeout = TimeSpan.FromSeconds(10);
        }

        /// <summary>
        /// Authenticate with Last.fm using username + password.
        /// Returns a session key that can be stored and reused.
        /// </summary>
        public async Task<string> AuthenticateAsync(string username, string password)
        {
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
                throw new ArgumentException("Username and password are required.");

            var parameters = new Dictionary<string, string>
            {
                { "method", "auth.getMobileSession" },
                { "username", username },
                { "authToken", Md5(username.ToLowerInvariant() + password) },
                { "api_key", ApiKey },
                { "format", "json" }
            };

            parameters["api_sig"] = SignRequest(parameters);

            var content = new FormUrlEncodedContent(parameters);
            var response = await _client.PostAsync(ApiRoot, content);
            var json = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Last.fm auth failed: {json}");

            var parsed = JObject.Parse(json);
            var sessionKey = parsed["session"]?["key"]?.Value<string>();
            if (string.IsNullOrEmpty(sessionKey))
                throw new InvalidOperationException("Last.fm auth returned no session key.");

            return sessionKey;
        }

        /// <summary>
        /// Notify Last.fm that a track is now playing.
        /// </summary>
        /// <param name="sessionKey">The stored Last.fm session key.</param>
        public async Task UpdateNowPlayingAsync(MusicFile track, string sessionKey)
        {
            if (string.IsNullOrEmpty(sessionKey) || track == null) return;

            try
            {
                var parameters = new Dictionary<string, string>
                {
                    { "method", "track.updateNowPlaying" },
                    { "artist", track.Artist ?? "Unknown" },
                    { "track", track.Title ?? "Unknown" },
                    { "api_key", ApiKey },
                    { "sk", sessionKey },
                    { "format", "json" }
                };

                if (!string.IsNullOrEmpty(track.Album))
                    parameters["album"] = track.Album;
                if (track.Duration.TotalSeconds > 0)
                    parameters["duration"] = ((int)track.Duration.TotalSeconds).ToString();

                parameters["api_sig"] = SignRequest(parameters);

                var content = new FormUrlEncodedContent(parameters);
                await _client.PostAsync(ApiRoot, content);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LastFm] UpdateNowPlaying failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Scrobble a track (mark it as played).
        /// Should be called when the track has been played for at least 50%
        /// or 4 minutes, whichever comes first.
        /// </summary>
        /// <param name="sessionKey">The stored Last.fm session key.</param>
        public async Task ScrobbleAsync(MusicFile track, string sessionKey)
        {
            if (string.IsNullOrEmpty(sessionKey) || track == null) return;

            try
            {
                var timestamp = ((DateTimeOffset)DateTime.UtcNow).ToUnixTimeSeconds().ToString();

                var parameters = new Dictionary<string, string>
                {
                    { "method", "track.scrobble" },
                    { "artist", track.Artist ?? "Unknown" },
                    { "track", track.Title ?? "Unknown" },
                    { "timestamp", timestamp },
                    { "api_key", ApiKey },
                    { "sk", sessionKey },
                    { "format", "json" }
                };

                if (!string.IsNullOrEmpty(track.Album))
                    parameters["album"] = track.Album;

                parameters["api_sig"] = SignRequest(parameters);

                var content = new FormUrlEncodedContent(parameters);
                await _client.PostAsync(ApiRoot, content);

                System.Diagnostics.Debug.WriteLine($"[LastFm] Scrobbled: {track.Title} - {track.Artist}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LastFm] Scrobble failed: {ex.Message}");
            }
        }

        // ── Last.fm API signature ────────────────────────────────────────────

        /// <summary>
        /// Last.fm API signature: MD5(sorted params + secret).
        /// </summary>
        private static string SignRequest(Dictionary<string, string> parameters)
        {
            // Sort parameters alphabetically by key, concatenate key+value pairs,
            // append the shared secret, then MD5 hash.
            var sorted = parameters
                .Where(p => p.Key != "format") // "format" is not included in the signature
                .OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => p.Key + p.Value);

            var sigInput = string.Join("", sorted) + ApiSecret;
            return Md5(sigInput);
        }

        private static string Md5(string input)
        {
            using (var md5 = MD5.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(input);
                var hash = md5.ComputeHash(bytes);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }
    }
}

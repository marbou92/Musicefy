using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Musicefy.Core.Services.YouTubeApi
{
    /// <summary>
    /// C# implementation of YouTube's InnerTube API client, inspired by Echo Music's architecture.
    /// Communicates directly with YouTube's internal API for search, browse, player, and metadata.
    /// Supports multiple YouTube client impersonations with fallback logic.
    /// </summary>
    public class InnerTubeClient : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly HttpClientHandler _handler;
        private string _visitorData;
        private string _cookie;
        private readonly SemaphoreSlim _initLock = new SemaphoreSlim(1, 1);
        private bool _initialized;

        #region YouTube Client Definitions (inspired by Echo Music)

        /// <summary>
        /// Represents a YouTube client identity that can be impersonated for API requests.
        /// Different clients have different capabilities and access levels.
        /// </summary>
        public class YouTubeClientInfo
        {
            public string ClientName { get; set; }
            public string ClientVersion { get; set; }
            public string ClientId { get; set; }
            public string UserAgent { get; set; }
            public bool LoginSupported { get; set; }
            public bool UseSignatureTimestamp { get; set; }
            public bool IsEmbedded { get; set; }

            // Android VR client — Echo Music's primary client because it uses non-adaptive
            // bitrate (fixes audio stuttering) and doesn't require authentication.
            public static YouTubeClientInfo AndroidVr => new YouTubeClientInfo
            {
                ClientName = "ANDROID_VR",
                ClientVersion = "1.43.32",
                ClientId = "30",
                UserAgent = "com.google.android.apps.youtube.vr.oculus/1.43.32 (Linux; U; Android 12; GB) gzip",
                LoginSupported = false,
                UseSignatureTimestamp = false,
                IsEmbedded = false
            };

            // Web Remix (YouTube Music Web) — primary for metadata and authenticated browsing
            public static YouTubeClientInfo WebRemix => new YouTubeClientInfo
            {
                ClientName = "WEB_REMIX",
                ClientVersion = "1.20240403.01.00",
                ClientId = "67",
                UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36",
                LoginSupported = true,
                UseSignatureTimestamp = true,
                IsEmbedded = false
            };

            // TV HTML5 Simply Embedded — bypasses age restrictions for embedded content
            public static YouTubeClientInfo TvHtml5Embedded => new YouTubeClientInfo
            {
                ClientName = "TVHTML5_SIMPLY_EMBEDDED_PLAYER",
                ClientVersion = "2.0",
                ClientId = "85",
                UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36",
                LoginSupported = false,
                UseSignatureTimestamp = false,
                IsEmbedded = true
            };

            // Android Music client
            public static YouTubeClientInfo AndroidMusic => new YouTubeClientInfo
            {
                ClientName = "ANDROID_MUSIC",
                ClientVersion = "7.11.50",
                ClientId = "21",
                UserAgent = "com.google.android.apps.youtube.music/7.11.50 (Linux; U; Android 14) gzip",
                LoginSupported = true,
                UseSignatureTimestamp = false,
                IsEmbedded = false
            };

            // Web Creator — for age-restricted content when logged in
            public static YouTubeClientInfo WebCreator => new YouTubeClientInfo
            {
                ClientName = "WEB_CREATOR",
                ClientVersion = "1.20240403.01.00",
                ClientId = "62",
                UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36",
                LoginSupported = true,
                UseSignatureTimestamp = true,
                IsEmbedded = false
            };

            // iOS client — another fallback option
            public static YouTubeClientInfo IOS => new YouTubeClientInfo
            {
                ClientName = "IOS",
                ClientVersion = "19.29.1",
                ClientId = "5",
                UserAgent = "com.google.ios.youtube/19.29.1 (iPhone; U; CPU iOS 17_6 like Mac OS X;)",
                LoginSupported = false,
                UseSignatureTimestamp = false,
                IsEmbedded = false
            };

            /// <summary>
            /// Fallback client chain in priority order, inspired by Echo Music's 11-client fallback.
            /// Primary: AndroidVR (no auth, no adaptive bitrate issues)
            /// Fallbacks: AndroidMusic, TvHtml5Embedded (age-restricted), IOS, WebRemix, WebCreator
            /// </summary>
            public static readonly YouTubeClientInfo[] PlaybackFallbackChain = new[]
            {
                AndroidVr,
                AndroidMusic,
                TvHtml5Embedded,
                IOS,
                WebRemix,
                WebCreator
            };
        }

        #endregion

        #region InnerTube API Response Models

        public class SearchResponse
        {
            public List<InnerTubeSearchItem> Items { get; set; } = new List<InnerTubeSearchItem>();
            public string ContinuationToken { get; set; }
        }

        public class InnerTubeSearchItem
        {
            public string VideoId { get; set; }
            public string Title { get; set; }
            public string Artist { get; set; }
            public string Album { get; set; }
            public string PlaylistId { get; set; }
            public string BrowseId { get; set; }
            public string ThumbnailUrl { get; set; }
            public TimeSpan Duration { get; set; }
            public SearchResultType Type { get; set; }
            public string Subtitle { get; set; }
        }

        public enum SearchResultType
        {
            Song,
            Video,
            Album,
            Artist,
            Playlist,
            Unknown
        }

        public class SearchFilter
        {
            public string Value { get; }
            public SearchFilter(string value) { Value = value; }

            // Filters inspired by Echo Music's search filter parameters
            public static readonly SearchFilter Song = new SearchFilter("EgWKAQIIAWoKEAkQBRAKEAMQBA%3D%3D");
            public static readonly SearchFilter Video = new SearchFilter("EgWKAQIQAWoKEAkQChAFEAMQBA%3D%3D");
            public static readonly SearchFilter Album = new SearchFilter("EgWKAQIYAWoKEAkQChAFEAMQBA%3D%3D");
            public static readonly SearchFilter Artist = new SearchFilter("EgWKAQIgAWoKEAkQChAFEAMQBA%3D%3D");
            public static readonly SearchFilter FeaturedPlaylist = new SearchFilter("EgeKAQQoADgBagwQDhAKEAMQBRAJEAQ%3D");
            public static readonly SearchFilter CommunityPlaylist = new SearchFilter("EgeKAQQoAEABagoQAxAEEAoQCRAF");
        }

        public class BrowseResponse
        {
            public string Title { get; set; }
            public string Description { get; set; }
            public string ThumbnailUrl { get; set; }
            public List<InnerTubeSearchItem> Tracks { get; set; } = new List<InnerTubeSearchItem>();
            public string ContinuationToken { get; set; }
        }

        public class SearchSuggestions
        {
            public List<string> Queries { get; set; } = new List<string>();
            public List<InnerTubeSearchItem> RecommendedItems { get; set; } = new List<InnerTubeSearchItem>();
        }

        public class PlayerResponse
        {
            public string Status { get; set; }
            public List<FormatInfo> Formats { get; set; } = new List<FormatInfo>();
            public VideoDetails Details { get; set; }
            public int ExpiresInSeconds { get; set; } = 21540; // ~6 hours default
            public double? LoudnessDb { get; set; }
        }

        public class FormatInfo
        {
            public int Itag { get; set; }
            public string Url { get; set; }
            public string MimeType { get; set; }
            public int Bitrate { get; set; }
            public int? AudioSampleRate { get; set; }
            public string SignatureCipher { get; set; }
            public string Cipher { get; set; }
            public AudioTrackInfo AudioTrack { get; set; }
            public long? ContentLength { get; set; }

            public bool IsAudio => !MimeType.Contains("video/");
            public bool IsOriginal => AudioTrack?.IsAutoDubbed != true;
            public bool IsOpus => MimeType.Contains("audio/webm") || MimeType.Contains("opus");
            public bool IsAac => MimeType.Contains("audio/mp4") || MimeType.Contains("mp4a");
        }

        public class AudioTrackInfo
        {
            public string Language { get; set; }
            public bool IsAutoDubbed { get; set; }
        }

        public class VideoDetails
        {
            public string VideoId { get; set; }
            public string Title { get; set; }
            public string Author { get; set; }
            public long LengthSeconds { get; set; }
            public string ThumbnailUrl { get; set; }
            public string MusicVideoType { get; set; }
        }

        #endregion

        public InnerTubeClient(string cookie = null)
        {
            _cookie = cookie;
            _handler = new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                MaxConnectionsPerServer = 10,
                UseCookies = false
            };
            _httpClient = new HttpClient(_handler)
            {
                Timeout = TimeSpan.FromSeconds(30)
            };
            _httpClient.DefaultRequestHeaders.Add("User-Agent", YouTubeClientInfo.WebRemix.UserAgent);
            _httpClient.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");
        }

        /// <summary>
        /// Initialize the client by fetching a visitor data token.
        /// Inspired by Echo Music's visitorData session management.
        /// </summary>
        public async Task InitializeAsync()
        {
            if (_initialized) return;

            await _initLock.WaitAsync();
            try
            {
                if (_initialized) return;

                try
                {
                    var response = await _httpClient.GetAsync("https://www.youtube.com/");
                    var html = await response.Content.ReadAsStringAsync();

                    // Extract visitor data from the page (like Echo Music does)
                    var match = System.Text.RegularExpressions.Regex.Match(
                        html, @"""visitorData"":\s*""([^""]+)""");
                    if (match.Success)
                        _visitorData = match.Groups[1].Value;
                }
                catch
                {
                    // Non-critical — proceed without visitor data
                }

                _initialized = true;
            }
            finally
            {
                _initLock.Release();
            }
        }

        #region Search (InnerTube API)

        /// <summary>
        /// Search YouTube Music with optional type filter.
        /// Uses InnerTube search endpoint, inspired by Echo Music's search implementation.
        /// </summary>
        public async Task<SearchResponse> SearchAsync(string query, SearchFilter filter = null,
            YouTubeClientInfo client = null, CancellationToken cancellationToken = default)
        {
            await InitializeAsync();
            client = client ?? YouTubeClientInfo.WebRemix;

            var body = BuildSearchBody(query, filter, client);
            var result = await PostAsync("search", body, client, cancellationToken);
            return ParseSearchResponse(result);
        }

        /// <summary>
        /// Get search suggestions for a query.
        /// Inspired by Echo Music's autocomplete implementation.
        /// </summary>
        public async Task<SearchSuggestions> GetSearchSuggestionsAsync(string query,
            YouTubeClientInfo client = null, CancellationToken cancellationToken = default)
        {
            await InitializeAsync();
            client = client ?? YouTubeClientInfo.WebRemix;

            var body = new Dictionary<string, object>
            {
                ["context"] = BuildContext(client),
                ["input"] = query
            };

            try
            {
                var result = await PostAsync("music/get_search_suggestions", body, client, cancellationToken);
                return ParseSearchSuggestions(result);
            }
            catch
            {
                return new SearchSuggestions();
            }
        }

        /// <summary>
        /// Search across all categories (summary), inspired by Echo Music's searchSummary.
        /// Returns grouped results for songs, videos, albums, artists, and playlists.
        /// </summary>
        public async Task<Dictionary<SearchResultType, List<InnerTubeSearchItem>>> SearchSummaryAsync(
            string query, YouTubeClientInfo client = null, CancellationToken cancellationToken = default)
        {
            var response = await SearchAsync(query, null, client, cancellationToken);
            var grouped = response.Items
                .GroupBy(i => i.Type)
                .ToDictionary(g => g.Key, g => g.ToList());
            return grouped;
        }

        #endregion

        #region Browse (InnerTube API)

        /// <summary>
        /// Browse a YouTube Music resource (album, artist, playlist, etc.).
        /// Inspired by Echo Music's browse endpoint for album/artist details.
        /// </summary>
        public async Task<BrowseResponse> BrowseAsync(string browseId,
            YouTubeClientInfo client = null, CancellationToken cancellationToken = default)
        {
            await InitializeAsync();
            client = client ?? YouTubeClientInfo.WebRemix;

            var body = new Dictionary<string, object>
            {
                ["context"] = BuildContext(client),
                ["browseId"] = browseId
            };

            var result = await PostAsync("browse", body, client, cancellationToken);
            return ParseBrowseResponse(result);
        }

        /// <summary>
        /// Continue browsing with a continuation token (for pagination).
        /// </summary>
        public async Task<BrowseResponse> BrowseContinuationAsync(string continuationToken,
            YouTubeClientInfo client = null, CancellationToken cancellationToken = default)
        {
            await InitializeAsync();
            client = client ?? YouTubeClientInfo.WebRemix;

            var body = new Dictionary<string, object>
            {
                ["context"] = BuildContext(client),
                ["continuation"] = continuationToken
            };

            var result = await PostAsync("browse", body, client, cancellationToken);
            return ParseBrowseResponse(result);
        }

        #endregion

        #region Player (InnerTube API)

        /// <summary>
        /// Get player information for a video, including streaming data.
        /// Uses multi-client fallback chain inspired by Echo Music's player resolution.
        /// </summary>
        public async Task<PlayerResponse> GetPlayerAsync(string videoId,
            YouTubeClientInfo client = null, int? signatureTimestamp = null,
            CancellationToken cancellationToken = default)
        {
            await InitializeAsync();
            client = client ?? YouTubeClientInfo.AndroidVr;

            var body = BuildPlayerBody(videoId, null, client, signatureTimestamp);
            var result = await PostAsync("player", body, client, cancellationToken);
            return ParsePlayerResponse(result);
        }

        /// <summary>
        /// Get player response with multi-client fallback.
        /// Tries clients in the fallback chain until one succeeds.
        /// Inspired by Echo Music's STREAM_FALLBACK_CLIENTS logic.
        /// </summary>
        public async Task<PlayerResponse> GetPlayerWithFallbackAsync(string videoId,
            CancellationToken cancellationToken = default)
        {
            Exception lastException = null;

            foreach (var client in YouTubeClientInfo.PlaybackFallbackChain)
            {
                try
                {
                    var response = await GetPlayerAsync(videoId, client, null, cancellationToken);

                    if (response.Status == "OK" && response.Formats.Count > 0)
                        return response;

                    // Handle age restriction — try embedded client (like Echo Music's TVHTML5_SIMPLY_EMBEDDED_PLAYER)
                    if (response.Status?.Contains("AGE") == true ||
                        response.Status?.Contains("LOGIN") == true)
                        continue;
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    continue;
                }
            }

            throw lastException ?? new Exception("All playback clients failed for video: " + videoId);
        }

        /// <summary>
        /// Find the best audio format from a player response.
        /// Inspired by Echo Music's format selection: prefers Opus/WebM over AAC/MP4,
        /// selects original (non-dubbed) audio tracks, and picks highest bitrate.
        /// </summary>
        public FormatInfo FindBestAudioFormat(PlayerResponse playerResponse, AudioQualityPreference qualityPreference = AudioQualityPreference.Best)
        {
            var audioFormats = playerResponse.Formats
                .Where(f => f.IsAudio)
                .Where(f => f.IsOriginal)
                .ToList();

            if (audioFormats.Count == 0)
                audioFormats = playerResponse.Formats.Where(f => f.IsAudio).ToList();

            if (audioFormats.Count == 0)
                return null;

            // Inspired by Echo Music: +10240 bonus for WebM/Opus over MP4/AAC
            return audioFormats
                .OrderByDescending(f => f.Bitrate + (f.IsOpus ? 10240 : 0))
                .FirstOrDefault();
        }

        #endregion

        #region Next / Radio (InnerTube API)

        /// <summary>
        /// Get related/next content for a video (radio queue, related songs).
        /// Inspired by Echo Music's "next" endpoint for radio generation.
        /// </summary>
        public async Task<SearchResponse> GetNextAsync(string videoId, string playlistId = null,
            YouTubeClientInfo client = null, CancellationToken cancellationToken = default)
        {
            await InitializeAsync();
            client = client ?? YouTubeClientInfo.WebRemix;

            var body = new Dictionary<string, object>
            {
                ["context"] = BuildContext(client),
                ["videoId"] = videoId,
                ["contentCheckOk"] = true,
                ["racyCheckOk"] = true
            };

            if (!string.IsNullOrEmpty(playlistId))
                body["playlistId"] = playlistId;

            var result = await PostAsync("next", body, client, cancellationToken);
            return ParseNextResponse(result);
        }

        #endregion

        #region Bot Detection Mitigation (inspired by Echo Music)

        /// <summary>
        /// Rotate the guest session when bot detection is suspected.
        /// Inspired by Echo Music's BotDetectionMitigator.
        /// </summary>
        public async Task RotateGuestSessionAsync()
        {
            _visitorData = null;
            _initialized = false;
            await InitializeAsync();
        }

        #endregion

        #region HTTP Request Building

        private Dictionary<string, object> BuildContext(YouTubeClientInfo client)
        {
            var context = new Dictionary<string, object>
            {
                ["client"] = new Dictionary<string, object>
                {
                    ["clientName"] = client.ClientName,
                    ["clientVersion"] = client.ClientVersion,
                    ["hl"] = "en",
                    ["gl"] = "US"
                }
            };

            if (!string.IsNullOrEmpty(_visitorData))
                ((Dictionary<string, object>)context["client"])["visitorData"] = _visitorData;

            return context;
        }

        private Dictionary<string, object> BuildSearchBody(string query, SearchFilter filter, YouTubeClientInfo client)
        {
            var body = new Dictionary<string, object>
            {
                ["context"] = BuildContext(client),
                ["query"] = query
            };

            if (filter != null)
                body["params"] = filter.Value;

            return body;
        }

        private Dictionary<string, object> BuildPlayerBody(string videoId, string playlistId,
            YouTubeClientInfo client, int? signatureTimestamp)
        {
            var body = new Dictionary<string, object>
            {
                ["context"] = BuildContext(client),
                ["videoId"] = videoId,
                ["contentCheckOk"] = true,
                ["racyCheckOk"] = true
            };

            if (!string.IsNullOrEmpty(playlistId))
                body["playlistId"] = playlistId;

            if (signatureTimestamp.HasValue && client.UseSignatureTimestamp)
            {
                body["playbackContext"] = new Dictionary<string, object>
                {
                    ["contentPlaybackContext"] = new Dictionary<string, object>
                    {
                        ["signatureTimestamp"] = signatureTimestamp.Value
                    }
                };
            }

            if (client.IsEmbedded)
            {
                body["thirdParty"] = new Dictionary<string, object>
                {
                    ["embedUrl"] = "https://www.youtube.com/"
                };
            }

            return body;
        }

        private async Task<JObject> PostAsync(string endpoint, Dictionary<string, object> body,
            YouTubeClientInfo client, CancellationToken cancellationToken, int maxRetries = 3)
        {
            Exception lastException = null;

            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                try
                {
                    // Exponential backoff on retry (inspired by Echo Music's withRetry)
                    if (attempt > 0)
                        await Task.Delay((int)(500 * Math.Pow(2, attempt - 1)), cancellationToken);

                    var json = JsonConvert.SerializeObject(body);
                    var content = new StringContent(json, Encoding.UTF8, "application/json");

                    var request = new HttpRequestMessage(HttpMethod.Post,
                        $"https://music.youtube.com/youtubei/v1/{endpoint}?prettyPrint=false")
                    {
                        Content = content
                    };

                    // Set client-specific headers (inspired by Echo Music's ytClient function)
                    request.Headers.Add("X-Goog-Api-Format-Version", "1");
                    request.Headers.Add("X-YouTube-Client-Name", client.ClientId);
                    request.Headers.Add("X-YouTube-Client-Version", client.ClientVersion);
                    request.Headers.Add("X-Origin", "https://music.youtube.com");
                    request.Headers.Add("Referer", "https://music.youtube.com/");

                    if (!string.IsNullOrEmpty(_visitorData))
                        request.Headers.Add("X-Goog-Visitor-Id", _visitorData);

                    // SAPISIDHASH authentication (inspired by Echo Music's SAPISIDHASH logic)
                    if (!string.IsNullOrEmpty(_cookie) && client.LoginSupported)
                    {
                        request.Headers.Add("Cookie", _cookie);

                        var sapisid = ExtractCookieValue(_cookie, "SAPISID");
                        if (!string.IsNullOrEmpty(sapisid))
                        {
                            var currentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                            var hashInput = $"{currentTime} {sapisid} https://music.youtube.com";
                            var hash = Sha1Hash(hashInput);
                            request.Headers.Add("Authorization", $"SAPISIDHASH {currentTime}_{hash}");
                        }
                    }

                    request.Headers.Add("User-Agent", client.UserAgent);

                    using (var response = await _httpClient.SendAsync(request, cancellationToken))
                    {
                        response.EnsureSuccessStatusCode();
                        var responseJson = await response.Content.ReadAsStringAsync();
                        return JObject.Parse(responseJson);
                    }
                }
                catch (IOException ex)
                {
                    lastException = ex;
                    continue;
                }
                catch (HttpRequestException ex)
                {
                    lastException = ex;
                    continue;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    break; // Non-retriable errors
                }
            }

            throw lastException ?? new Exception($"Failed to call InnerTube {endpoint} after {maxRetries} attempts");
        }

        #endregion

        #region Response Parsing

        private SearchResponse ParseSearchResponse(JObject result)
        {
            var response = new SearchResponse();

            try
            {
                // Navigate through the InnerTube response structure
                var tabs = result.SelectToken("$.contents.tabbedSearchResultsRenderer.tabs");
                if (tabs == null)
                {
                    // Try continuation format
                    var continuationItems = result.SelectToken("$.continuationContents");
                    if (continuationItems != null)
                    {
                        ParseMusicShelfFromToken(continuationItems, response);
                        return response;
                    }
                    return response;
                }

                var tabContent = tabs.First?.SelectToken("tabRenderer.content.sectionListRenderer.contents");
                if (tabContent == null) return response;

                foreach (var section in tabContent)
                {
                    var shelf = section.SelectToken("musicShelfRenderer");
                    if (shelf != null)
                    {
                        ParseMusicShelfFromToken(shelf, response);
                    }

                    // Also check for card shelf (featured results)
                    var cardShelf = section.SelectToken("musicCardShelfRenderer");
                    if (cardShelf != null)
                    {
                        ParseCardShelfFromToken(cardShelf, response);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[InnerTube] Search parse error: {ex.Message}");
            }

            return response;
        }

        private void ParseMusicShelfFromToken(JToken shelf, SearchResponse response)
        {
            var contents = shelf.SelectToken("contents");
            if (contents == null) return;

            foreach (var item in contents)
            {
                var parsed = ParseMusicResponsiveListItem(item.SelectToken("musicResponsiveListItemRenderer"));
                if (parsed != null)
                    response.Items.Add(parsed);
            }

            // Extract continuation token
            var continuations = shelf.SelectToken("continuations");
            response.ContinuationToken = ExtractContinuationToken(continuations);
        }

        private void ParseCardShelfFromToken(JToken cardShelf, SearchResponse response)
        {
            var title = cardShelf.SelectToken("title.runs[0].text")?.ToString();
            var subtitle = cardShelf.SelectToken("subtitle.runs")?
                .Select(r => r.SelectToken("text")?.ToString())
                .Where(t => t != null);
            var thumbnail = cardShelf.SelectToken("thumbnail.musicThumbnailRenderer.thumbnail.thumbnails[0].url")?.ToString();

            // Try to extract video/playlist/browse ID from buttons or title
            var videoId = cardShelf.SelectToken("buttons[0].buttonRenderer.navigationEndpoint.watchEndpoint.videoId")?.ToString()
                ?? cardShelf.SelectToken("title.runs[0].navigationEndpoint.watchEndpoint.videoId")?.ToString();
            var browseId = cardShelf.SelectToken("title.runs[0].navigationEndpoint.browseEndpoint.browseId")?.ToString();
            var playlistId = cardShelf.SelectToken("buttons[0].buttonRenderer.navigationEndpoint.watchEndpoint.playlistId")?.ToString();

            var type = DetermineResultType(browseId, playlistId, videoId);

            response.Items.Add(new InnerTubeSearchItem
            {
                VideoId = videoId,
                Title = title,
                Artist = subtitle != null ? string.Join(" ", subtitle) : null,
                ThumbnailUrl = thumbnail,
                PlaylistId = playlistId,
                BrowseId = browseId,
                Type = type
            });
        }

        private InnerTubeSearchItem ParseMusicResponsiveListItem(JToken item)
        {
            if (item == null) return null;

            try
            {
                // Extract columns for metadata
                var columns = item.SelectTokens("$.flexColumns[*].musicResponsiveListItemFlexColumnRenderer")
                    .ToList();

                string title = null, subtitle = null;
                if (columns.Count > 0)
                    title = ExtractRunsText(columns[0].SelectToken("text"));
                if (columns.Count > 1)
                    subtitle = ExtractRunsText(columns[1].SelectToken("text"));

                var thumbnail = item.SelectToken("thumbnail.musicThumbnailRenderer.thumbnail.thumbnails[0].url")?.ToString();

                // Extract navigation endpoint to determine type and ID
                var videoId = item.SelectToken("navigationEndpoint.watchEndpoint.videoId")?.ToString()
                    ?? item.SelectToken("playlistItemData.videoId")?.ToString();
                var browseId = item.SelectToken("navigationEndpoint.browseEndpoint.browseId")?.ToString();
                var playlistId = item.SelectToken("navigationEndpoint.watchEndpoint.playlistId")?.ToString();

                var type = DetermineResultType(browseId, playlistId, videoId);

                // Parse subtitle for artist, album, duration
                string artist = null, album = null;
                TimeSpan duration = TimeSpan.Zero;

                if (!string.IsNullOrEmpty(subtitle))
                {
                    var parts = subtitle.Split(new[] { " \u2022 " }, StringSplitOptions.None);
                    if (parts.Length > 0) artist = parts[0].Trim();
                    if (parts.Length > 1 && type == SearchResultType.Song) album = parts[1].Trim();

                    // Try to parse duration from last part
                    if (parts.Length > 0)
                    {
                        var lastPart = parts[parts.Length - 1].Trim();
                        if (TimeSpan.TryParse(lastPart, out var dur))
                            duration = dur;
                    }
                }

                return new InnerTubeSearchItem
                {
                    VideoId = videoId,
                    Title = title,
                    Artist = artist,
                    Album = album,
                    ThumbnailUrl = thumbnail,
                    PlaylistId = playlistId,
                    BrowseId = browseId,
                    Duration = duration,
                    Type = type,
                    Subtitle = subtitle
                };
            }
            catch
            {
                return null;
            }
        }

        private BrowseResponse ParseBrowseResponse(JObject result)
        {
            var response = new BrowseResponse();

            try
            {
                // Try to find header info
                var header = result.SelectToken("$.header.musicDetailHeaderRenderer")
                    ?? result.SelectToken("$.header.musicEditablePlaylistDetailHeaderRenderer.header.musicDetailHeaderRenderer")
                    ?? result.SelectToken("$.header.musicResponsiveHeaderRenderer")
                    ?? result.SelectToken("$.header.musicImmersiveHeaderRenderer");

                if (header != null)
                {
                    response.Title = header.SelectToken("title.runs[0].text")?.ToString()
                        ?? header.SelectToken("title.runs")?.Select(r => r.SelectToken("text")?.ToString())
                            .Where(t => t != null).FirstOrDefault();
                    response.Description = header.SelectToken("description.musicDescriptionShelfRenderer.text.runs[0].text")?.ToString();
                    response.ThumbnailUrl = header.SelectToken("thumbnail.croppedSquareThumbnailRenderer.thumbnail.thumbnails[0].url")?.ToString()
                        ?? header.SelectToken("thumbnail.musicThumbnailRenderer.thumbnail.thumbnails[0].url")?.ToString();
                }

                // Parse track list from contents
                var contents = result.SelectToken("$.contents.singleColumnBrowseResultsRenderer.tabs[0].tabRenderer.content.sectionListRenderer.contents")
                    ?? result.SelectToken("$.contents.singleColumnBrowseResultsRenderer.tabs[0].tabRenderer.content.sectionListRenderer.contents");

                if (contents != null)
                {
                    foreach (var section in contents)
                    {
                        var shelf = section.SelectToken("musicShelfRenderer");
                        if (shelf != null)
                        {
                            var items = shelf.SelectToken("contents");
                            if (items != null)
                            {
                                foreach (var item in items)
                                {
                                    var parsed = ParseMusicResponsiveListItem(
                                        item.SelectToken("musicResponsiveListItemRenderer"));
                                    if (parsed != null)
                                        response.Tracks.Add(parsed);
                                }
                            }

                            var continuations = shelf.SelectToken("continuations");
                            response.ContinuationToken = ExtractContinuationToken(continuations);
                        }

                        // Also check musicPlaylistShelfRenderer
                        var playlistShelf = section.SelectToken("musicPlaylistShelfRenderer");
                        if (playlistShelf != null)
                        {
                            var items = playlistShelf.SelectToken("contents");
                            if (items != null)
                            {
                                foreach (var item in items)
                                {
                                    var parsed = ParseMusicResponsiveListItem(
                                        item.SelectToken("musicResponsiveListItemRenderer"));
                                    if (parsed != null)
                                        response.Tracks.Add(parsed);
                                }
                            }

                            var continuations = playlistShelf.SelectToken("continuations");
                            response.ContinuationToken = ExtractContinuationToken(continuations);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[InnerTube] Browse parse error: {ex.Message}");
            }

            return response;
        }

        private PlayerResponse ParsePlayerResponse(JObject result)
        {
            var response = new PlayerResponse();

            try
            {
                response.Status = result.SelectToken("$.playabilityStatus.status")?.ToString() ?? "ERROR";

                var streamingData = result.SelectToken("$.streamingData");
                if (streamingData != null)
                {
                    var expiresStr = streamingData.SelectToken("$.expiresInSeconds")?.ToString();
                    if (int.TryParse(expiresStr, out var expires))
                        response.ExpiresInSeconds = expires;

                    // Parse adaptive formats (audio-only streams)
                    var adaptiveFormats = streamingData.SelectToken("$.adaptiveFormats");
                    if (adaptiveFormats != null)
                    {
                        foreach (var fmt in adaptiveFormats)
                        {
                            var format = ParseFormat(fmt);
                            if (format != null && format.IsAudio)
                                response.Formats.Add(format);
                        }
                    }

                    // Parse regular formats
                    var formats = streamingData.SelectToken("$.formats");
                    if (formats != null)
                    {
                        foreach (var fmt in formats)
                        {
                            var format = ParseFormat(fmt);
                            if (format != null && format.IsAudio)
                                response.Formats.Add(format);
                        }
                    }
                }

                // Parse video details
                var videoDetails = result.SelectToken("$.videoDetails");
                if (videoDetails != null)
                {
                    response.Details = new VideoDetails
                    {
                        VideoId = videoDetails.SelectToken("videoId")?.ToString(),
                        Title = videoDetails.SelectToken("title")?.ToString(),
                        Author = videoDetails.SelectToken("author")?.ToString(),
                        LengthSeconds = videoDetails.SelectToken("lengthSeconds")?.Value<long>() ?? 0,
                        ThumbnailUrl = videoDetails.SelectToken("thumbnail.thumbnails[0].url")?.ToString(),
                        MusicVideoType = videoDetails.SelectToken("musicVideoType")?.ToString()
                    };
                }

                // Parse loudness normalization
                var loudness = result.SelectToken("$.playerConfig.audioConfig.loudnessDb");
                if (loudness != null)
                    response.LoudnessDb = loudness.Value<double?>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[InnerTube] Player parse error: {ex.Message}");
            }

            return response;
        }

        private FormatInfo ParseFormat(JToken fmt)
        {
            try
            {
                return new FormatInfo
                {
                    Itag = fmt.SelectToken("itag")?.Value<int>() ?? 0,
                    Url = fmt.SelectToken("url")?.ToString(),
                    MimeType = fmt.SelectToken("mimeType")?.ToString() ?? "",
                    Bitrate = fmt.SelectToken("bitrate")?.Value<int>() ?? 0,
                    AudioSampleRate = fmt.SelectToken("audioSampleRate")?.Value<int>(),
                    SignatureCipher = fmt.SelectToken("signatureCipher")?.ToString(),
                    Cipher = fmt.SelectToken("cipher")?.ToString(),
                    ContentLength = fmt.SelectToken("contentLength")?.Value<long>(),
                    AudioTrack = fmt.SelectToken("audioTrack") != null ? new AudioTrackInfo
                    {
                        Language = fmt.SelectToken("audioTrack.displayName")?.ToString(),
                        IsAutoDubbed = fmt.SelectToken("audioTrack.audioIsDefault")?.Value<bool>() == false
                            && fmt.SelectToken("audioTrack.captionsOriginal")?.Value<bool>() != true
                    } : null
                };
            }
            catch
            {
                return null;
            }
        }

        private SearchResponse ParseNextResponse(JObject result)
        {
            var response = new SearchResponse();

            try
            {
                var contents = result.SelectToken("$.contents.singleColumnMusicWatchNextResultsRenderer.tabbedRenderer.watchNextTabbedResultsRenderer.tabs");
                if (contents != null)
                {
                    foreach (var tab in contents)
                    {
                        var tabContent = tab.SelectToken("tabRenderer.content.musicQueueRenderer.content.playlistPanelRenderer");
                        if (tabContent != null)
                        {
                            var items = tabContent.SelectToken("contents");
                            if (items != null)
                            {
                                foreach (var item in items)
                                {
                                    var panelItem = item.SelectToken("playlistPanelVideoRenderer");
                                    if (panelItem != null)
                                    {
                                        var videoId = panelItem.SelectToken("videoId")?.ToString();
                                        var title = panelItem.SelectToken("title.runs[0].text")?.ToString();
                                        var artist = panelItem.SelectToken("longBylineText.runs[0].text")?.ToString();
                                        var thumbnail = panelItem.SelectToken("thumbnail.thumbnails[0].url")?.ToString();
                                        var durationStr = panelItem.SelectToken("lengthText.runs[0].text")?.ToString();

                                        TimeSpan duration = TimeSpan.Zero;
                                        if (!string.IsNullOrEmpty(durationStr))
                                            TimeSpan.TryParse(durationStr, out duration);

                                        response.Items.Add(new InnerTubeSearchItem
                                        {
                                            VideoId = videoId,
                                            Title = title,
                                            Artist = artist,
                                            ThumbnailUrl = thumbnail,
                                            Duration = duration,
                                            Type = SearchResultType.Song
                                        });
                                    }
                                }
                            }

                            var continuations = tabContent.SelectToken("continuations");
                            response.ContinuationToken = ExtractContinuationToken(continuations);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[InnerTube] Next parse error: {ex.Message}");
            }

            return response;
        }

        private SearchSuggestions ParseSearchSuggestions(JObject result)
        {
            var suggestions = new SearchSuggestions();

            try
            {
                var contents = result.SelectToken("$.contents");
                if (contents != null)
                {
                    // Parse query suggestions
                    var queriesSection = contents.First?.SelectToken("searchSuggestionsSectionRenderer.contents");
                    if (queriesSection != null)
                    {
                        foreach (var item in queriesSection)
                        {
                            var query = item.SelectToken("searchSuggestionRenderer.suggestion.runs")?
                                .Select(r => r.SelectToken("text")?.ToString())
                                .Where(t => t != null);
                            if (query != null)
                                suggestions.Queries.Add(string.Join("", query));
                        }
                    }

                    // Parse recommended items
                    var itemsSection = contents.Skip(1).FirstOrDefault()?
                        .SelectToken("searchSuggestionsSectionRenderer.contents");
                    if (itemsSection != null)
                    {
                        foreach (var item in itemsSection)
                        {
                            var parsed = ParseMusicResponsiveListItem(
                                item.SelectToken("musicResponsiveListItemRenderer"));
                            if (parsed != null)
                                suggestions.RecommendedItems.Add(parsed);
                        }
                    }
                }
            }
            catch
            {
                // Non-critical — return whatever we have
            }

            return suggestions;
        }

        #endregion

        #region Utility Methods

        private static string ExtractRunsText(JToken textToken)
        {
            if (textToken == null) return null;

            var simpleText = textToken.SelectToken("simpleText")?.ToString();
            if (simpleText != null) return simpleText;

            var runs = textToken.SelectToken("runs");
            if (runs != null)
                return string.Join("", runs.Select(r => r.SelectToken("text")?.ToString() ?? ""));

            return null;
        }

        private static SearchResultType DetermineResultType(string browseId, string playlistId, string videoId)
        {
            if (!string.IsNullOrEmpty(browseId))
            {
                if (browseId.StartsWith("MPRE")) return SearchResultType.Album;
                if (browseId.StartsWith("UC") || browseId.StartsWith("VLUC")) return SearchResultType.Artist;
            }
            if (!string.IsNullOrEmpty(playlistId))
                return SearchResultType.Playlist;
            if (!string.IsNullOrEmpty(videoId))
                return SearchResultType.Song; // Default YouTube Music results are songs

            return SearchResultType.Unknown;
        }

        private static string ExtractContinuationToken(JToken continuations)
        {
            if (continuations == null) return null;

            var token = continuations.SelectToken("[0].nextContinuationData.continuation")?.ToString()
                ?? continuations.SelectToken("[0].nextContinuationData.continuation")?.ToString();
            return token;
        }

        private static string ExtractCookieValue(string cookie, string name)
        {
            var parts = cookie.Split(';');
            foreach (var part in parts)
            {
                var kvp = part.Trim().Split('=');
                if (kvp.Length == 2 && kvp[0].Trim().Equals(name, StringComparison.OrdinalIgnoreCase))
                    return kvp[1].Trim();
            }
            return null;
        }

        private static string Sha1Hash(string input)
        {
            using (var sha1 = SHA1.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(input);
                var hash = sha1.ComputeHash(bytes);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }

        #endregion

        public void Dispose()
        {
            _httpClient?.Dispose();
            _handler?.Dispose();
            _initLock?.Dispose();
        }
    }

    /// <summary>
    /// Audio quality preference for format selection.
    /// Inspired by Echo Music's AudioQuality enum.
    /// </summary>
    public enum AudioQualityPreference
    {
        Best,
        Medium,
        Low,
        OpusPreferred,
        AacPreferred
    }
}

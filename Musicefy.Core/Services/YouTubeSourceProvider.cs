using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Musicefy.Core.Interfaces;
using Musicefy.Core.Models;
using Musicefy.Core.Services.YouTubeApi;
using YoutubeExplode;
using YoutubeExplode.Search;
using YoutubeExplode.Videos.Streams;
using static Musicefy.Core.SourceTypes;

namespace Musicefy.Core.Services
{
    /// <summary>
    /// Improved YouTube Music source provider with InnerTube API integration,
    /// multi-strategy stream resolution, caching, and bot detection mitigation.
    /// 
    /// Architecture inspired by Echo Music (https://github.com/EchoMusicApp/Echo-Music):
    /// - InnerTube API for YouTube Music-specific search, browse, and metadata
    /// - Multi-client fallback chain for reliable playback (6 client identities)
    /// - Stream URL caching with expiration tracking (~6 hour TTL)
    /// - Bot detection mitigation with automatic session rotation
    /// - Audio format selection preferring Opus/WebM over AAC/MP4
    /// - Search with type filters (songs, videos, albums, artists, playlists)
    /// - Album, artist, and playlist browsing via InnerTube browse endpoint
    /// - Search suggestions via InnerTube autocomplete
    /// - Retry with exponential backoff on all API calls
    /// - YouTube URL parsing for various formats
    /// </summary>
    public class YouTubeSourceProvider : IMusicSourceProvider
    {
        private static readonly HttpClient _httpClient = new HttpClient(new HttpClientHandler
        {
            AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate,
            MaxConnectionsPerServer = 10
        })
        { Timeout = TimeSpan.FromSeconds(30) };

        private readonly YouTubeStreamCache _streamCache = new YouTubeStreamCache();
        private readonly YouTubeMetadataCache _metadataCache = new YouTubeMetadataCache();
        private readonly BotDetectionMitigator _botMitigator = new BotDetectionMitigator();
        private InnerTubeClient _innerTube;
        private readonly object _innerTubeLock = new object();

        // Periodic cache cleanup timer
        private Timer _cacheCleanupTimer;

        public string SourceType => YouTube;
        public string DisplayName => "YouTube Music";
        public string Description => "Search and play music from YouTube with enhanced InnerTube API integration";
        public string IconGlyph => "\u25B6\uFE0F";

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
            },
            new SourceConfigField
            {
                Key = "cookie",
                Label = "YouTube Cookie (optional)",
                Description = "Login cookie for authenticated access. Enables age-restricted content and higher quality.",
                IsRequired = false,
                IsPassword = true,
                Placeholder = "SID=...; HSID=...; SAPISID=..."
            },
            new SourceConfigField
            {
                Key = "audioQuality",
                Label = "Audio Quality Preference",
                Description = "Preferred audio format. Opus provides better quality at lower bitrates.",
                IsRequired = false,
                IsPassword = false,
                Placeholder = "opus (default) | aac"
            }
        };

        public YouTubeSourceProvider()
        {
            // Setup periodic cache cleanup (every 30 minutes)
            _cacheCleanupTimer = new Timer(_ =>
            {
                _streamCache.PurgeExpired();
                _metadataCache.PurgeExpired();
            }, null, TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(30));

            // Wire up bot detection to auto-rotate InnerTube session
            _botMitigator.BotDetectionSuspected += OnBotDetectionSuspected;
        }

        private void OnBotDetectionSuspected()
        {
            lock (_innerTubeLock)
            {
                if (_innerTube != null)
                {
                    _ = _innerTube.RotateGuestSessionAsync();
                    System.Diagnostics.Debug.WriteLine("[YouTubeSourceProvider] Bot detection suspected - rotating guest session");
                }
            }
        }

        /// <summary>
        /// Get or create an InnerTube client for the given configuration.
        /// </summary>
        private InnerTubeClient GetInnerTubeClient(IReadOnlyDictionary<string, string> config)
        {
            string cookie = null;
            if (config != null && config.TryGetValue("cookie", out var cookieValue))
                cookie = cookieValue;

            lock (_innerTubeLock)
            {
                if (_innerTube == null)
                {
                    _innerTube = new InnerTubeClient(cookie);
                    _ = _innerTube.InitializeAsync();
                }
                return _innerTube;
            }
        }

        public async Task<bool> TestConnectionAsync(IReadOnlyDictionary<string, string> config)
        {
            try
            {
                // Test 1: Try YoutubeExplode (basic connectivity)
                using (var youtube = new YoutubeClient())
                {
                    var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    var enumerator = youtube.Search.GetResultsAsync("test").GetAsyncEnumerator(cts.Token);
                    try
                    {
                        if (await enumerator.MoveNextAsync())
                            return true;
                    }
                    finally
                    {
                        await enumerator.DisposeAsync();
                    }
                }

                // Test 2: Try InnerTube API
                var innerTube = GetInnerTubeClient(config);
                var results = await innerTube.SearchAsync("test",
                    InnerTubeClient.SearchFilter.Song,
                    cancellationToken: new CancellationTokenSource(5000).Token);
                return results.Items.Count > 0;
            }
            catch
            {
                return false;
            }
        }

        public IMusicSourceSession CreateSession(IReadOnlyDictionary<string, string> config, string sourceId)
        {
            return new YouTubeSourceSession(sourceId, config, this);
        }

        /// <summary>
        /// Enhanced YouTube source session with InnerTube API integration.
        /// </summary>
        private class YouTubeSourceSession : IYouTubeSourceSession
        {
            private readonly YoutubeClient _youtube;
            private readonly InnerTubeClient _innerTube;
            private readonly string _sourceId;
            private readonly YouTubeSourceProvider _provider;
            private readonly string _cookie;
            private readonly AudioQualityPreference _audioQualityPref;

            public YouTubeSourceSession(string sourceId, IReadOnlyDictionary<string, string> config,
                YouTubeSourceProvider provider)
            {
                _sourceId = sourceId;
                _provider = provider;
                _youtube = new YoutubeClient();
                _innerTube = provider.GetInnerTubeClient(config);

                // Extract configuration
                if (config != null)
                {
                    config.TryGetValue("cookie", out _cookie);

                    string qualityPref = null;
                    if (config.TryGetValue("audioQuality", out qualityPref) &&
                        qualityPref?.Equals("aac", StringComparison.OrdinalIgnoreCase) == true)
                        _audioQualityPref = AudioQualityPreference.AacPreferred;
                    else
                        _audioQualityPref = AudioQualityPreference.OpusPreferred;
                }
                else
                {
                    _audioQualityPref = AudioQualityPreference.OpusPreferred;
                }
            }

            #region Search (Enhanced with InnerTube + type filters)

            /// <summary>
            /// Search YouTube Music using both InnerTube API (for structured results)
            /// and YoutubeExplode (as fallback). Inspired by Echo Music's dual search strategy.
            /// </summary>
            public async Task<IReadOnlyList<MusicFile>> SearchAsync(string query, int limit = 50)
            {
                if (string.IsNullOrWhiteSpace(query))
                    return await GetRandomSongsAsync(limit);

                var results = new List<MusicFile>();

                // Strategy 1: Try InnerTube search with song filter first
                try
                {
                    if (!_provider._metadataCache.TryGetSearchResults(query, out var innerTubeResults))
                    {
                        innerTubeResults = await _innerTube.SearchAsync(query,
                            InnerTubeClient.SearchFilter.Song);
                        _provider._metadataCache.PutSearchResults(query, innerTubeResults);
                    }

                    foreach (var item in innerTubeResults.Items.Take(limit))
                    {
                        var musicFile = ConvertToMusicFile(item);
                        if (musicFile != null)
                            results.Add(musicFile);
                    }

                    if (results.Count >= limit)
                        return results.Take(limit).ToList();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[YouTube] InnerTube search failed, falling back to YoutubeExplode: {ex.Message}");
                }

                // Strategy 2: Fallback to YoutubeExplode search
                try
                {
                    var remaining = limit - results.Count;
                    var existingIds = new HashSet<string>(results.Select(r => r.FilePath));

                    await foreach (var result in _youtube.Search.GetResultsAsync(query))
                    {
                        if (results.Count >= limit) break;

                        if (result is VideoSearchResult video)
                        {
                            var id = $"{_sourceId}:{video.Id}";
                            if (existingIds.Contains(id)) continue;

                            results.Add(new MusicFile
                            {
                                FilePath = id,
                                Title = video.Title,
                                Artist = video.Author?.ChannelTitle ?? "YouTube",
                                Album = "YouTube Music",
                                Genre = "Music",
                                SourceType = YouTube,
                                CoverPath = SanitizeThumbnailUrl(video.Thumbnails?.FirstOrDefault()?.Url),
                                Duration = video.Duration ?? TimeSpan.Zero
                            });

                            existingIds.Add(id);
                        }
                    }
                }
                catch
                {
                    // Search failed - return whatever we have
                }

                return results;
            }

            /// <summary>
            /// Search with type filter. Inspired by Echo Music's search with filters.
            /// </summary>
            public async Task<IReadOnlyList<MusicFile>> SearchWithTypeAsync(string query,
                InnerTubeClient.SearchResultType type, int limit = 50)
            {
                var filter = type switch
                {
                    InnerTubeClient.SearchResultType.Song => InnerTubeClient.SearchFilter.Song,
                    InnerTubeClient.SearchResultType.Video => InnerTubeClient.SearchFilter.Video,
                    InnerTubeClient.SearchResultType.Album => InnerTubeClient.SearchFilter.Album,
                    InnerTubeClient.SearchResultType.Artist => InnerTubeClient.SearchFilter.Artist,
                    InnerTubeClient.SearchResultType.Playlist => InnerTubeClient.SearchFilter.FeaturedPlaylist,
                    _ => (InnerTubeClient.SearchFilter)null
                };

                try
                {
                    var results = await _innerTube.SearchAsync(query, filter);
                    return results.Items
                        .Where(i => i.Type == type || type == InnerTubeClient.SearchResultType.Unknown)
                        .Take(limit)
                        .Select(ConvertToMusicFile)
                        .Where(m => m != null)
                        .ToList();
                }
                catch
                {
                    return new List<MusicFile>();
                }
            }

            /// <summary>
            /// Search with type filter (string-based for IYouTubeSourceSession).
            /// </summary>
            public Task<IReadOnlyList<MusicFile>> SearchWithTypeAsync(string query, string resultType, int limit = 50)
            {
                var type = Enum.TryParse<InnerTubeClient.SearchResultType>(resultType, true, out var parsed)
                    ? parsed
                    : InnerTubeClient.SearchResultType.Unknown;
                return SearchWithTypeAsync(query, type, limit);
            }

            /// <summary>
            /// Get search suggestions. Inspired by Echo Music's autocomplete.
            /// </summary>
            public async Task<List<string>> GetSearchSuggestionsAsync(string query)
            {
                try
                {
                    var suggestions = await _innerTube.GetSearchSuggestionsAsync(query);
                    return suggestions.Queries;
                }
                catch
                {
                    return new List<string>();
                }
            }

            #endregion

            #region Stream URL Resolution (Multi-strategy with caching)

            /// <summary>
            /// Get the stream URL for a track using a multi-strategy approach with caching.
            /// </summary>
            public async Task<string> GetStreamUrlAsync(string trackId)
            {
                if (string.IsNullOrEmpty(trackId))
                    return null;

                // Handle URLs that might be passed directly
                if (trackId.StartsWith("http"))
                {
                    var parsed = YouTubeUrlParser.Parse(trackId);
                    if (parsed.VideoId != null)
                        trackId = parsed.VideoId;
                    else
                        return trackId;
                }

                // Step 1: Check cache
                var cachedUrl = _provider._streamCache.TryGet(trackId);
                if (cachedUrl != null)
                {
                    System.Diagnostics.Debug.WriteLine($"[YouTube] Stream URL cache hit for {trackId}");
                    return cachedUrl;
                }

                // Step 2: Try YoutubeExplode (primary strategy)
                string streamUrl = await ResolveWithYoutubeExplodeAsync(trackId);

                // Step 3: If YoutubeExplode fails, try InnerTube with multi-client fallback
                if (string.IsNullOrEmpty(streamUrl))
                {
                    streamUrl = await ResolveWithInnerTubeAsync(trackId);
                }

                // Step 4: If both strategies fail, try bot detection mitigation and retry
                if (string.IsNullOrEmpty(streamUrl) && _provider._botMitigator.IsBotDetectionLikely)
                {
                    System.Diagnostics.Debug.WriteLine(
                        "[YouTube] Bot detection likely - rotating session and retrying");

                    await _innerTube.RotateGuestSessionAsync();
                    _provider._botMitigator.Reset();

                    streamUrl = await ResolveWithInnerTubeAsync(trackId);
                }

                // Step 5: Cache successful result
                if (!string.IsNullOrEmpty(streamUrl))
                {
                    _provider._streamCache.Put(trackId, streamUrl);
                    _provider._botMitigator.NotifyPlaybackSuccess();
                }
                else
                {
                    _provider._botMitigator.NotifyPlaybackFailure();
                }

                return streamUrl;
            }

            /// <summary>
            /// Resolve stream URL using YoutubeExplode with Opus format preference.
            /// </summary>
            private async Task<string> ResolveWithYoutubeExplodeAsync(string videoId)
            {
                try
                {
                    var manifest = await _youtube.Videos.Streams.GetManifestAsync(videoId);

                    IStreamInfo audioStream = null;

                    if (_audioQualityPref == AudioQualityPreference.OpusPreferred)
                    {
                        // Try Opus first
                        audioStream = manifest.GetAudioOnlyStreams()
                            .Where(s => s.Container.Name == "webm" || s.AudioCodec?.Contains("opus") == true)
                            .OrderByDescending(s => s.Bitrate)
                            .FirstOrDefault();

                        if (audioStream == null)
                            audioStream = manifest.GetAudioOnlyStreams()
                                .OrderByDescending(s => s.Bitrate)
                                .FirstOrDefault();
                    }
                    else if (_audioQualityPref == AudioQualityPreference.AacPreferred)
                    {
                        // Try AAC/MP4 first
                        audioStream = manifest.GetAudioOnlyStreams()
                            .Where(s => s.Container.Name == "mp4" || s.AudioCodec?.Contains("mp4a") == true)
                            .OrderByDescending(s => s.Bitrate)
                            .FirstOrDefault();

                        if (audioStream == null)
                            audioStream = manifest.GetAudioOnlyStreams()
                                .OrderByDescending(s => s.Bitrate)
                                .FirstOrDefault();
                    }
                    else
                    {
                        audioStream = manifest.GetAudioOnlyStreams()
                            .OrderByDescending(s => s.Bitrate)
                            .FirstOrDefault();
                    }

                    if (audioStream != null)
                    {
                        var url = audioStream.Url?.ToString();

                        // Validate URL is accessible
                        if (!string.IsNullOrEmpty(url))
                        {
                            var isValid = await ValidateStreamUrlAsync(url);
                            if (isValid)
                                return url;
                        }
                    }

                    // Fallback: any stream with audio
                    var anyWithAudio = manifest.Streams
                        .Where(s => s is IAudioStreamInfo)
                        .OrderByDescending(s => s.Bitrate)
                        .FirstOrDefault();

                    return anyWithAudio?.Url?.ToString();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[YouTube] YoutubeExplode resolution failed for {videoId}: {ex.Message}");
                    return null;
                }
            }

            /// <summary>
            /// Resolve stream URL using InnerTube player API with multi-client fallback.
            /// </summary>
            private async Task<string> ResolveWithInnerTubeAsync(string videoId)
            {
                try
                {
                    var playerResponse = await _innerTube.GetPlayerWithFallbackAsync(videoId);

                    if (playerResponse.Status != "OK" || playerResponse.Formats.Count == 0)
                        return null;

                    var bestFormat = _innerTube.FindBestAudioFormat(playerResponse, _audioQualityPref);

                    if (bestFormat != null)
                    {
                        if (!string.IsNullOrEmpty(bestFormat.Url))
                        {
                            var isValid = await ValidateStreamUrlAsync(bestFormat.Url);
                            if (isValid)
                                return bestFormat.Url;
                        }
                    }

                    // Try any format with a direct URL
                    foreach (var format in playerResponse.Formats
                        .Where(f => !string.IsNullOrEmpty(f.Url))
                        .OrderByDescending(f => f.Bitrate + (f.IsOpus ? 10240 : 0)))
                    {
                        var isValid = await ValidateStreamUrlAsync(format.Url);
                        if (isValid)
                        {
                            _provider._streamCache.Put(videoId, format.Url,
                                format.MimeType, format.Bitrate, playerResponse.ExpiresInSeconds);
                            return format.Url;
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[YouTube] InnerTube resolution failed for {videoId}: {ex.Message}");
                }

                return null;
            }

            /// <summary>
            /// Validate that a stream URL is accessible using a HEAD request.
            /// </summary>
            private static async Task<bool> ValidateStreamUrlAsync(string url)
            {
                try
                {
                    var request = new HttpRequestMessage(HttpMethod.Head, url);
                    request.Headers.Add("User-Agent",
                        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

                    using (var response = await _httpClient.SendAsync(request,
                        HttpCompletionOption.ResponseHeadersRead))
                    {
                        return response.IsSuccessStatusCode;
                    }
                }
                catch
                {
                    return false;
                }
            }

            #endregion

            #region Cover Art

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

            #endregion

            #region Random Songs (Enhanced with InnerTube trending)

            /// <summary>
            /// Get random/trending songs. Enhanced with InnerTube browse for actual YouTube Music trending charts.
            /// </summary>
            public async Task<IReadOnlyList<MusicFile>> GetRandomSongsAsync(int count = 50)
            {
                var results = new List<MusicFile>();

                // Strategy 1: Try InnerTube trending/charts
                try
                {
                    var chartsResponse = await _innerTube.BrowseAsync("FEmusic_charts");
                    if (chartsResponse.Tracks.Count > 0)
                    {
                        foreach (var track in chartsResponse.Tracks.Take(count))
                        {
                            var musicFile = ConvertToMusicFile(track);
                            if (musicFile != null)
                                results.Add(musicFile);
                        }

                        if (results.Count >= count)
                            return results.Take(count).ToList();
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[YouTube] InnerTube charts failed: {ex.Message}");
                }

                // Strategy 2: Fallback to random searches
                var queries = new[] { "popular music", "top hits", "new songs", "music mix", "trending music" };
                var random = new Random();
                var shuffled = queries.OrderBy(_ => random.Next()).Take(3).ToArray();
                var tasks = shuffled.Select(q => SearchAsync(q, count / shuffled.Length));
                var searchResults = await Task.WhenAll(tasks);
                return searchResults.SelectMany(r => r).Distinct().Take(count).ToList();
            }

            #endregion

            #region Album, Artist, Playlist Support (via InnerTube browse)

            /// <summary>
            /// Get list of albums from YouTube Music.
            /// </summary>
            public async Task<IReadOnlyList<MusicFile>> GetAlbumListAsync(int count = 50)
            {
                var results = new List<MusicFile>();

                try
                {
                    var browseResponse = await _innerTube.BrowseAsync("FEmusic_new_releases_albums");
                    foreach (var item in browseResponse.Tracks.Take(count))
                    {
                        var musicFile = ConvertToMusicFile(item);
                        if (musicFile != null)
                            results.Add(musicFile);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[YouTube] GetAlbumListAsync failed: {ex.Message}");
                }

                return results;
            }

            /// <summary>
            /// Get tracks in an album by browse ID.
            /// </summary>
            public async Task<IReadOnlyList<MusicFile>> GetAlbumAsync(string albumId)
            {
                if (string.IsNullOrEmpty(albumId))
                    return new List<MusicFile>();

                string browseId = albumId;
                if (!albumId.StartsWith("MPRE") && !albumId.StartsWith("VLMP") && !albumId.StartsWith("OLAK"))
                {
                    browseId = $"VL{albumId}";
                }

                try
                {
                    if (_provider._metadataCache.TryGetBrowseResults(browseId, out var cached))
                    {
                        return cached.Tracks.Select(ConvertToMusicFile).Where(m => m != null).ToList();
                    }

                    var browseResponse = await _innerTube.BrowseAsync(browseId);
                    _provider._metadataCache.PutBrowseResults(browseId, browseResponse);

                    var results = new List<MusicFile>();
                    foreach (var track in browseResponse.Tracks)
                    {
                        var musicFile = ConvertToMusicFile(track);
                        if (musicFile != null)
                        {
                            musicFile.Album = browseResponse.Title ?? "Unknown Album";
                            results.Add(musicFile);
                        }
                    }

                    return results;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[YouTube] GetAlbumAsync failed for {albumId}: {ex.Message}");
                    return new List<MusicFile>();
                }
            }

            /// <summary>
            /// Get an artist's top songs by browse ID.
            /// </summary>
            public async Task<IReadOnlyList<MusicFile>> GetArtistAsync(string artistId)
            {
                if (string.IsNullOrEmpty(artistId))
                    return new List<MusicFile>();

                try
                {
                    if (_provider._metadataCache.TryGetBrowseResults(artistId, out var cached))
                    {
                        return cached.Tracks.Select(ConvertToMusicFile).Where(m => m != null).ToList();
                    }

                    var browseResponse = await _innerTube.BrowseAsync(artistId);
                    _provider._metadataCache.PutBrowseResults(artistId, browseResponse);

                    return browseResponse.Tracks
                        .Select(ConvertToMusicFile)
                        .Where(m => m != null)
                        .ToList();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[YouTube] GetArtistAsync failed for {artistId}: {ex.Message}");
                    return new List<MusicFile>();
                }
            }

            /// <summary>
            /// Get tracks from a YouTube Music playlist.
            /// </summary>
            public async Task<IReadOnlyList<MusicFile>> GetPlaylistAsync(string playlistId, int limit = 100)
            {
                if (string.IsNullOrEmpty(playlistId))
                    return new List<MusicFile>();

                string browseId = playlistId.StartsWith("VL") ? playlistId : $"VL{playlistId}";

                try
                {
                    var browseResponse = await _innerTube.BrowseAsync(browseId);
                    return browseResponse.Tracks
                        .Take(limit)
                        .Select(ConvertToMusicFile)
                        .Where(m => m != null)
                        .ToList();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[YouTube] GetPlaylistAsync failed for {playlistId}: {ex.Message}");
                    return new List<MusicFile>();
                }
            }

            /// <summary>
            /// Get related songs / radio for a video.
            /// </summary>
            public async Task<IReadOnlyList<MusicFile>> GetRadioAsync(string videoId, string playlistId = null)
            {
                if (string.IsNullOrEmpty(videoId))
                    return new List<MusicFile>();

                try
                {
                    var radioPlaylistId = playlistId ?? $"RDAMVM{videoId}";
                    var nextResponse = await _innerTube.GetNextAsync(videoId, radioPlaylistId);
                    return nextResponse.Items
                        .Select(ConvertToMusicFile)
                        .Where(m => m != null)
                        .ToList();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[YouTube] GetRadioAsync failed for {videoId}: {ex.Message}");
                    return new List<MusicFile>();
                }
            }

            /// <summary>
            /// Search across all categories and return grouped results.
            /// </summary>
            public async Task<Dictionary<string, List<MusicFile>>> SearchSummaryAsync(
                string query, int perTypeLimit = 5)
            {
                try
                {
                    var grouped = await _innerTube.SearchSummaryAsync(query);
                    return grouped.ToDictionary(
                        kvp => kvp.Key.ToString(),
                        kvp => kvp.Value
                            .Take(perTypeLimit)
                            .Select(ConvertToMusicFile)
                            .Where(m => m != null)
                            .ToList());
                }
                catch
                {
                    return new Dictionary<string, List<MusicFile>>();
                }
            }

            #endregion

            #region Conversion Helpers

            private MusicFile ConvertToMusicFile(InnerTubeClient.InnerTubeSearchItem item)
            {
                if (item == null) return null;
                if (item.Type == InnerTubeClient.SearchResultType.Unknown && string.IsNullOrEmpty(item.VideoId))
                    return null;

                var musicFile = new MusicFile
                {
                    FilePath = !string.IsNullOrEmpty(item.VideoId)
                        ? $"{_sourceId}:{item.VideoId}"
                        : $"{_sourceId}:browse:{item.BrowseId}",
                    Title = item.Title ?? "Unknown",
                    Artist = item.Artist ?? "YouTube Music",
                    Album = item.Album ?? (item.Type == InnerTubeClient.SearchResultType.Album ? item.Title : "YouTube Music"),
                    Genre = "Music",
                    SourceType = YouTube,
                    CoverPath = SanitizeThumbnailUrl(item.ThumbnailUrl),
                    Duration = item.Duration
                };

                // Store YouTube-specific metadata in SourceUri
                var metadata = new List<string>();
                if (!string.IsNullOrEmpty(item.BrowseId))
                    metadata.Add($"browse={item.BrowseId}");
                if (!string.IsNullOrEmpty(item.PlaylistId))
                    metadata.Add($"playlist={item.PlaylistId}");
                if (!string.IsNullOrEmpty(item.VideoId))
                    metadata.Add($"video={item.VideoId}");

                if (metadata.Count > 0)
                    musicFile.SourceUri = string.Join(";", metadata);

                // Store YouTube-specific IDs in dedicated fields
                musicFile.YouTubeVideoId = item.VideoId;
                musicFile.YouTubeBrowseId = item.BrowseId;
                musicFile.YouTubePlaylistId = item.PlaylistId;

                return musicFile;
            }

            /// <summary>
            /// Transforms YouTube thumbnail URLs to request JPEG format instead of WebP.
            /// WPF's BitmapImage cannot decode WebP natively (WIC error 0x88982F50).
            /// Uses Regex.Replace instead of string.Replace(string,string,StringComparison)
            /// because the 3-argument Replace overload is not available in .NET Framework 4.7.2.
            /// </summary>
            private static string SanitizeThumbnailUrl(string url)
            {
                if (string.IsNullOrEmpty(url))
                    return url;

                try
                {
                    // YouTube i.ytimg.com — convert /vi_webp/ paths to /vi/ with .jpg
                    if (url.IndexOf("ytimg.com", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        url = System.Text.RegularExpressions.Regex.Replace(
                            url, "/vi_webp/", "/vi/",
                            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        if (url.EndsWith(".webp", StringComparison.OrdinalIgnoreCase))
                            url = url.Substring(0, url.Length - 5) + ".jpg";
                        return url;
                    }

                    // Google CDN — replace WebP suffixes with JPEG (-rj)
                    if (url.IndexOf("googleusercontent.com", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        url.IndexOf("ggpht.com", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        if (url.IndexOf("-rw", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            url = System.Text.RegularExpressions.Regex.Replace(
                                url, @"-rw(?:-p)?(?=[-\s/&?]|$)", "-rj",
                                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        }
                        if (url.IndexOf("-no-rw", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            url = System.Text.RegularExpressions.Regex.Replace(
                                url, "-no-rw", "-no-rj",
                                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        }
                    }

                    // Generic .webp extension replacement
                    if (url.EndsWith(".webp", StringComparison.OrdinalIgnoreCase))
                        url = url.Substring(0, url.Length - 5) + ".jpg";
                }
                catch
                {
                    // If URL manipulation fails, use the original URL
                    // The PathToImageConverter will handle the fallback
                }

                return url;
            }

            #endregion

            /// <summary>
            /// Get all tracks from YouTube Music by fetching the user's library
            /// and trending content. Much more complete and efficient than SearchAsync("").
            /// </summary>
            public async Task<IReadOnlyList<MusicFile>> GetAllTracksAsync(int limit = 500)
            {
                var allTracks = new List<MusicFile>();

                // Strategy 1: Fetch trending/charts content from InnerTube
                try
                {
                    var chartsResults = await _innerTube.SearchAsync("", InnerTubeClient.SearchFilter.Song);
                    if (chartsResults?.Items != null)
                    {
                        foreach (var item in chartsResults.Items.Take(limit))
                        {
                            var musicFile = ConvertToMusicFile(item);
                            if (musicFile != null)
                                allTracks.Add(musicFile);
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[YouTube] GetAllTracksAsync InnerTube failed: {ex.Message}");
                }

                if (allTracks.Count >= limit)
                    return allTracks.Take(limit).ToList();

                // Strategy 2: Fetch random songs from YouTube home
                try
                {
                    var randomResults = await GetRandomSongsAsync(limit - allTracks.Count);
                    if (randomResults != null)
                    {
                        var existingIds = new HashSet<string>(allTracks.Select(t => t.FilePath));
                        foreach (var track in randomResults)
                        {
                            if (allTracks.Count >= limit) break;
                            if (!existingIds.Contains(track.FilePath))
                                allTracks.Add(track);
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[YouTube] GetAllTracksAsync random fallback failed: {ex.Message}");
                }

                return allTracks;
            }

            public void Dispose()
            {
                _youtube?.Dispose();
            }
        }
    }
}

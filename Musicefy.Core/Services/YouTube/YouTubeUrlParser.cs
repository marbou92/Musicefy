using System;
using System.Text.RegularExpressions;

namespace Musicefy.Core.Services.YouTubeApi
{
    /// <summary>
    /// Parser for various YouTube URL formats.
    /// Inspired by Echo Music's YouTubeUrlParser that handles watch, shorts, embed, and music URLs.
    /// </summary>
    public static class YouTubeUrlParser
    {
        // Video URL patterns (inspired by Echo Music's VIDEO_URL_PATTERNS)
        private static readonly Regex[] VideoUrlPatterns = new[]
        {
            // Standard watch URL
            new Regex(@"(?:https?://)?(?:www\.)?(?:music\.)?youtube\.com/watch\?.*v=([a-zA-Z0-9_-]{11})", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            // Short URL
            new Regex(@"(?:https?://)?youtu\.be/([a-zA-Z0-9_-]{11})", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            // Shorts URL
            new Regex(@"(?:https?://)?(?:www\.)?youtube\.com/shorts/([a-zA-Z0-9_-]{11})", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            // Embed URL
            new Regex(@"(?:https?://)?(?:www\.)?youtube\.com/embed/([a-zA-Z0-9_-]{11})", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            // Music watch URL
            new Regex(@"(?:https?://)?music\.youtube\.com/watch\?.*v=([a-zA-Z0-9_-]{11})", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        };

        // Playlist URL patterns
        private static readonly Regex[] PlaylistUrlPatterns = new[]
        {
            new Regex(@"(?:https?://)?(?:www\.)?(?:music\.)?youtube\.com/playlist\?.*list=([a-zA-Z0-9_-]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            new Regex(@"(?:https?://)?(?:www\.)?(?:music\.)?youtube\.com/watch\?.*list=([a-zA-Z0-9_-]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        };

        // Artist/channel URL patterns (inspired by Echo Music's ARTIST_URL_PATTERNS)
        private static readonly Regex[] ArtistUrlPatterns = new[]
        {
            new Regex(@"(?:https?://)?(?:www\.)?music\.youtube\.com/channel/(UC[a-zA-Z0-9_-]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            new Regex(@"(?:https?://)?(?:www\.)?music\.youtube\.com/browse/(MPRE[a-zA-Z0-9_-]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        };

        /// <summary>
        /// Parsed result of a YouTube URL.
        /// </summary>
        public class ParsedYouTubeUrl
        {
            public string VideoId { get; set; }
            public string PlaylistId { get; set; }
            public string BrowseId { get; set; }
            public UrlType Type { get; set; }
        }

        public enum UrlType
        {
            Video,
            Playlist,
            Artist,
            Album,
            Unknown
        }

        /// <summary>
        /// Parse a YouTube URL and extract the video/playlist/artist ID.
        /// </summary>
        public static ParsedYouTubeUrl Parse(string url)
        {
            if (string.IsNullOrEmpty(url))
                return new ParsedYouTubeUrl { Type = UrlType.Unknown };

            // Try video patterns
            foreach (var pattern in VideoUrlPatterns)
            {
                var match = pattern.Match(url);
                if (match.Success)
                {
                    var result = new ParsedYouTubeUrl
                    {
                        VideoId = match.Groups[1].Value,
                        Type = UrlType.Video
                    };

                    // Also check for playlist ID in the same URL
                    foreach (var plPattern in PlaylistUrlPatterns)
                    {
                        var plMatch = plPattern.Match(url);
                        if (plMatch.Success)
                        {
                            result.PlaylistId = plMatch.Groups[1].Value;
                            break;
                        }
                    }

                    return result;
                }
            }

            // Try playlist patterns
            foreach (var pattern in PlaylistUrlPatterns)
            {
                var match = pattern.Match(url);
                if (match.Success)
                    return new ParsedYouTubeUrl { PlaylistId = match.Groups[1].Value, Type = UrlType.Playlist };
            }

            // Try artist/channel patterns
            foreach (var pattern in ArtistUrlPatterns)
            {
                var match = pattern.Match(url);
                if (match.Success)
                {
                    var id = match.Groups[1].Value;
                    return new ParsedYouTubeUrl
                    {
                        BrowseId = id,
                        Type = id.StartsWith("MPRE") ? UrlType.Album : UrlType.Artist
                    };
                }
            }

            return new ParsedYouTubeUrl { Type = UrlType.Unknown };
        }

        /// <summary>
        /// Extract just the video ID from a URL or ID string.
        /// </summary>
        public static string ExtractVideoId(string urlOrId)
        {
            if (string.IsNullOrEmpty(urlOrId))
                return null;

            // If it's already a valid video ID (11 chars, alphanumeric + - _)
            if (IsValidVideoId(urlOrId))
                return urlOrId;

            // Try parsing as URL
            var parsed = Parse(urlOrId);
            return parsed.VideoId;
        }

        /// <summary>
        /// Check if a string is a valid YouTube video ID.
        /// </summary>
        public static bool IsValidVideoId(string id)
        {
            if (string.IsNullOrEmpty(id) || id.Length != 11)
                return false;

            foreach (char c in id)
            {
                if (!char.IsLetterOrDigit(c) && c != '-' && c != '_')
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Create a YouTube Music watch URL from a video ID.
        /// </summary>
        public static string CreateWatchUrl(string videoId)
        {
            return $"https://music.youtube.com/watch?v={videoId}";
        }
    }
}

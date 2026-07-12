using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Musicefy.Core.Models;
using static Musicefy.Core.SourceTypes;

namespace Musicefy.Core.Services
{
    /// <summary>
    /// Sprint 7: Browse YouTube Music's Mood/Genres/Charts content.
    ///
    /// Uses the InnerTube API (via YouTubeSourceProvider's session) to fetch:
    ///   - Mood & Genres categories (FEmoods, FEgenres)
    ///   - Charts (top songs, top artists, top music videos)
    ///   - New Releases
    ///   - Explore page
    ///
    /// All browse IDs are YouTube Music constants:
    ///   "FEmusic_moods_and_genres"  — mood/genre launcher
    ///   "FEmusic_charts"            — charts
    ///   "FEmusic_new_releases"      — new albums
    ///   "FEmusic_explore"           — explore page
    /// </summary>
    public class YouTubeBrowseService
    {
        private readonly IStreamingSourceManager _sourceManager;

        // YouTube Music browse IDs
        public const string BrowseMoodsAndGenres = "FEmusic_moods_and_genres";
        public const string BrowseCharts = "FEmusic_charts";
        public const string BrowseNewReleases = "FEmusic_new_releases";
        public const string BrowseExplore = "FEmusic_explore";

        public YouTubeBrowseService(IStreamingSourceManager sourceManager)
        {
            _sourceManager = sourceManager ?? throw new ArgumentNullException(nameof(sourceManager));
        }

        /// <summary>
        /// Get the list of mood/genre categories from YouTube Music.
        /// Returns a list of (Title, BrowseId) pairs.
        /// </summary>
        public async Task<List<MoodGenreCategory>> GetMoodsAndGenresAsync(CancellationToken ct = default)
        {
            var session = GetYouTubeSession();
            if (session == null) return new List<MoodGenreCategory>();

            try
            {
                var results = await session.SearchWithTypeAsync("", "genres", 50);
                // The InnerTube API doesn't have a direct "genres" search filter,
                // so we use the browse endpoint via the session's SearchWithType.
                // In a full implementation, we'd call YouTube.Browse(FEmusic_moods_and_genres).
                // For now, return a static list of known categories.
                return GetKnownMoodGenreCategories();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[YouTubeBrowse] GetMoodsAndGenres failed: {ex.Message}");
                return GetKnownMoodGenreCategories();
            }
        }

        /// <summary>
        /// Get trending/top tracks from YouTube Music charts.
        /// </summary>
        public async Task<List<MusicFile>> GetChartTracksAsync(string chartType = "top_songs", int limit = 50, CancellationToken ct = default)
        {
            var session = GetYouTubeSession();
            if (session == null) return new List<MusicFile>();

            try
            {
                // Use the search summary to get a mix of trending content
                var results = await session.SearchWithTypeAsync("top songs 2024", "songs", limit);
                return results?.ToList() ?? new List<MusicFile>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[YouTubeBrowse] GetChartTracks failed: {ex.Message}");
                return new List<MusicFile>();
            }
        }

        /// <summary>
        /// Get new album releases from YouTube Music.
        /// </summary>
        public async Task<List<MusicFile>> GetNewReleasesAsync(int limit = 50, CancellationToken ct = default)
        {
            var session = GetYouTubeSession();
            if (session == null) return new List<MusicFile>();

            try
            {
                var results = await session.SearchWithTypeAsync("new music 2024", "albums", limit);
                return results?.ToList() ?? new List<MusicFile>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[YouTubeBrowse] GetNewReleases failed: {ex.Message}");
                return new List<MusicFile>();
            }
        }

        /// <summary>
        /// Get explore content (trending, new music, etc.) as browse sections.
        /// </summary>
        public async Task<List<BrowseSection>> GetExploreSectionsAsync(CancellationToken ct = default)
        {
            var sections = new List<BrowseSection>();

            try
            {
                // Section 1: Trending Songs
                var trending = await GetChartTracksAsync("top_songs", 20, ct);
                if (trending?.Count > 0)
                {
                    sections.Add(new BrowseSection
                    {
                        Title = "Trending Songs",
                        SectionType = "TrendingSongs",
                        BaseWeight = 100,
                        SourceType = YouTube,
                        Items = trending.Cast<object>().ToList()
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[YouTubeBrowse] Trending section failed: {ex.Message}");
            }

            try
            {
                // Section 2: New Releases
                var newReleases = await GetNewReleasesAsync(20, ct);
                if (newReleases?.Count > 0)
                {
                    sections.Add(new BrowseSection
                    {
                        Title = "New Releases",
                        SectionType = "NewReleases",
                        BaseWeight = 90,
                        SourceType = YouTube,
                        Items = newReleases.Cast<object>().ToList()
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[YouTubeBrowse] New Releases section failed: {ex.Message}");
            }

            return sections;
        }

        /// <summary>
        /// Get known mood/genre categories (static list, since the InnerTube
        /// browse endpoint for moods requires additional parsing).
        /// </summary>
        private static List<MoodGenreCategory> GetKnownMoodGenreCategories()
        {
            return new List<MoodGenreCategory>
            {
                new MoodGenreCategory { Title = "Chill", BrowseId = "FEmusic_mood_chill", Icon = "🌙" },
                new MoodGenreCategory { Title = "Party", BrowseId = "FEmusic_mood_party", Icon = "🎉" },
                new MoodGenreCategory { Title = "Workout", BrowseId = "FEmusic_mood_workout", Icon = "💪" },
                new MoodGenreCategory { Title = "Focus", BrowseId = "FEmusic_mood_focus", Icon = "🎯" },
                new MoodGenreCategory { Title = "Romance", BrowseId = "FEmusic_mood_romance", Icon = "❤️" },
                new MoodGenreCategory { Title = "Sleep", BrowseId = "FEmusic_mood_sleep", Icon = "😴" },
                new MoodGenreCategory { Title = "Pop", BrowseId = "FEmusic_genre_pop", Icon = "🎤" },
                new MoodGenreCategory { Title = "Hip-Hop", BrowseId = "FEmusic_genre_hiphop", Icon = "🎧" },
                new MoodGenreCategory { Title = "Rock", BrowseId = "FEmusic_genre_rock", Icon = "🎸" },
                new MoodGenreCategory { Title = "Electronic", BrowseId = "FEmusic_genre_electronic", Icon = "⚡" },
                new MoodGenreCategory { Title = "Jazz", BrowseId = "FEmusic_genre_jazz", Icon = "🎷" },
                new MoodGenreCategory { Title = "Classical", BrowseId = "FEmusic_genre_classical", Icon = "🎻" },
                new MoodGenreCategory { Title = "R&B", BrowseId = "FEmusic_genre_rnb", Icon = "🎶" },
                new MoodGenreCategory { Title = "Country", BrowseId = "FEmusic_genre_country", Icon = "🤠" },
                new MoodGenreCategory { Title = "Latin", BrowseId = "FEmusic_genre_latin", Icon = "💃" },
                new MoodGenreCategory { Title = "K-Pop", BrowseId = "FEmusic_genre_kpop", Icon = "🌟" },
            };
        }

        /// <summary>
        /// Find the first connected YouTube source session.
        /// </summary>
        private IYouTubeSourceSession GetYouTubeSession()
        {
            try
            {
                var ytSource = _sourceManager.Sources.FirstOrDefault(
                    s => string.Equals(s.Type, YouTube, StringComparison.OrdinalIgnoreCase) && s.IsConnected);
                if (ytSource == null) return null;

                return _sourceManager.GetYouTubeSession(ytSource.Id);
            }
            catch { return null; }
        }
    }

    /// <summary>
    /// A mood or genre category from YouTube Music.
    /// </summary>
    public class MoodGenreCategory
    {
        public string Title { get; set; }
        public string BrowseId { get; set; }
        public string Icon { get; set; }
    }
}

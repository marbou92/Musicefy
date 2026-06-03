using System;
using System.Collections.ObjectModel;

namespace Musicefy.Core.Models
{
    /// <summary>
    /// Represents the different types of sections on the Home screen,
    /// inspired by Echo Music's HomeScreen but adapted for WPF/MVVM multi-source architecture.
    /// </summary>
    public enum HomeSectionType
    {
        /// <summary>Quick picks based on user listening habits.</summary>
        QuickPicks,

        /// <summary>Resume or continue listening to partially played content.</summary>
        KeepListening,

        /// <summary>Recently played tracks or albums.</summary>
        RecentlyPlayed,

        /// <summary>Daily discovery recommendations tailored to the user.</summary>
        DailyDiscover,

        /// <summary>Recommendations similar to what the user enjoys.</summary>
        SimilarRecommendation,

        /// <summary>Content fetched from the YouTube Home Feed.</summary>
        YouTubeHome,

        /// <summary>Albums available from a Subsonic source.</summary>
        SubsonicAlbums
    }

    /// <summary>
    /// Represents a section on the Home screen, containing a titled group of items
    /// sourced from a specific music provider. Designed as a concrete (non-abstract)
    /// class so that WPF data binding can work directly with instances.
    /// </summary>
    public class HomeSection
    {
        /// <summary>Display title for this section.</summary>
        public string Title { get; set; }

        /// <summary>The type of home section, used for ordering and rendering logic.</summary>
        public HomeSectionType SectionType { get; set; }

        /// <summary>
        /// Ordering weight — higher values mean the section appears more prominently.
        /// Default is 50.
        /// </summary>
        public int BaseWeight { get; set; } = 50;

        /// <summary>
        /// Identifier of the source that provided this section
        /// (e.g. a YouTube account ID or Subsonic server ID).
        /// </summary>
        public string SourceId { get; set; }

        /// <summary>
        /// Category of the source: "Local", "YouTube", or "Subsonic".
        /// </summary>
        public string SourceType { get; set; }

        /// <summary>
        /// Items contained in this section. Can hold MusicFile, ArtistInfo, or AlbumInfo objects.
        /// Uses ObservableCollection for WPF binding support.
        /// </summary>
        public ObservableCollection<object> Items { get; }

        /// <summary>
        /// Indicates whether this section is currently loading its data.
        /// Useful for per-section loading spinners in the UI.
        /// </summary>
        public bool IsLoading { get; set; }

        /// <summary>
        /// Continuation token for infinite-scroll scenarios
        /// (e.g. YouTube Home Feed pagination).
        /// </summary>
        public string ContinuationToken { get; set; }

        /// <summary>Initializes a new HomeSection with an empty Items collection.</summary>
        public HomeSection()
        {
            Items = new ObservableCollection<object>();
        }
    }

    /// <summary>
    /// Represents the overall loading state of the Home screen.
    /// </summary>
    public enum HomeLoadState
    {
        /// <summary>Loading has not started yet.</summary>
        NotStarted,

        /// <summary>Local sources are being loaded.</summary>
        LoadingLocal,

        /// <summary>Network sources are being loaded.</summary>
        LoadingNetwork,

        /// <summary>All sources have been loaded successfully.</summary>
        Loaded,

        /// <summary>An error occurred during loading.</summary>
        Error
    }

    /// <summary>
    /// Holds the aggregate loading state for the Home screen,
    /// including error information and last refresh timestamp.
    /// </summary>
    public class HomeState
    {
        /// <summary>Current loading state of the Home screen.</summary>
        public HomeLoadState State { get; set; }

        /// <summary>Error message when State is Error; otherwise null or empty.</summary>
        public string ErrorMessage { get; set; }

        /// <summary>Timestamp of the last successful refresh, if any.</summary>
        public DateTime? LastRefreshed { get; set; }
    }
}

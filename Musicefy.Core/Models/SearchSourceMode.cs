namespace Musicefy.Core.Models
{
    /// <summary>
    /// Determines which search backend is active.
    /// Inspired by Echo Music's dual search system where local and
    /// online search are separate code paths.
    /// </summary>
    public enum SearchSourceMode
    {
        /// <summary>Search the local SQLite library only.</summary>
        Local,

        /// <summary>Search all online streaming sources (YouTube, Subsonic, etc.).</summary>
        Online
    }

    /// <summary>
    /// Represents the four states of the search state machine.
    /// Mirrors Echo Music's SearchScreen state flow:
    /// Idle → Suggestions → Searching → Results
    /// </summary>
    public enum SearchState
    {
        /// <summary>No query; showing history and explore tabs.</summary>
        Idle,

        /// <summary>Query changed (debounced); fetching autocomplete suggestions.</summary>
        Suggestions,

        /// <summary>Search is in progress across selected sources.</summary>
        Searching,

        /// <summary>Results are available for display.</summary>
        Results
    }

    /// <summary>
    /// Filter tabs for categorized search results.
    /// Mirrors YouTube Music's result type filters.
    /// </summary>
    public enum SearchResultFilter
    {
        /// <summary>Show all result categories grouped together.</summary>
        All,

        /// <summary>Show only songs/tracks.</summary>
        Songs,

        /// <summary>Show only albums.</summary>
        Albums,

        /// <summary>Show only artists.</summary>
        Artists,

        /// <summary>Show only playlists.</summary>
        Playlists
    }
}

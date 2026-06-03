using System.Collections.ObjectModel;

namespace Musicefy.Core.Models
{
    /// <summary>
    /// Represents a categorized group of search results, used for the
    /// grouped "All" view and filtered single-category view.
    /// Inspired by Echo Music's <c>SearchSummary</c> response which returns
    /// results grouped by type (Top Result, Songs, Videos, Albums, Artists, Playlists).
    /// </summary>
    public class SearchResultGroup
    {
        /// <summary>
        /// Category name for this group (e.g. "Songs", "Albums", "Top Result", "Artists").
        /// </summary>
        public string Category { get; set; }

        /// <summary>
        /// Results in this category. Can contain <see cref="MusicFile"/>,
        /// <see cref="ArtistInfo"/>, or <see cref="AlbumInfo"/> objects.
        /// </summary>
        public ObservableCollection<object> Items { get; } = new ObservableCollection<object>();

        /// <summary>
        /// Whether more results are available for this category (pagination).
        /// </summary>
        public bool HasMore { get; set; }

        /// <summary>
        /// Display order weight — higher values appear first in the "All" view.
        /// </summary>
        public int DisplayOrder { get; set; }
    }
}

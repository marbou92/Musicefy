using System;

namespace Musicefy.Core.Models
{
    /// <summary>
    /// Represents a single search history entry, persisted in SQLite.
    /// Inspired by Echo Music's Room-based search history but adapted
    /// for Musicefy's multi-source architecture.
    /// </summary>
    public class SearchHistory
    {
        /// <summary>Auto-incremented row ID.</summary>
        public int Id { get; set; }

        /// <summary>The search query text.</summary>
        public string Query { get; set; }

        /// <summary>Timestamp when the search was performed.</summary>
        public DateTime SearchedAt { get; set; }

        /// <summary>
        /// Source mode used for this search: "Local" or "Online".
        /// Matches <see cref="SearchSourceMode"/> values.
        /// </summary>
        public string SourceType { get; set; }
    }
}

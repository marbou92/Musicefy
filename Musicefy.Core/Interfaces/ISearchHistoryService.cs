using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Musicefy.Core.Models;

namespace Musicefy.Core.Interfaces
{
    /// <summary>
    /// Interface for search history persistence.
    /// Inspired by Echo Music's Room-based search history,
    /// adapted for Musicefy's SQLite + Dapper stack.
    /// </summary>
    public interface ISearchHistoryService
    {
        /// <summary>
        /// Save a search query to history. If the same query already exists
        /// for the same source type, its timestamp is updated instead of
        /// creating a duplicate.
        /// </summary>
        Task SaveAsync(string query, string sourceType, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get recent search history entries, ordered by most recent first.
        /// </summary>
        /// <param name="limit">Maximum number of entries to return.</param>
        Task<List<SearchHistory>> GetRecentAsync(int limit = 10, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get search history entries matching a query prefix, for autocomplete.
        /// </summary>
        Task<List<SearchHistory>> SearchByPrefixAsync(string prefix, int limit = 5, CancellationToken cancellationToken = default);

        /// <summary>
        /// Clear all search history.
        /// </summary>
        Task ClearAsync(CancellationToken cancellationToken = default);
    }
}

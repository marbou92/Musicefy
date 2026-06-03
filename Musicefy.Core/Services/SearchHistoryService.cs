using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.Sqlite;
using Musicefy.Core.Configuration;
using Musicefy.Core.Interfaces;
using Musicefy.Core.Models;

namespace Musicefy.Core.Services
{
    /// <summary>
    /// Search history persistence using SQLite + Dapper.
    /// Shares the same database as the music library.
    /// Inspired by Echo Music's Room-based search history but
    /// adapted for Musicefy's infrastructure.
    /// </summary>
    public class SearchHistoryService : ISearchHistoryService
    {
        private readonly string _connectionString;
        private bool _schemaEnsured;

        public SearchHistoryService()
        {
            _connectionString = DatabaseConfig.ConnectionString;
        }

        /// <summary>
        /// Constructor that accepts a connection string directly.
        /// Used for DI where DatabaseConfig might not be initialized yet.
        /// </summary>
        public SearchHistoryService(string connectionString)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        }

        private async Task EnsureSchemaAsync(CancellationToken ct = default)
        {
            if (_schemaEnsured) return;

            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync(ct);

                await connection.ExecuteAsync(@"
                    CREATE TABLE IF NOT EXISTS SearchHistory (
                        Id         INTEGER PRIMARY KEY AUTOINCREMENT,
                        Query      TEXT NOT NULL,
                        SearchedAt TEXT NOT NULL,
                        SourceType TEXT NOT NULL
                    );");

                await connection.ExecuteAsync(@"
                    CREATE INDEX IF NOT EXISTS idx_searchhistory_query
                    ON SearchHistory(Query COLLATE NOCASE);");

                await connection.ExecuteAsync(@"
                    CREATE INDEX IF NOT EXISTS idx_searchhistory_searchedat
                    ON SearchHistory(SearchedAt DESC);");
            }

            _schemaEnsured = true;
        }

        public async Task SaveAsync(string query, string sourceType, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(query)) return;

            await EnsureSchemaAsync(cancellationToken);

            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync(cancellationToken);

                // Upsert: if same query+sourceType exists, update timestamp
                var existing = await connection.QueryFirstOrDefaultAsync<SearchHistory>(
                    @"SELECT * FROM SearchHistory
                      WHERE Query = @Query AND SourceType = @SourceType
                      COLLATE NOCASE
                      LIMIT 1",
                    new { Query = query.Trim(), SourceType = sourceType });

                if (existing != null)
                {
                    await connection.ExecuteAsync(
                        @"UPDATE SearchHistory SET SearchedAt = @SearchedAt
                          WHERE Id = @Id",
                        new { SearchedAt = DateTime.UtcNow.ToString("o"), Id = existing.Id });
                }
                else
                {
                    await connection.ExecuteAsync(
                        @"INSERT INTO SearchHistory (Query, SearchedAt, SourceType)
                          VALUES (@Query, @SearchedAt, @SourceType)",
                        new
                        {
                            Query = query.Trim(),
                            SearchedAt = DateTime.UtcNow.ToString("o"),
                            SourceType = sourceType
                        });
                }

                // Prune: keep only the latest 100 entries
                await connection.ExecuteAsync(@"
                    DELETE FROM SearchHistory
                    WHERE Id NOT IN (
                        SELECT Id FROM SearchHistory
                        ORDER BY SearchedAt DESC
                        LIMIT 100
                    );");
            }
        }

        public async Task<List<SearchHistory>> GetRecentAsync(int limit = 10, CancellationToken cancellationToken = default)
        {
            await EnsureSchemaAsync(cancellationToken);

            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync(cancellationToken);
                var results = await connection.QueryAsync<SearchHistory>(
                    @"SELECT * FROM SearchHistory
                      ORDER BY SearchedAt DESC
                      LIMIT @Limit",
                    new { Limit = limit });
                return results.AsList();
            }
        }

        public async Task<List<SearchHistory>> SearchByPrefixAsync(string prefix, int limit = 5, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(prefix))
                return new List<SearchHistory>();

            await EnsureSchemaAsync(cancellationToken);

            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync(cancellationToken);
                var pattern = $"{prefix}%";
                var results = await connection.QueryAsync<SearchHistory>(
                    @"SELECT * FROM SearchHistory
                      WHERE Query LIKE @Pattern
                      ORDER BY SearchedAt DESC
                      LIMIT @Limit",
                    new { Pattern = pattern, Limit = limit });
                return results.AsList();
            }
        }

        public async Task ClearAsync(CancellationToken cancellationToken = default)
        {
            await EnsureSchemaAsync(cancellationToken);

            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync(cancellationToken);
                await connection.ExecuteAsync("DELETE FROM SearchHistory");
            }
        }
    }
}

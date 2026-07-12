using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.Sqlite;
using Musicefy.Core.Models;

namespace Musicefy.Core.Services
{
    /// <summary>
    /// Sprint 5: Provides access to listen history and statistics data.
    /// Reads from the PlayEvents table (populated by LibraryScanner.RecordPlayAsync)
    /// and the Tracks table.
    /// </summary>
    public class HistoryService
    {
        private readonly string _connectionString;

        public HistoryService(string connectionString)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        }

        /// <summary>
        /// Returns recent play events joined with track metadata.
        /// Most recent first.
        /// </summary>
        public async Task<List<HistoryEntry>> GetRecentHistoryAsync(int limit = 100, CancellationToken cancellationToken = default)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var sql = @"
                SELECT pe.Id AS Id,
                       pe.TrackFilePath AS TrackFilePath,
                       pe.Timestamp AS Timestamp,
                       pe.PlayTimeMs AS PlayTimeMs,
                       t.Title AS Title,
                       t.Artist AS Artist,
                       t.Album AS Album,
                       t.Duration AS Duration,
                       t.CoverPath AS CoverPath,
                       t.SourceType AS SourceType,
                       t.YouTubeVideoId AS YouTubeVideoId
                FROM PlayEvents pe
                LEFT JOIN Tracks t ON pe.TrackFilePath = t.FilePath
                ORDER BY pe.Timestamp DESC
                LIMIT @Limit";

            var rows = await connection.QueryAsync<HistoryEntryRow>(sql, new { Limit = limit });
            var results = new List<HistoryEntry>();

            foreach (var row in rows)
            {
                results.Add(new HistoryEntry
                {
                    Id = row.Id,
                    TrackFilePath = row.TrackFilePath,
                    Timestamp = DateTime.TryParse(row.Timestamp, out var ts) ? ts : DateTime.MinValue,
                    PlayTimeMs = row.PlayTimeMs,
                    Title = row.Title ?? "Unknown",
                    Artist = row.Artist ?? "Unknown Artist",
                    Album = row.Album ?? "",
                    Duration = TimeSpan.TryParse(row.Duration, out var d) ? d : TimeSpan.Zero,
                    CoverPath = row.CoverPath,
                    SourceType = row.SourceType ?? "Local",
                    YouTubeVideoId = row.YouTubeVideoId
                });
            }

            return results;
        }

        /// <summary>
        /// Returns play count grouped by day for the last N days.
        /// </summary>
        public async Task<List<DailyPlayCount>> GetDailyPlayCountsAsync(int days = 30, CancellationToken cancellationToken = default)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var sql = @"
                SELECT DATE(Timestamp) AS Date, COUNT(*) AS Count
                FROM PlayEvents
                WHERE Timestamp >= @Cutoff
                GROUP BY DATE(Timestamp)
                ORDER BY Date DESC";

            var cutoff = DateTime.UtcNow.AddDays(-days).ToString("o");
            var rows = await connection.QueryAsync<DailyPlayCountRow>(sql, new { Cutoff = cutoff });

            return rows.Select(r => new DailyPlayCount
            {
                Date = DateTime.TryParse(r.Date, out var d) ? d : DateTime.MinValue,
                Count = r.Count
            }).ToList();
        }

        /// <summary>
        /// Returns the most played tracks in the given time period.
        /// </summary>
        public async Task<List<StatsEntry>> GetTopTracksAsync(int days = 30, int limit = 50, CancellationToken cancellationToken = default)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var sql = @"
                SELECT t.Title AS Title, t.Artist AS Artist, t.Album AS Album,
                       t.CoverPath AS CoverPath, t.SourceType AS SourceType,
                       COUNT(pe.Id) AS PlayCount
                FROM PlayEvents pe
                LEFT JOIN Tracks t ON pe.TrackFilePath = t.FilePath
                WHERE pe.Timestamp >= @Cutoff AND t.Title IS NOT NULL
                GROUP BY pe.TrackFilePath
                ORDER BY PlayCount DESC
                LIMIT @Limit";

            var cutoff = DateTime.UtcNow.AddDays(-days).ToString("o");
            var rows = await connection.QueryAsync<StatsEntry>(sql, new { Cutoff = cutoff, Limit = limit });

            return rows.ToList();
        }

        /// <summary>
        /// Returns the most played artists in the given time period.
        /// </summary>
        public async Task<List<StatsEntry>> GetTopArtistsAsync(int days = 30, int limit = 20, CancellationToken cancellationToken = default)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var sql = @"
                SELECT t.Artist AS Artist, COUNT(pe.Id) AS PlayCount
                FROM PlayEvents pe
                LEFT JOIN Tracks t ON pe.TrackFilePath = t.FilePath
                WHERE pe.Timestamp >= @Cutoff AND t.Artist IS NOT NULL AND t.Artist != ''
                GROUP BY t.Artist
                ORDER BY PlayCount DESC
                LIMIT @Limit";

            var cutoff = DateTime.UtcNow.AddDays(-days).ToString("o");
            var rows = await connection.QueryAsync<StatsEntry>(sql, new { Cutoff = cutoff, Limit = limit });

            return rows.ToList();
        }

        /// <summary>
        /// Returns the most played albums in the given time period.
        /// </summary>
        public async Task<List<StatsEntry>> GetTopAlbumsAsync(int days = 30, int limit = 20, CancellationToken cancellationToken = default)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var sql = @"
                SELECT t.Album AS Album, t.Artist AS Artist, t.CoverPath AS CoverPath,
                       COUNT(pe.Id) AS PlayCount
                FROM PlayEvents pe
                LEFT JOIN Tracks t ON pe.TrackFilePath = t.FilePath
                WHERE pe.Timestamp >= @Cutoff AND t.Album IS NOT NULL AND t.Album != ''
                GROUP BY t.Album
                ORDER BY PlayCount DESC
                LIMIT @Limit";

            var cutoff = DateTime.UtcNow.AddDays(-days).ToString("o");
            var rows = await connection.QueryAsync<StatsEntry>(sql, new { Cutoff = cutoff, Limit = limit });

            return rows.ToList();
        }

        /// <summary>
        /// Clears all play history (PlayEvents table).
        /// </summary>
        public async Task ClearHistoryAsync(CancellationToken cancellationToken = default)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await connection.ExecuteAsync("DELETE FROM PlayEvents");
        }

        /// <summary>
        /// Returns total play count and total listening time.
        /// </summary>
        public async Task<StatsSummary> GetStatsSummaryAsync(int days = 30, CancellationToken cancellationToken = default)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var cutoff = DateTime.UtcNow.AddDays(-days).ToString("o");
            var sql = @"
                SELECT COUNT(*) AS TotalPlays,
                       COUNT(DISTINCT pe.TrackFilePath) AS UniqueTracks,
                       COUNT(DISTINCT t.Artist) AS UniqueArtists,
                       COUNT(DISTINCT t.Album) AS UniqueAlbums
                FROM PlayEvents pe
                LEFT JOIN Tracks t ON pe.TrackFilePath = t.FilePath
                WHERE pe.Timestamp >= @Cutoff";

            var summary = await connection.QueryFirstOrDefaultAsync<StatsSummary>(sql, new { Cutoff = cutoff });
            if (summary == null)
                summary = new StatsSummary();
            summary.PeriodDays = days;

            return summary;
        }
    }

    // ── Internal row types for Dapper (strongly-typed, no dynamic) ─────────

    internal class HistoryEntryRow
    {
        public long Id { get; set; }
        public string TrackFilePath { get; set; }
        public string Timestamp { get; set; }
        public long PlayTimeMs { get; set; }
        public string Title { get; set; }
        public string Artist { get; set; }
        public string Album { get; set; }
        public string Duration { get; set; }
        public string CoverPath { get; set; }
        public string SourceType { get; set; }
        public string YouTubeVideoId { get; set; }
    }

    internal class DailyPlayCountRow
    {
        public string Date { get; set; }
        public int Count { get; set; }
    }

    // ── Data models ────────────────────────────────────────────────────────

    public class HistoryEntry
    {
        public long Id { get; set; }
        public string TrackFilePath { get; set; }
        public DateTime Timestamp { get; set; }
        public long PlayTimeMs { get; set; }
        public string Title { get; set; }
        public string Artist { get; set; }
        public string Album { get; set; }
        public TimeSpan Duration { get; set; }
        public string CoverPath { get; set; }
        public string SourceType { get; set; }
        public string YouTubeVideoId { get; set; }

        public string RelativeTime
        {
            get
            {
                var elapsed = DateTime.UtcNow - Timestamp;
                if (elapsed.TotalMinutes < 1) return "Just now";
                if (elapsed.TotalMinutes < 60) return $"{(int)elapsed.TotalMinutes}m ago";
                if (elapsed.TotalHours < 24) return $"{(int)elapsed.TotalHours}h ago";
                if (elapsed.TotalDays < 7) return $"{(int)elapsed.TotalDays}d ago";
                return Timestamp.ToLocalTime().ToString("MMM d, yyyy");
            }
        }
    }

    public class DailyPlayCount
    {
        public DateTime Date { get; set; }
        public int Count { get; set; }
    }

    public class StatsEntry
    {
        public string Title { get; set; }
        public string Artist { get; set; }
        public string Album { get; set; }
        public string CoverPath { get; set; }
        public string SourceType { get; set; }
        public int PlayCount { get; set; }
    }

    public class StatsSummary
    {
        public int TotalPlays { get; set; }
        public int UniqueTracks { get; set; }
        public int UniqueArtists { get; set; }
        public int UniqueAlbums { get; set; }
        public int PeriodDays { get; set; }
    }
}

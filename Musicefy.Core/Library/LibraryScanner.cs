using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Dapper;
using Musicefy.Core.Interfaces;
using Musicefy.Core.Models;

namespace Musicefy.Core.Library
{
    /// <summary>
    /// Rich progress snapshot reported on every batch tick during a library scan.
    /// Matches the style used by MusicBee / foobar2000: file count, percent, and
    /// the current filename are all surfaced so the UI can show a live status.
    /// </summary>
    public sealed class ScanProgressInfo
    {
        public int    Percent          { get; set; }
        public int    FilesProcessed   { get; set; }
        public int    TotalFiles       { get; set; }
        public int    NewTracksAdded   { get; set; }
        public int    TracksUpdated    { get; set; }
        public int    TracksRemoved    { get; set; }
        public string CurrentFileName  { get; set; } = string.Empty;
        public bool   IsComplete       { get; set; }
    }

    /// <summary>
    /// Cached metadata entry with file fingerprint for change detection
    /// </summary>
    public sealed class MetadataCacheEntry
    {
        public string FilePath { get; set; }
        public string LastModified { get; set; }
        public long FileSize { get; set; }
        public MusicFile Metadata { get; set; }
        public DateTime CachedAt { get; set; }
    }

    public class LibraryScanner : ILibraryService
    {
        // ── Configuration ──────────────────────────────────────────────────
        private readonly string   _dbConnectionString;
        
        // ── Metadata Cache (in-memory, thread-safe) ────────────────────────
        private readonly ConcurrentDictionary<string, MetadataCacheEntry> _metadataCache;
        private readonly int _maxCacheSize = 10000;
        private readonly object _cacheLock = new object();

        // All formats MusicBee/foobar2000 support that TagLib# can read
        private static readonly HashSet<string> ValidExtensions = Musicefy.Core.Models.MusicFileExtensions.All;

        // Same artwork filename priority list as MusicBee
        private static readonly string[] FolderArtNames =
        {
            "cover.jpg",   "cover.png",
            "folder.jpg",  "folder.png",
            "front.jpg",   "front.png",
            "album.jpg",   "album.png",
            "artwork.jpg", "artwork.png",
            "thumb.jpg",   "thumb.png"
        };

        // ── Constructor ────────────────────────────────────────────────────
        public LibraryScanner(string dbConnectionString)
        {
            _dbConnectionString = dbConnectionString;
            _metadataCache = new ConcurrentDictionary<string, MetadataCacheEntry>(StringComparer.OrdinalIgnoreCase);
        }

        // ── Schema ─────────────────────────────────────────────────────────
        /// <summary>
        /// Creates the Tracks table (if missing) and performs non-destructive
        /// column migrations so older databases get the new columns automatically.
        /// Call once on app startup before any scan or query.
        /// Includes full indexing on Artist, Album, Title, Favourites, and LastPlayed
        /// for instant search and sort performance.
        /// </summary>
        public async Task EnsureSchemaAsync(CancellationToken cancellationToken = default)
        {
            using (var connection = new SqliteConnection(_dbConnectionString))
            {
                await connection.OpenAsync(cancellationToken);

                await connection.ExecuteAsync(@"
                    CREATE TABLE IF NOT EXISTS Tracks (
                        FilePath     TEXT PRIMARY KEY,
                        Title        TEXT,
                        Artist       TEXT,
                        Album        TEXT,
                        Year         INTEGER,
                        Genre        TEXT,
                        Duration     TEXT,
                        TrackNumber  INTEGER,
                        Bitrate      INTEGER,
                        FileSize     INTEGER,
                        CoverPath    TEXT,
                        Lyrics       TEXT,
                        SourceUri    TEXT,
                        SourceType   TEXT,
                        LastModified TEXT,
                        IsFavourite  INTEGER DEFAULT 0,
                        PlayCount    INTEGER DEFAULT 0,
                        LastPlayed   TEXT,
                        IsDownloaded INTEGER DEFAULT 0
                    );");

                // Non-destructive migration — add columns that may not exist in older DBs
                var existingCols = (await connection.QueryAsync<string>(
                    "SELECT name FROM pragma_table_info('Tracks')"))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                if (!existingCols.Contains("LastModified"))
                    await connection.ExecuteAsync("ALTER TABLE Tracks ADD COLUMN LastModified TEXT;");
                if (!existingCols.Contains("Lyrics"))
                    await connection.ExecuteAsync("ALTER TABLE Tracks ADD COLUMN Lyrics TEXT;");
                if (!existingCols.Contains("IsFavourite"))
                    await connection.ExecuteAsync("ALTER TABLE Tracks ADD COLUMN IsFavourite INTEGER DEFAULT 0;");
                if (!existingCols.Contains("PlayCount"))
                    await connection.ExecuteAsync("ALTER TABLE Tracks ADD COLUMN PlayCount INTEGER DEFAULT 0;");
                if (!existingCols.Contains("LastPlayed"))
                    await connection.ExecuteAsync("ALTER TABLE Tracks ADD COLUMN LastPlayed TEXT;");
                if (!existingCols.Contains("IsDownloaded"))
                    await connection.ExecuteAsync("ALTER TABLE Tracks ADD COLUMN IsDownloaded INTEGER DEFAULT 0;");

                // YouTube-specific fields (Phase 0a: persist YouTube metadata)
                if (!existingCols.Contains("YouTubeVideoId"))
                    await connection.ExecuteAsync("ALTER TABLE Tracks ADD COLUMN YouTubeVideoId TEXT;");
                if (!existingCols.Contains("YouTubeBrowseId"))
                    await connection.ExecuteAsync("ALTER TABLE Tracks ADD COLUMN YouTubeBrowseId TEXT;");
                if (!existingCols.Contains("YouTubePlaylistId"))
                    await connection.ExecuteAsync("ALTER TABLE Tracks ADD COLUMN YouTubePlaylistId TEXT;");
                if (!existingCols.Contains("YouTubeMusicVideoType"))
                    await connection.ExecuteAsync("ALTER TABLE Tracks ADD COLUMN YouTubeMusicVideoType TEXT;");
                if (!existingCols.Contains("LoudnessDb"))
                    await connection.ExecuteAsync("ALTER TABLE Tracks ADD COLUMN LoudnessDb REAL;");
                if (!existingCols.Contains("AudioFormat"))
                    await connection.ExecuteAsync("ALTER TABLE Tracks ADD COLUMN AudioFormat TEXT;");
                if (!existingCols.Contains("AlbumArtist"))
                    await connection.ExecuteAsync("ALTER TABLE Tracks ADD COLUMN AlbumArtist TEXT;");
                if (!existingCols.Contains("AlbumBrowseId"))
                    await connection.ExecuteAsync("ALTER TABLE Tracks ADD COLUMN AlbumBrowseId TEXT;");
                if (!existingCols.Contains("ArtistBrowseId"))
                    await connection.ExecuteAsync("ALTER TABLE Tracks ADD COLUMN ArtistBrowseId TEXT;");
                if (!existingCols.Contains("DateAdded"))
                    await connection.ExecuteAsync("ALTER TABLE Tracks ADD COLUMN DateAdded TEXT;");

                await connection.ExecuteAsync(@"
                    CREATE TABLE IF NOT EXISTS Playlists (
                        Id        TEXT PRIMARY KEY,
                        Name      TEXT NOT NULL,
                        CreatedAt TEXT NOT NULL
                    );");

                // ── Phase 5: PlaylistTracks junction table & Playlists enrichment ──
                // Enables ordered track membership for playlists.
                // Inspired by Echo Music's playlist model with full CRUD and reorder.

                // Non-destructive migration — add columns to Playlists that may not exist
                var playlistCols = (await connection.QueryAsync<string>(
                    "SELECT name FROM pragma_table_info('Playlists')"))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                if (!playlistCols.Contains("LastModifiedAt"))
                    await connection.ExecuteAsync("ALTER TABLE Playlists ADD COLUMN LastModifiedAt TEXT;");
                if (!playlistCols.Contains("Description"))
                    await connection.ExecuteAsync("ALTER TABLE Playlists ADD COLUMN Description TEXT;");
                if (!playlistCols.Contains("CoverPath"))
                    await connection.ExecuteAsync("ALTER TABLE Playlists ADD COLUMN CoverPath TEXT;");
                if (!playlistCols.Contains("YouTubePlaylistId"))
                    await connection.ExecuteAsync("ALTER TABLE Playlists ADD COLUMN YouTubePlaylistId TEXT;");
                if (!playlistCols.Contains("SourceType"))
                    await connection.ExecuteAsync("ALTER TABLE Playlists ADD COLUMN SourceType TEXT;");

                await connection.ExecuteAsync(@"
                    CREATE TABLE IF NOT EXISTS PlaylistTracks (
                        PlaylistId  TEXT NOT NULL,
                        TrackFilePath TEXT NOT NULL,
                        Position    INTEGER NOT NULL,
                        PRIMARY KEY (PlaylistId, TrackFilePath),
                        FOREIGN KEY (PlaylistId) REFERENCES Playlists(Id) ON DELETE CASCADE
                    );");

                // Indexes for fast playlist track lookup and ordering
                await connection.ExecuteAsync(@"
                    CREATE INDEX IF NOT EXISTS idx_playlisttracks_playlist
                        ON PlaylistTracks(PlaylistId, Position);
                    CREATE INDEX IF NOT EXISTS idx_playlisttracks_track
                        ON PlaylistTracks(TrackFilePath);
                    CREATE INDEX IF NOT EXISTS idx_playlists_name
                        ON Playlists(Name COLLATE NOCASE);
                ");

                // ── Phase 2: First-class Artists & Albums tables ──────────────
                // These persist artist/album metadata independently from tracks,
                // enabling "follow artist" / "save album" features and stable
                // navigation even when tracks aren't in the local library.
                // Inspired by Echo Music's first-class entity model.

                await connection.ExecuteAsync(@"
                    CREATE TABLE IF NOT EXISTS Artists (
                        Id               TEXT PRIMARY KEY,
                        Name             TEXT NOT NULL,
                        CoverPath        TEXT,
                        SourceType       TEXT,
                        YouTubeChannelId TEXT,
                        Description      TEXT,
                        SubscriberCount  INTEGER DEFAULT 0,
                        IsFollowed       INTEGER DEFAULT 0,
                        LastBrowsedAt    TEXT
                    );");

                // Non-destructive migrations for Artists table
                var artistCols = (await connection.QueryAsync<string>(
                    "SELECT name FROM pragma_table_info('Artists')"))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                if (!artistCols.Contains("Description"))
                    await connection.ExecuteAsync("ALTER TABLE Artists ADD COLUMN Description TEXT;");
                if (!artistCols.Contains("SubscriberCount"))
                    await connection.ExecuteAsync("ALTER TABLE Artists ADD COLUMN SubscriberCount INTEGER DEFAULT 0;");
                if (!artistCols.Contains("IsFollowed"))
                    await connection.ExecuteAsync("ALTER TABLE Artists ADD COLUMN IsFollowed INTEGER DEFAULT 0;");
                if (!artistCols.Contains("LastBrowsedAt"))
                    await connection.ExecuteAsync("ALTER TABLE Artists ADD COLUMN LastBrowsedAt TEXT;");

                await connection.ExecuteAsync(@"
                    CREATE TABLE IF NOT EXISTS Albums (
                        Id            TEXT PRIMARY KEY,
                        Name          TEXT NOT NULL,
                        ArtistId      TEXT,
                        ArtistName    TEXT,
                        Year          INTEGER,
                        CoverPath     TEXT,
                        SourceType    TEXT,
                        YouTubeAlbumId TEXT,
                        Description   TEXT,
                        Genre         TEXT,
                        IsSaved       INTEGER DEFAULT 0,
                        TrackCount    INTEGER DEFAULT 0,
                        LastBrowsedAt TEXT,
                        FOREIGN KEY (ArtistId) REFERENCES Artists(Id)
                    );");

                // Non-destructive migrations for Albums table
                var albumCols = (await connection.QueryAsync<string>(
                    "SELECT name FROM pragma_table_info('Albums')"))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                if (!albumCols.Contains("Description"))
                    await connection.ExecuteAsync("ALTER TABLE Albums ADD COLUMN Description TEXT;");
                if (!albumCols.Contains("Genre"))
                    await connection.ExecuteAsync("ALTER TABLE Albums ADD COLUMN Genre TEXT;");
                if (!albumCols.Contains("IsSaved"))
                    await connection.ExecuteAsync("ALTER TABLE Albums ADD COLUMN IsSaved INTEGER DEFAULT 0;");
                if (!albumCols.Contains("TrackCount"))
                    await connection.ExecuteAsync("ALTER TABLE Albums ADD COLUMN TrackCount INTEGER DEFAULT 0;");
                if (!albumCols.Contains("LastBrowsedAt"))
                    await connection.ExecuteAsync("ALTER TABLE Albums ADD COLUMN LastBrowsedAt TEXT;");

                // Indexes for Artists & Albums tables
                await connection.ExecuteAsync(@"
                    CREATE INDEX IF NOT EXISTS idx_artists_name       ON Artists(Name COLLATE NOCASE);
                    CREATE INDEX IF NOT EXISTS idx_artists_ytchannel  ON Artists(YouTubeChannelId) WHERE YouTubeChannelId IS NOT NULL;
                    CREATE INDEX IF NOT EXISTS idx_artists_followed   ON Artists(IsFollowed) WHERE IsFollowed = 1;
                    CREATE INDEX IF NOT EXISTS idx_albums_name        ON Albums(Name COLLATE NOCASE);
                    CREATE INDEX IF NOT EXISTS idx_albums_artistid    ON Albums(ArtistId);
                    CREATE INDEX IF NOT EXISTS idx_albums_ytalbumid   ON Albums(YouTubeAlbumId) WHERE YouTubeAlbumId IS NOT NULL;
                    CREATE INDEX IF NOT EXISTS idx_albums_saved       ON Albums(IsSaved) WHERE IsSaved = 1;
                ");

                // Drop old indexes first to recreate with optimized composite indexes
                await connection.ExecuteAsync(@"
                    DROP INDEX IF EXISTS idx_tracks_artist;
                    DROP INDEX IF EXISTS idx_tracks_album;
                    DROP INDEX IF EXISTS idx_tracks_title;
                    DROP INDEX IF EXISTS idx_tracks_isfavourite;
                    DROP INDEX IF EXISTS idx_tracks_lastplayed;
                    DROP INDEX IF EXISTS idx_tracks_filepath;
                    DROP INDEX IF EXISTS idx_tracks_sourcetype;
                ");

                // Full SQLite indexing for instant search and sort performance
                // Composite indexes for common query patterns (MusicBee/foobar2000 style)
                await connection.ExecuteAsync(@"
                    -- Individual column indexes for single-field lookups
                    CREATE INDEX IF NOT EXISTS idx_tracks_artist      ON Tracks(Artist COLLATE NOCASE);
                    CREATE INDEX IF NOT EXISTS idx_tracks_album       ON Tracks(Album COLLATE NOCASE);
                    CREATE INDEX IF NOT EXISTS idx_tracks_title       ON Tracks(Title COLLATE NOCASE);
                    CREATE INDEX IF NOT EXISTS idx_tracks_genre       ON Tracks(Genre COLLATE NOCASE);
                    CREATE INDEX IF NOT EXISTS idx_tracks_year        ON Tracks(Year);
                    
                    -- User interaction indexes
                    CREATE INDEX IF NOT EXISTS idx_tracks_isfavourite  ON Tracks(IsFavourite) WHERE IsFavourite = 1;
                    CREATE INDEX IF NOT EXISTS idx_tracks_lastplayed   ON Tracks(LastPlayed DESC) WHERE LastPlayed IS NOT NULL;
                    CREATE INDEX IF NOT EXISTS idx_tracks_playcount    ON Tracks(PlayCount DESC);
                    
                    -- Composite indexes for common sort patterns (critical for performance)
                    CREATE INDEX IF NOT EXISTS idx_tracks_artist_album ON Tracks(Artist COLLATE NOCASE, Album COLLATE NOCASE);
                    CREATE INDEX IF NOT EXISTS idx_tracks_artist_title ON Tracks(Artist COLLATE NOCASE, Title COLLATE NOCASE);
                    CREATE INDEX IF NOT EXISTS idx_tracks_album_track  ON Tracks(Album COLLATE NOCASE, TrackNumber);
                    
                    -- Full-text search index for instant search across multiple fields
                    CREATE INDEX IF NOT EXISTS idx_tracks_search ON Tracks(Artist COLLATE NOCASE, Album COLLATE NOCASE, Title COLLATE NOCASE);
                    
                    -- Lookup indexes for fast file and source filtering
                    CREATE INDEX IF NOT EXISTS idx_tracks_filepath  ON Tracks(FilePath);
                    CREATE INDEX IF NOT EXISTS idx_tracks_sourcetype ON Tracks(SourceType);
                ");
            }
        }

        // ── Deep scan (background indexer) ────────────────────────────────
        /// <summary>
        /// Recursively indexes rootPath the same way MusicBee and foobar2000 do:
        ///   1. Discover all audio files.
        ///   2. Load the existing (FilePath → LastModified) map from the DB.
        ///   3. Skip files whose timestamp has not changed (zero redundant TagLib reads).
        ///   4. INSERT new files; UPDATE metadata-only columns on changed files
        ///      (user data: IsFavourite, PlayCount, LastPlayed are never overwritten).
        ///   5. Prune DB rows for files deleted from disk.
        ///   6. Commit in batches of 50 for throughput.
        ///   7. Report rich ScanProgressInfo for the UI toast.
        /// </summary>
        public async Task ScanLibraryDeepAsync(
            string rootPath,
            IProgress<ScanProgressInfo> progress,
            CancellationToken cancellationToken = default)
        {
            if (!Directory.Exists(rootPath)) return;

            // 1. Discover files ────────────────────────────────────────────
            var allFiles = await Task.Run(() =>
                Directory.EnumerateFiles(rootPath, "*.*", SearchOption.AllDirectories)
                    .Where(f => ValidExtensions.Contains(Path.GetExtension(f)))
                    .ToList(), cancellationToken);

            int total     = allFiles.Count;
            int processed = 0;
            int added     = 0;
            int updated   = 0;
            int removed   = 0;
            const int batchSize = 50;

            // 2. Load existing fingerprint map and populate metadata cache
            Dictionary<string, string> existingMap;
            using (var conn = new SqliteConnection(_dbConnectionString))
            {
                await conn.OpenAsync(cancellationToken);
                var rows = await conn.QueryAsync<FileStamp>(
                    "SELECT FilePath, LastModified FROM Tracks");
                existingMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var r in rows)
                {
                    existingMap[r.FilePath] = r.LastModified ?? string.Empty;
                    
                    // Pre-populate cache with existing tracks for quick access
                    if (_metadataCache.Count < _maxCacheSize)
                    {
                        _metadataCache[r.FilePath] = new MetadataCacheEntry
                        {
                            FilePath = r.FilePath,
                            LastModified = r.LastModified ?? string.Empty,
                            CachedAt = DateTime.UtcNow
                        };
                    }
                }
            }

            var fileSet = new HashSet<string>(allFiles, StringComparer.OrdinalIgnoreCase);

            // SQL for new tracks (INSERT OR IGNORE preserves existing user data)
            const string insertSql = @"
                INSERT OR IGNORE INTO Tracks (
                    FilePath, Title, Artist, Album, Year, Genre, Duration,
                    TrackNumber, Bitrate, FileSize, CoverPath, Lyrics,
                    SourceUri, SourceType, LastModified,
                    IsFavourite, PlayCount, IsDownloaded,
                    AlbumArtist, DateAdded
                ) VALUES (
                    @FilePath, @Title, @Artist, @Album, @Year, @Genre, @Duration,
                    @TrackNumber, @Bitrate, @FileSize, @CoverPath, @Lyrics,
                    @SourceUri, @SourceType, @LastModified,
                    0, 0, 0,
                    @AlbumArtist, @DateAdded
                );";

            // SQL for changed tracks — updates only metadata, NEVER touches user data
            const string updateSql = @"
                UPDATE Tracks SET
                    Title       = @Title,
                    Artist      = @Artist,
                    Album       = @Album,
                    Year        = @Year,
                    Genre       = @Genre,
                    Duration    = @Duration,
                    TrackNumber = @TrackNumber,
                    Bitrate     = @Bitrate,
                    FileSize    = @FileSize,
                    CoverPath   = @CoverPath,
                    Lyrics      = @Lyrics,
                    LastModified= @LastModified,
                    AlbumArtist = @AlbumArtist
                WHERE FilePath = @FilePath;";

            // 3. Scan + upsert in batches ──────────────────────────────────
            using (var connection = new SqliteConnection(_dbConnectionString))
            {
                await connection.OpenAsync(cancellationToken);

                for (int batchStart = 0; batchStart < total; batchStart += batchSize)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    int batchEnd = Math.Min(batchStart + batchSize, total);

                    using (var tx = connection.BeginTransaction())
                    {
                        for (int i = batchStart; i < batchEnd; i++)
                        {
                            var file = allFiles[i];
                            cancellationToken.ThrowIfCancellationRequested();

                            bool cached = _metadataCache.TryGetValue(file, out var cacheEntry);
                            bool exists = existingMap.TryGetValue(file, out var dbStamp);
                            
                            bool changed = !exists;
                            string diskStamp = null;
                            long fileSize = 0;

                            if (exists)
                            {
                                try
                                {
                                    diskStamp = File.GetLastWriteTimeUtc(file).ToString("o");
                                    fileSize = new FileInfo(file).Length;
                                }
                                catch
                                {
                                    processed++;
                                    continue;
                                }
                                changed = dbStamp != diskStamp;
                            }
                            
                            if (changed)
                            {
                                try
                                {
                                    if (diskStamp == null)
                                    {
                                        diskStamp = File.GetLastWriteTimeUtc(file).ToString("o");
                                        fileSize = new FileInfo(file).Length;
                                    }
                                    var track = await Task.Run(
                                        () => ExtractMetadataWithCache(file, diskStamp, fileSize), cancellationToken);

                                    if (exists)
                                    {
                                        await connection.ExecuteAsync(updateSql, track, transaction: tx);
                                        updated++;
                                    }
                                    else
                                    {
                                        await connection.ExecuteAsync(insertSql, track, transaction: tx);
                                        added++;
                                    }
                                    
                                    _metadataCache[file] = new MetadataCacheEntry
                                    {
                                        FilePath = file,
                                        LastModified = diskStamp,
                                        FileSize = fileSize,
                                        Metadata = track,
                                        CachedAt = DateTime.UtcNow
                                    };
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine(
                                        $"[LibraryScanner] Failed to index {file}: {ex.Message}");
                                }
                            }

                            processed++;
                            progress?.Report(new ScanProgressInfo
                            {
                                Percent        = total > 0 ? (processed * 100) / total : 0,
                                FilesProcessed = processed,
                                TotalFiles     = total,
                                NewTracksAdded = added,
                                TracksUpdated  = updated,
                                CurrentFileName = Path.GetFileName(file)
                            });
                        }

                        tx.Commit();
                    }

                    // Evict cache if over capacity (once per batch, not per file)
                    if (_metadataCache.Count >= _maxCacheSize)
                    {
                        lock (_cacheLock)
                        {
                            int toRemove = _metadataCache.Count / 10;
                            if (toRemove < 1) toRemove = 1;

                            var oldest = _metadataCache.Values
                                .OrderBy(e => e.CachedAt)
                                .Take(toRemove)
                                .Select(e => e.FilePath)
                                .ToList();

                            foreach (var key in oldest)
                                _metadataCache.TryRemove(key, out _);
                        }
                    }
                }

                // 4. Prune deleted files ───────────────────────────────────
                var deletedPaths = new List<string>();
                foreach (var key in existingMap.Keys)
                {
                    if (!fileSet.Contains(key))
                        deletedPaths.Add(key);
                }

                if (deletedPaths.Count > 0)
                {
                    using (var tx = connection.BeginTransaction())
                    {
                        foreach (var dead in deletedPaths)
                        {
                            await connection.ExecuteAsync("DELETE FROM Tracks WHERE FilePath = @FilePath",
                                new { FilePath = dead }, transaction: tx);
                            removed++;
                        }
                        tx.Commit();
                    }
                }
            }

            // 4b. Phase 4: Sync Artists/Albums from Tracks after scan completes
            try
            {
                await SyncArtistsAlbumsFromTracksAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[LibraryScanner] SyncArtistsAlbumsFromTracks failed: {ex.Message}");
            }

            // 5. Completion report ─────────────────────────────────────────
            progress?.Report(new ScanProgressInfo
            {
                Percent         = 100,
                FilesProcessed  = total,
                TotalFiles      = total,
                NewTracksAdded  = added,
                TracksUpdated   = updated,
                TracksRemoved   = removed,
                CurrentFileName = string.Empty,
                IsComplete      = true
            });
        }

        // ── User-data mutations (favourites / play history) ────────────────
        public async Task ToggleFavouriteAsync(string filePath, CancellationToken cancellationToken = default)
        {
            using (var connection = new SqliteConnection(_dbConnectionString))
            {
                await connection.OpenAsync(cancellationToken);
                await connection.ExecuteAsync(
                    "UPDATE Tracks SET IsFavourite = CASE WHEN IsFavourite = 1 THEN 0 ELSE 1 END WHERE FilePath = @FilePath",
                    new { FilePath = filePath });
            }
        }

        public async Task RecordPlayAsync(string filePath, CancellationToken cancellationToken = default)
        {
            using (var connection = new SqliteConnection(_dbConnectionString))
            {
                await connection.OpenAsync(cancellationToken);
                await connection.ExecuteAsync(
                    "UPDATE Tracks SET PlayCount = PlayCount + 1, LastPlayed = @LastPlayed WHERE FilePath = @FilePath",
                    new { FilePath = filePath, LastPlayed = DateTime.UtcNow.ToString("o") });
            }
        }

        public async Task<string> CreatePlaylistAsync(string name)
        {
            var id = Guid.NewGuid().ToString();
            using (var connection = new SqliteConnection(_dbConnectionString))
            {
                await connection.OpenAsync();
                await connection.ExecuteAsync(
                    "INSERT INTO Playlists (Id, Name, CreatedAt) VALUES (@Id, @Name, @CreatedAt)",
                    new { Id = id, Name = name, CreatedAt = DateTime.UtcNow.ToString("o") });
            }
            return id;
        }

        // ── Library queries (called by LibraryControl) ─────────────────────
        public async Task<List<MusicFile>> GetFavouriteTracksAsync(CancellationToken cancellationToken = default)
        {
            using (var connection = new SqliteConnection(_dbConnectionString))
            {
                await connection.OpenAsync(cancellationToken);
                var tracks = await connection.QueryAsync<MusicFile>(
                    "SELECT * FROM Tracks WHERE IsFavourite = 1 ORDER BY Artist, Album, TrackNumber");
                return tracks.ToList();
            }
        }

        public async Task<List<MusicFile>> GetHistoryTracksAsync(int limit = 100, CancellationToken cancellationToken = default)
        {
            using (var connection = new SqliteConnection(_dbConnectionString))
            {
                await connection.OpenAsync(cancellationToken);
                var tracks = await connection.QueryAsync<MusicFile>(
                    "SELECT * FROM Tracks WHERE LastPlayed IS NOT NULL AND LastPlayed != '' ORDER BY LastPlayed DESC LIMIT @Limit",
                    new { Limit = limit });
                return tracks.ToList();
            }
        }

        public async Task<List<MusicFile>> GetAllTracksAsync(CancellationToken cancellationToken = default)
        {
            using (var connection = new SqliteConnection(_dbConnectionString))
            {
                await connection.OpenAsync(cancellationToken);
                var tracks = await connection.QueryAsync<MusicFile>(
                    "SELECT * FROM Tracks ORDER BY Artist, Album, TrackNumber");
                return tracks.ToList();
            }
        }

        public async Task<List<MusicFile>> SearchAsync(string query, CancellationToken cancellationToken = default)
        {
            using var connection = new SqliteConnection(_dbConnectionString);
            await connection.OpenAsync(cancellationToken);
            var searchPattern = $"%{query}%";
            var tracks = await connection.QueryAsync<MusicFile>(
                @"SELECT * FROM Tracks
                  WHERE Title LIKE @Pattern OR Artist LIKE @Pattern OR Album LIKE @Pattern
                  ORDER BY Artist, Album, TrackNumber",
                new { Pattern = searchPattern });
            return tracks.ToList();
        }

        // ── Artwork helpers ────────────────────────────────────────────────
        public static string GetArtworkCachePath(string filePath)
        {
            var cacheDir = Path.Combine(Path.GetTempPath(), "MusicefyArtworkCache");
            Directory.CreateDirectory(cacheDir);
            using (var md5 = MD5.Create())
            {
                byte[] hashBytes = md5.ComputeHash(Encoding.UTF8.GetBytes(filePath.ToLowerInvariant()));
                string hashStr   = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
                return Path.Combine(cacheDir, $"cover_{hashStr}.jpg");
            }
        }

        string ILibraryService.GetArtworkCachePath(string filePath) => GetArtworkCachePath(filePath);

        private string FindFolderArtwork(string filePath)
        {
            string dir = Path.GetDirectoryName(filePath);
            if (dir == null) return null;

            foreach (var name in FolderArtNames)
            {
                string candidate = Path.Combine(dir, name);
                if (File.Exists(candidate)) return candidate;
            }

            return null;
        }

        // ── Metadata extraction with caching ───────────────────────────────
        /// <summary>
        /// Extracts metadata from file, using cache if available.
        /// This prevents redundant TagLib operations for unchanged files.
        /// </summary>
        private MusicFile ExtractMetadataWithCache(string file, string lastModified, long fileSize)
        {
            // Check if we have valid cached metadata
            if (_metadataCache.TryGetValue(file, out var cacheEntry) &&
                cacheEntry.LastModified == lastModified &&
                cacheEntry.FileSize == fileSize &&
                cacheEntry.Metadata != null)
            {
                return cacheEntry.Metadata;
            }

            // No valid cache, extract metadata normally
            var result = ExtractMetadata(file, lastModified);

            // Store in cache
            if (_metadataCache.Count < _maxCacheSize)
            {
                _metadataCache[file] = new MetadataCacheEntry
                {
                    FilePath = file,
                    LastModified = lastModified,
                    FileSize = fileSize,
                    Metadata = result,
                    CachedAt = DateTime.UtcNow
                };
            }

            return result;
        }

        // ── Metadata extraction ────────────────────────────────────────────
        private MusicFile ExtractMetadata(string file, string lastModified)
        {
            var    fileInfo   = new FileInfo(file);
            string title      = Path.GetFileNameWithoutExtension(file);
            string artist     = "Unknown Artist";
            string albumArtist = null;
            string album      = "Unknown Album";
            string genre      = "Unknown";
            string lyrics     = string.Empty;
            string coverPath  = string.Empty;
            int    year       = 0;
            int    trackNumber = 0;
            int    bitrate    = 0;
            TimeSpan duration = TimeSpan.Zero;

            try
            {
                using (var tag = TagLib.File.Create(file))
                {
                    duration = tag.Properties.Duration;
                    bitrate  = tag.Properties.AudioBitrate;

                    if (tag.Tag != null)
                    {
                        if (!string.IsNullOrWhiteSpace(tag.Tag.Title))
                            title = tag.Tag.Title.Trim();

                        // AlbumArtist → primary field for album grouping
                        if (tag.Tag.AlbumArtists != null && tag.Tag.AlbumArtists.Length > 0)
                            albumArtist = string.Join(", ", Array.FindAll(tag.Tag.AlbumArtists,
                                a => !string.IsNullOrWhiteSpace(a))).Trim();

                        // Performer → Artist display field; fallback to AlbumArtist
                        if (tag.Tag.Performers != null && tag.Tag.Performers.Length > 0)
                            artist = string.Join(", ", Array.FindAll(tag.Tag.Performers,
                                p => !string.IsNullOrWhiteSpace(p))).Trim();
                        else if (albumArtist != null)
                            artist = albumArtist;

                        if (string.IsNullOrWhiteSpace(artist)) artist = "Unknown Artist";

                        if (!string.IsNullOrWhiteSpace(tag.Tag.Album))
                            album = tag.Tag.Album.Trim();

                        if (!string.IsNullOrWhiteSpace(tag.Tag.FirstGenre))
                            genre = tag.Tag.FirstGenre.Trim();

                        year        = (int)tag.Tag.Year;
                        trackNumber = (int)tag.Tag.Track;

                        if (!string.IsNullOrWhiteSpace(tag.Tag.Lyrics))
                            lyrics = tag.Tag.Lyrics.Trim();

                        // Embedded art — FrontCover first, then any picture
                        if (tag.Tag.Pictures != null && tag.Tag.Pictures.Length > 0)
                        {
                            TagLib.IPicture pic = null;
                            foreach (var p in tag.Tag.Pictures)
                            {
                                if (p.Type == TagLib.PictureType.FrontCover)
                                {
                                    pic = p;
                                    break;
                                }
                            }
                            if (pic == null) pic = tag.Tag.Pictures[0];

                            string targetPath = GetArtworkCachePath(file);
                            if (!File.Exists(targetPath))
                                File.WriteAllBytes(targetPath, pic.Data.Data);

                            coverPath = targetPath;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[LibraryScanner] TagLib failed for {file}: {ex.Message}");
            }

            // Folder-level artwork fallback
            if (string.IsNullOrEmpty(coverPath))
            {
                string folderArt = FindFolderArtwork(file);
                if (folderArt != null)
                    coverPath = folderArt;
            }

            return new MusicFile
            {
                FilePath    = file,
                Title       = title,
                Artist      = artist,
                AlbumArtist = albumArtist,
                Album       = album,
                Year        = year,
                Genre       = genre,
                Duration    = duration,
                TrackNumber = trackNumber,
                Bitrate     = bitrate,
                FileSize    = fileInfo.Length,
                CoverPath   = coverPath,
                Lyrics      = lyrics,
                SourceUri   = file,
                SourceType  = "FileItem",
                DateAdded   = DateTime.UtcNow
            };
        }

            /// <summary>
        /// Returns the immediate children of targetPath (sub-folders first,
        /// then audio files with full tags). If excludePaths is provided,
        /// files in that set are skipped (used for live folder browsing to
        /// avoid re-reading metadata for files already loaded from the DB).
        /// </summary>
        public List<MusicFile> ScanDirectory(string targetPath)
        {
            return ScanDirectory(targetPath, null);
        }

        public List<MusicFile> ScanDirectory(string targetPath, ISet<string> excludePaths)
        {
            var results = new List<MusicFile>();
            if (!Directory.Exists(targetPath)) return results;
    
            // Sub-folders first (skip hidden), sorted alphabetically
            string[] subDirs = Directory.GetDirectories(targetPath);
            Array.Sort(subDirs, StringComparer.OrdinalIgnoreCase);
    
            foreach (var dir in subDirs)
            {
                var info = new DirectoryInfo(dir);
                if ((info.Attributes & FileAttributes.Hidden) == FileAttributes.Hidden) continue;
    
                results.Add(new MusicFile
                {
                    Title      = info.Name,
                    Artist     = "Folder",
                    SourceType = "FolderItem",
                    FilePath   = dir,
                    SourceUri  = dir
                });
            }
    
            // Audio files in this folder only (no recursion)
            string[] files = Directory.GetFiles(targetPath);
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
    
            foreach (var file in files)
            {
                if (!ValidExtensions.Contains(Path.GetExtension(file))) continue;
    
                // Skip files already known (avoids redundant TagLib reads)
                if (excludePaths != null && excludePaths.Contains(file)) continue;
    
                try
                {
                    results.Add(ExtractMetadata(file, File.GetLastWriteTimeUtc(file).ToString("o")));
                }
                catch
                {
                    results.Add(new MusicFile
                    {
                        Title      = Path.GetFileNameWithoutExtension(file),
                        Artist     = "Unknown Artist",
                        SourceType = "FileItem",
                        FilePath   = file,
                        SourceUri  = file
                    });
                }
            }
    
            return results;
        }

        // ── Smart playlists / Quick Picks queries ─────────────────────────
        public async Task<List<MusicFile>> GetMostPlayedAsync(int days, int limit, CancellationToken cancellationToken = default)
        {
            using (var connection = new SqliteConnection(_dbConnectionString))
            {
                await connection.OpenAsync(cancellationToken);
                var cutoff = DateTime.UtcNow.AddDays(-days).ToString("o");
                var tracks = await connection.QueryAsync<MusicFile>(
                    @"SELECT * FROM Tracks 
              WHERE PlayCount > 0 AND LastPlayed IS NOT NULL AND LastPlayed != '' AND LastPlayed >= @Cutoff
              ORDER BY PlayCount DESC, LastPlayed DESC
              LIMIT @Limit",
                    new { Cutoff = cutoff, Limit = limit });
                return tracks.ToList();
            }
        }

        public async Task<List<MusicFile>> GetForgottenFavoritesAsync(int daysSincePlayed, int limit, CancellationToken cancellationToken = default)
        {
            using (var connection = new SqliteConnection(_dbConnectionString))
            {
                await connection.OpenAsync(cancellationToken);
                var cutoff = DateTime.UtcNow.AddDays(-daysSincePlayed).ToString("o");
                var tracks = await connection.QueryAsync<MusicFile>(
                    @"SELECT * FROM Tracks 
              WHERE IsFavourite = 1 
                AND (LastPlayed IS NULL OR LastPlayed = '' OR LastPlayed < @Cutoff)
              ORDER BY RANDOM()
              LIMIT @Limit",
                    new { Cutoff = cutoff, Limit = limit });
                return tracks.ToList();
            }
        }

        public async Task<List<MusicFile>> GetRecentlyPlayedAsync(int limit, CancellationToken cancellationToken = default)
        {
            return await GetHistoryTracksAsync(limit, cancellationToken);
        }

        public async Task<List<MusicFile>> GetRandomFavouriteTracksAsync(int limit, CancellationToken cancellationToken = default)
        {
            using (var connection = new SqliteConnection(_dbConnectionString))
            {
                await connection.OpenAsync(cancellationToken);
                var tracks = await connection.QueryAsync<MusicFile>(
                    @"SELECT * FROM Tracks 
              WHERE IsFavourite = 1
              ORDER BY RANDOM()
              LIMIT @Limit",
                    new { Limit = limit });
                return tracks.ToList();
            }
        }

        // ── Phase 2: Artist & Album persistence ────────────────────────────

        public async Task SaveArtistAsync(ArtistInfo artist, CancellationToken cancellationToken = default)
        {
            if (artist == null || string.IsNullOrEmpty(artist.Id)) return;

            using (var connection = new SqliteConnection(_dbConnectionString))
            {
                await connection.OpenAsync(cancellationToken);
                await connection.ExecuteAsync(@"
                    INSERT OR REPLACE INTO Artists (Id, Name, CoverPath, SourceType, YouTubeChannelId, Description, SubscriberCount, IsFollowed, LastBrowsedAt)
                    VALUES (@Id, @Name, @CoverPath, @SourceType, @YouTubeChannelId, @Description, @SubscriberCount, @IsFollowed, @LastBrowsedAt)",
                    new
                    {
                        artist.Id,
                        artist.Name,
                        artist.CoverPath,
                        artist.SourceType,
                        artist.YouTubeChannelId,
                        artist.Description,
                        SubscriberCount = artist.SubscriberCount ?? 0,
                        IsFollowed = artist.IsFollowed ? 1 : 0,
                        LastBrowsedAt = artist.LastBrowsedAt?.ToString("o")
                    });
            }
        }

        public async Task<ArtistInfo> GetArtistAsync(string artistId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(artistId)) return null;

            using (var connection = new SqliteConnection(_dbConnectionString))
            {
                await connection.OpenAsync(cancellationToken);
                var row = await connection.QueryFirstOrDefaultAsync<ArtistRow>(
                    "SELECT * FROM Artists WHERE Id = @Id", new { Id = artistId });

                return row != null ? RowToArtistInfo(row) : null;
            }
        }

        public async Task<ArtistInfo> GetArtistByYouTubeIdAsync(string channelId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(channelId)) return null;

            using (var connection = new SqliteConnection(_dbConnectionString))
            {
                await connection.OpenAsync(cancellationToken);
                var row = await connection.QueryFirstOrDefaultAsync<ArtistRow>(
                    "SELECT * FROM Artists WHERE YouTubeChannelId = @ChannelId", new { ChannelId = channelId });

                return row != null ? RowToArtistInfo(row) : null;
            }
        }

        public async Task<List<ArtistInfo>> GetFollowedArtistsAsync(CancellationToken cancellationToken = default)
        {
            using (var connection = new SqliteConnection(_dbConnectionString))
            {
                await connection.OpenAsync(cancellationToken);
                var rows = await connection.QueryAsync<ArtistRow>(
                    "SELECT * FROM Artists WHERE IsFollowed = 1 ORDER BY Name COLLATE NOCASE");
                return rows.Select(RowToArtistInfo).ToList();
            }
        }

        public async Task ToggleFollowArtistAsync(ArtistInfo artist, CancellationToken cancellationToken = default)
        {
            if (artist == null) return;

            // Ensure the artist exists in the table first
            await SaveArtistAsync(artist, cancellationToken);

            using (var connection = new SqliteConnection(_dbConnectionString))
            {
                await connection.OpenAsync(cancellationToken);
                await connection.ExecuteAsync(
                    "UPDATE Artists SET IsFollowed = CASE WHEN IsFollowed = 1 THEN 0 ELSE 1 END WHERE Id = @Id",
                    new { Id = artist.Id });
            }
        }

        public async Task SaveAlbumAsync(AlbumInfo album, CancellationToken cancellationToken = default)
        {
            if (album == null || string.IsNullOrEmpty(album.Id)) return;

            using (var connection = new SqliteConnection(_dbConnectionString))
            {
                await connection.OpenAsync(cancellationToken);
                await connection.ExecuteAsync(@"
                    INSERT OR REPLACE INTO Albums (Id, Name, ArtistId, ArtistName, Year, CoverPath, SourceType, YouTubeAlbumId, Description, Genre, IsSaved, TrackCount, LastBrowsedAt)
                    VALUES (@Id, @Name, @ArtistId, @ArtistName, @Year, @CoverPath, @SourceType, @YouTubeAlbumId, @Description, @Genre, @IsSaved, @TrackCount, @LastBrowsedAt)",
                    new
                    {
                        album.Id,
                        album.Name,
                        album.ArtistId,
                        ArtistName = album.Artist,
                        album.Year,
                        album.CoverPath,
                        album.SourceType,
                        album.YouTubeAlbumId,
                        album.Description,
                        album.Genre,
                        IsSaved = album.IsSaved ? 1 : 0,
                        TrackCount = album.TrackCount > 0 ? album.TrackCount : (album.Tracks?.Count ?? 0),
                        LastBrowsedAt = album.LastBrowsedAt?.ToString("o")
                    });
            }
        }

        public async Task<AlbumInfo> GetAlbumAsync(string albumId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(albumId)) return null;

            using (var connection = new SqliteConnection(_dbConnectionString))
            {
                await connection.OpenAsync(cancellationToken);
                var row = await connection.QueryFirstOrDefaultAsync<AlbumRow>(
                    "SELECT * FROM Albums WHERE Id = @Id", new { Id = albumId });

                return row != null ? RowToAlbumInfo(row) : null;
            }
        }

        public async Task<AlbumInfo> GetAlbumByYouTubeIdAsync(string youTubeAlbumId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(youTubeAlbumId)) return null;

            using (var connection = new SqliteConnection(_dbConnectionString))
            {
                await connection.OpenAsync(cancellationToken);
                var row = await connection.QueryFirstOrDefaultAsync<AlbumRow>(
                    "SELECT * FROM Albums WHERE YouTubeAlbumId = @YouTubeAlbumId", new { YouTubeAlbumId = youTubeAlbumId });

                return row != null ? RowToAlbumInfo(row) : null;
            }
        }

        public async Task<List<AlbumInfo>> GetSavedAlbumsAsync(CancellationToken cancellationToken = default)
        {
            using (var connection = new SqliteConnection(_dbConnectionString))
            {
                await connection.OpenAsync(cancellationToken);
                var rows = await connection.QueryAsync<AlbumRow>(
                    "SELECT * FROM Albums WHERE IsSaved = 1 ORDER BY ArtistName COLLATE NOCASE, Name COLLATE NOCASE");
                return rows.Select(RowToAlbumInfo).ToList();
            }
        }

        public async Task ToggleSaveAlbumAsync(AlbumInfo album, CancellationToken cancellationToken = default)
        {
            if (album == null) return;

            // Ensure the album exists in the table first
            await SaveAlbumAsync(album, cancellationToken);

            using (var connection = new SqliteConnection(_dbConnectionString))
            {
                await connection.OpenAsync(cancellationToken);
                await connection.ExecuteAsync(
                    "UPDATE Albums SET IsSaved = CASE WHEN IsSaved = 1 THEN 0 ELSE 1 END WHERE Id = @Id",
                    new { Id = album.Id });
            }
        }

        // ── Phase 4: Full library browsing ────────────────────────────────

        public async Task<List<ArtistInfo>> GetAllArtistsAsync(CancellationToken cancellationToken = default)
        {
            using (var connection = new SqliteConnection(_dbConnectionString))
            {
                await connection.OpenAsync(cancellationToken);
                var rows = await connection.QueryAsync<ArtistRow>(
                    "SELECT * FROM Artists ORDER BY Name COLLATE NOCASE");
                return rows.Select(RowToArtistInfo).ToList();
            }
        }

        public async Task<List<AlbumInfo>> GetAllAlbumsAsync(CancellationToken cancellationToken = default)
        {
            using (var connection = new SqliteConnection(_dbConnectionString))
            {
                await connection.OpenAsync(cancellationToken);
                var rows = await connection.QueryAsync<AlbumRow>(
                    "SELECT * FROM Albums ORDER BY ArtistName COLLATE NOCASE, Name COLLATE NOCASE");
                return rows.Select(RowToAlbumInfo).ToList();
            }
        }

        /// <summary>
        /// Synchronize the Artists and Albums tables from the Tracks table.
        /// Extracts distinct artists and albums from track metadata, then
        /// upserts them into the Artists/Albums tables. Preserves existing
        /// IsFollowed/IsSaved states. Enriches YouTube browse IDs from
        /// track metadata (ArtistBrowseId, AlbumBrowseId columns).
        /// Inspired by Echo Music's library-first entity model — every track
        /// implicitly defines its artist and album.
        /// </summary>
        public async Task SyncArtistsAlbumsFromTracksAsync(CancellationToken cancellationToken = default)
        {
            using (var connection = new SqliteConnection(_dbConnectionString))
            {
                await connection.OpenAsync(cancellationToken);

                using (var tx = connection.BeginTransaction())
                {
                    // ── Sync Artists from Tracks ──────────────────────────────
                    // Group tracks by Artist name; for each group, derive a stable
                    // ID (prefer YouTube channel ID if available, else local hash).
                    var artistRows = await connection.QueryAsync<ArtistTrackRow>(
                        @"SELECT
                            COALESCE(NULLIF(Artist, ''), 'Unknown Artist') AS ArtistName,
                            ArtistBrowseId,
                            SourceType,
                            MIN(CoverPath) AS CoverPath
                          FROM Tracks
                          GROUP BY COALESCE(NULLIF(Artist, ''), 'Unknown Artist')",
                        transaction: tx);

                    foreach (var row in artistRows)
                    {
                        string artistId = !string.IsNullOrEmpty(row.ArtistBrowseId)
                            ? row.ArtistBrowseId
                            : $"local_artist:{row.ArtistName}";

                        // Check if this artist already exists — preserve IsFollowed
                        var existingFollow = await connection.QueryFirstOrDefaultAsync<int?>(
                            "SELECT IsFollowed FROM Artists WHERE Id = @Id",
                            new { Id = artistId }, transaction: tx);

                        // Preserve existing YouTubeChannelId/Description/SubscriberCount if we already have them
                        var existingYtChannel = await connection.QueryFirstOrDefaultAsync<string>(
                            "SELECT YouTubeChannelId FROM Artists WHERE Id = @Id",
                            new { Id = artistId }, transaction: tx);

                        string ytChannelId = !string.IsNullOrEmpty(row.ArtistBrowseId)
                            ? row.ArtistBrowseId
                            : existingYtChannel;

                        int isFollowed = existingFollow ?? 0;

                        await connection.ExecuteAsync(@"
                            INSERT OR REPLACE INTO Artists (Id, Name, CoverPath, SourceType, YouTubeChannelId, Description, SubscriberCount, IsFollowed, LastBrowsedAt)
                            VALUES (@Id, @Name, @CoverPath, @SourceType, @YouTubeChannelId,
                                    (SELECT Description FROM Artists WHERE Id = @Id),
                                    (SELECT SubscriberCount FROM Artists WHERE Id = @Id),
                                    @IsFollowed,
                                    (SELECT LastBrowsedAt FROM Artists WHERE Id = @Id))",
                            new
                            {
                                Id = artistId,
                                Name = row.ArtistName,
                                row.CoverPath,
                                row.SourceType,
                                YouTubeChannelId = ytChannelId,
                                IsFollowed = isFollowed
                            }, transaction: tx);
                    }

                    // ── Sync Albums from Tracks ──────────────────────────────
                    // Group tracks by Album + Artist; derive stable ID.
                    var albumRows = await connection.QueryAsync<AlbumTrackRow>(
                        @"SELECT
                            COALESCE(NULLIF(Album, ''), 'Unknown Album') AS AlbumName,
                            COALESCE(NULLIF(Artist, ''), 'Unknown Artist') AS ArtistName,
                            AlbumBrowseId,
                            ArtistBrowseId,
                            SourceType,
                            MAX(Year) AS Year,
                            MIN(CoverPath) AS CoverPath,
                            COUNT(*) AS TrackCount
                          FROM Tracks
                          GROUP BY COALESCE(NULLIF(Album, ''), 'Unknown Album'),
                                   COALESCE(NULLIF(Artist, ''), 'Unknown Artist')",
                        transaction: tx);

                    foreach (var row in albumRows)
                    {
                        string albumId = !string.IsNullOrEmpty(row.AlbumBrowseId)
                            ? row.AlbumBrowseId
                            : $"local_album:{row.AlbumName}:{row.ArtistName}";

                        // Derive ArtistId from the artist's stable ID
                        string artistId = !string.IsNullOrEmpty(row.ArtistBrowseId)
                            ? row.ArtistBrowseId
                            : $"local_artist:{row.ArtistName}";

                        // Check if this album already exists — preserve IsSaved
                        var existingSaved = await connection.QueryFirstOrDefaultAsync<int?>(
                            "SELECT IsSaved FROM Albums WHERE Id = @Id",
                            new { Id = albumId }, transaction: tx);

                        // Preserve existing YouTubeAlbumId if we already have a better one
                        var existingYtAlbumId = await connection.QueryFirstOrDefaultAsync<string>(
                            "SELECT YouTubeAlbumId FROM Albums WHERE Id = @Id",
                            new { Id = albumId }, transaction: tx);

                        string ytAlbumId = !string.IsNullOrEmpty(row.AlbumBrowseId)
                            ? row.AlbumBrowseId
                            : existingYtAlbumId;

                        int isSaved = existingSaved ?? 0;

                        await connection.ExecuteAsync(@"
                            INSERT OR REPLACE INTO Albums (Id, Name, ArtistId, ArtistName, Year, CoverPath, SourceType, YouTubeAlbumId, Description, Genre, IsSaved, TrackCount, LastBrowsedAt)
                            VALUES (@Id, @Name, @ArtistId, @ArtistName, @Year, @CoverPath, @SourceType, @YouTubeAlbumId,
                                    (SELECT Description FROM Albums WHERE Id = @Id),
                                    (SELECT Genre FROM Albums WHERE Id = @Id),
                                    @IsSaved,
                                    @TrackCount,
                                    (SELECT LastBrowsedAt FROM Albums WHERE Id = @Id))",
                            new
                            {
                                Id = albumId,
                                Name = row.AlbumName,
                                ArtistId = artistId,
                                ArtistName = row.ArtistName,
                                row.Year,
                                row.CoverPath,
                                row.SourceType,
                                YouTubeAlbumId = ytAlbumId,
                                IsSaved = isSaved,
                                row.TrackCount
                            }, transaction: tx);
                    }

                    tx.Commit();
                }
            }
        }

        // ── Phase 2: Row → Model mappers ─────────────────────────────────

        private static ArtistInfo RowToArtistInfo(ArtistRow row)
        {
            return new ArtistInfo
            {
                Id = row.Id,
                Name = row.Name,
                CoverPath = row.CoverPath,
                SourceType = row.SourceType,
                YouTubeChannelId = row.YouTubeChannelId,
                Description = row.Description,
                SubscriberCount = row.SubscriberCount,
                IsFollowed = row.IsFollowed == 1,
                LastBrowsedAt = row.LastBrowsedAt != null
                    ? (DateTime.TryParse(row.LastBrowsedAt, out var dt) ? dt : (DateTime?)null)
                    : null
            };
        }

        private static AlbumInfo RowToAlbumInfo(AlbumRow row)
        {
            return new AlbumInfo
            {
                Id = row.Id,
                Name = row.Name,
                Artist = row.ArtistName,
                ArtistId = row.ArtistId,
                Year = row.Year,
                CoverPath = row.CoverPath,
                SourceType = row.SourceType,
                YouTubeAlbumId = row.YouTubeAlbumId,
                Description = row.Description,
                Genre = row.Genre,
                IsSaved = row.IsSaved == 1,
                TrackCount = row.TrackCount,
                LastBrowsedAt = row.LastBrowsedAt != null
                    ? (DateTime.TryParse(row.LastBrowsedAt, out var dt) ? dt : (DateTime?)null)
                    : null
            };
        }

        // ── Phase 2: Dapper row DTOs ─────────────────────────────────────

        private class ArtistRow
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public string CoverPath { get; set; }
            public string SourceType { get; set; }
            public string YouTubeChannelId { get; set; }
            public string Description { get; set; }
            public long SubscriberCount { get; set; }
            public int IsFollowed { get; set; }
            public string LastBrowsedAt { get; set; }
        }

        private class AlbumRow
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public string ArtistId { get; set; }
            public string ArtistName { get; set; }
            public int Year { get; set; }
            public string CoverPath { get; set; }
            public string SourceType { get; set; }
            public string YouTubeAlbumId { get; set; }
            public string Description { get; set; }
            public string Genre { get; set; }
            public int IsSaved { get; set; }
            public int TrackCount { get; set; }
            public string LastBrowsedAt { get; set; }
        }

        // ── Private helpers ────────────────────────────────────────────────
        private class FileStamp
        {
            public string FilePath     { get; set; }
            public string LastModified { get; set; }
        }

        // Phase 4: Row types for SyncArtistsAlbumsFromTracksAsync queries
        private class ArtistTrackRow
        {
            public string ArtistName { get; set; }
            public string ArtistBrowseId { get; set; }
            public string SourceType { get; set; }
            public string CoverPath { get; set; }
        }

        private class AlbumTrackRow
        {
            public string AlbumName { get; set; }
            public string ArtistName { get; set; }
            public string AlbumBrowseId { get; set; }
            public string ArtistBrowseId { get; set; }
            public string SourceType { get; set; }
            public int Year { get; set; }
            public string CoverPath { get; set; }
            public int TrackCount { get; set; }
        }

        // ── Phase 5: Playlist persistence ─────────────────────────────────

        public async Task<List<PlaylistInfo>> GetAllPlaylistsAsync(CancellationToken cancellationToken = default)
        {
            using (var connection = new SqliteConnection(_dbConnectionString))
            {
                await connection.OpenAsync(cancellationToken);
                var rows = await connection.QueryAsync<PlaylistRow>(
                    "SELECT * FROM Playlists ORDER BY Name COLLATE NOCASE");

                var result = new List<PlaylistInfo>();
                foreach (var row in rows)
                {
                    var playlist = RowToPlaylistInfo(row);
                    // Get track count from junction table
                    var trackCount = await connection.QueryFirstOrDefaultAsync<int>(
                        "SELECT COUNT(*) FROM PlaylistTracks WHERE PlaylistId = @PlaylistId",
                        new { PlaylistId = playlist.Id });
                    playlist.TrackCount = trackCount;

                    // Use the cover of the first track as a fallback
                    if (string.IsNullOrEmpty(playlist.CoverPath) && trackCount > 0)
                    {
                        var firstCover = await connection.QueryFirstOrDefaultAsync<string>(
                            @"SELECT t.CoverPath FROM PlaylistTracks pt
                              JOIN Tracks t ON pt.TrackFilePath = t.FilePath
                              WHERE pt.PlaylistId = @PlaylistId
                              ORDER BY pt.Position LIMIT 1",
                            new { PlaylistId = playlist.Id });
                        playlist.CoverPath = firstCover;
                    }

                    result.Add(playlist);
                }
                return result;
            }
        }

        public async Task<PlaylistInfo> GetPlaylistAsync(string playlistId, CancellationToken cancellationToken = default)
        {
            using (var connection = new SqliteConnection(_dbConnectionString))
            {
                await connection.OpenAsync(cancellationToken);
                var row = await connection.QueryFirstOrDefaultAsync<PlaylistRow>(
                    "SELECT * FROM Playlists WHERE Id = @Id",
                    new { Id = playlistId });
                if (row == null) return null;

                var playlist = RowToPlaylistInfo(row);
                var tracks = await GetPlaylistTracksAsync(playlistId, cancellationToken);
                playlist.Tracks = tracks;
                playlist.TrackCount = tracks.Count;
                playlist.TotalDuration = TimeSpan.FromTicks(tracks.Sum(t => t.Duration.Ticks));

                if (string.IsNullOrEmpty(playlist.CoverPath) && tracks.Count > 0)
                {
                    playlist.CoverPath = tracks[0].CoverPath;
                }

                return playlist;
            }
        }

        public async Task<List<MusicFile>> GetPlaylistTracksAsync(string playlistId, CancellationToken cancellationToken = default)
        {
            using (var connection = new SqliteConnection(_dbConnectionString))
            {
                await connection.OpenAsync(cancellationToken);
                var tracks = await connection.QueryAsync<MusicFile>(
                    @"SELECT t.* FROM PlaylistTracks pt
                      JOIN Tracks t ON pt.TrackFilePath = t.FilePath
                      WHERE pt.PlaylistId = @PlaylistId
                      ORDER BY pt.Position",
                    new { PlaylistId = playlistId });
                return tracks.ToList();
            }
        }

        public async Task AddTrackToPlaylistAsync(string playlistId, string trackFilePath, CancellationToken cancellationToken = default)
        {
            using (var connection = new SqliteConnection(_dbConnectionString))
            {
                await connection.OpenAsync(cancellationToken);

                // Check if the track is already in this playlist
                var exists = await connection.QueryFirstOrDefaultAsync<int>(
                    "SELECT COUNT(*) FROM PlaylistTracks WHERE PlaylistId = @PlaylistId AND TrackFilePath = @TrackFilePath",
                    new { PlaylistId = playlistId, TrackFilePath = trackFilePath });
                if (exists > 0) return; // No-op if already present

                // Get the next position
                var maxPos = await connection.QueryFirstOrDefaultAsync<int>(
                    "SELECT COALESCE(MAX(Position), -1) FROM PlaylistTracks WHERE PlaylistId = @PlaylistId",
                    new { PlaylistId = playlistId });

                await connection.ExecuteAsync(
                    "INSERT INTO PlaylistTracks (PlaylistId, TrackFilePath, Position) VALUES (@PlaylistId, @TrackFilePath, @Position)",
                    new { PlaylistId = playlistId, TrackFilePath = trackFilePath, Position = maxPos + 1 });

                // Update playlist's LastModifiedAt
                await connection.ExecuteAsync(
                    "UPDATE Playlists SET LastModifiedAt = @LastModifiedAt WHERE Id = @Id",
                    new { LastModifiedAt = DateTime.UtcNow.ToString("o"), Id = playlistId });
            }
        }

        public async Task AddTracksToPlaylistAsync(string playlistId, IEnumerable<string> trackFilePaths, CancellationToken cancellationToken = default)
        {
            using (var connection = new SqliteConnection(_dbConnectionString))
            {
                await connection.OpenAsync(cancellationToken);

                // Get existing tracks to skip duplicates
                var existing = (await connection.QueryAsync<string>(
                    "SELECT TrackFilePath FROM PlaylistTracks WHERE PlaylistId = @PlaylistId",
                    new { PlaylistId = playlistId }))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                // Get current max position
                var maxPos = await connection.QueryFirstOrDefaultAsync<int>(
                    "SELECT COALESCE(MAX(Position), -1) FROM PlaylistTracks WHERE PlaylistId = @PlaylistId",
                    new { PlaylistId = playlistId });

                using (var tx = connection.BeginTransaction())
                {
                    foreach (var filePath in trackFilePaths)
                    {
                        if (existing.Contains(filePath)) continue;
                        maxPos++;
                        await connection.ExecuteAsync(
                            "INSERT INTO PlaylistTracks (PlaylistId, TrackFilePath, Position) VALUES (@PlaylistId, @TrackFilePath, @Position)",
                            new { PlaylistId = playlistId, TrackFilePath = filePath, Position = maxPos },
                            transaction: tx);
                        existing.Add(filePath);
                    }
                    tx.Commit();
                }

                // Update playlist's LastModifiedAt
                await connection.ExecuteAsync(
                    "UPDATE Playlists SET LastModifiedAt = @LastModifiedAt WHERE Id = @Id",
                    new { LastModifiedAt = DateTime.UtcNow.ToString("o"), Id = playlistId });
            }
        }

        public async Task RemoveTrackFromPlaylistAsync(string playlistId, string trackFilePath, CancellationToken cancellationToken = default)
        {
            using (var connection = new SqliteConnection(_dbConnectionString))
            {
                await connection.OpenAsync(cancellationToken);

                // Get the position of the track being removed
                var removedPos = await connection.QueryFirstOrDefaultAsync<int?>(
                    "SELECT Position FROM PlaylistTracks WHERE PlaylistId = @PlaylistId AND TrackFilePath = @TrackFilePath",
                    new { PlaylistId = playlistId, TrackFilePath = trackFilePath });

                if (removedPos == null) return; // Track not in playlist

                using (var tx = connection.BeginTransaction())
                {
                    // Delete the track
                    await connection.ExecuteAsync(
                        "DELETE FROM PlaylistTracks WHERE PlaylistId = @PlaylistId AND TrackFilePath = @TrackFilePath",
                        new { PlaylistId = playlistId, TrackFilePath = trackFilePath }, transaction: tx);

                    // Re-order remaining tracks (shift positions down to fill the gap)
                    await connection.ExecuteAsync(
                        "UPDATE PlaylistTracks SET Position = Position - 1 WHERE PlaylistId = @PlaylistId AND Position > @RemovedPos",
                        new { PlaylistId = playlistId, RemovedPos = removedPos.Value }, transaction: tx);

                    tx.Commit();
                }

                // Update playlist's LastModifiedAt
                await connection.ExecuteAsync(
                    "UPDATE Playlists SET LastModifiedAt = @LastModifiedAt WHERE Id = @Id",
                    new { LastModifiedAt = DateTime.UtcNow.ToString("o"), Id = playlistId });
            }
        }

        public async Task MoveTrackInPlaylistAsync(string playlistId, int fromPosition, int toPosition, CancellationToken cancellationToken = default)
        {
            using (var connection = new SqliteConnection(_dbConnectionString))
            {
                await connection.OpenAsync(cancellationToken);

                using (var tx = connection.BeginTransaction())
                {
                    if (fromPosition < toPosition)
                    {
                        // Moving down: shift items between [from+1, to] up by 1
                        await connection.ExecuteAsync(
                            "UPDATE PlaylistTracks SET Position = Position - 1 WHERE PlaylistId = @PlaylistId AND Position > @From AND Position <= @To",
                            new { PlaylistId = playlistId, From = fromPosition, To = toPosition }, transaction: tx);
                    }
                    else
                    {
                        // Moving up: shift items between [to, from-1] down by 1
                        await connection.ExecuteAsync(
                            "UPDATE PlaylistTracks SET Position = Position + 1 WHERE PlaylistId = @PlaylistId AND Position >= @To AND Position < @From",
                            new { PlaylistId = playlistId, From = fromPosition, To = toPosition }, transaction: tx);
                    }

                    // Set the moved track to its new position
                    await connection.ExecuteAsync(
                        "UPDATE PlaylistTracks SET Position = @To WHERE PlaylistId = @PlaylistId AND Position = @From",
                        new { PlaylistId = playlistId, From = fromPosition, To = toPosition },
                        transaction: tx);

                    tx.Commit();
                }

                // Update playlist's LastModifiedAt
                await connection.ExecuteAsync(
                    "UPDATE Playlists SET LastModifiedAt = @LastModifiedAt WHERE Id = @Id",
                    new { LastModifiedAt = DateTime.UtcNow.ToString("o"), Id = playlistId });
            }
        }

        public async Task DeletePlaylistAsync(string playlistId, CancellationToken cancellationToken = default)
        {
            using (var connection = new SqliteConnection(_dbConnectionString))
            {
                await connection.OpenAsync(cancellationToken);
                using (var tx = connection.BeginTransaction())
                {
                    // Delete all track associations first
                    await connection.ExecuteAsync(
                        "DELETE FROM PlaylistTracks WHERE PlaylistId = @PlaylistId",
                        new { PlaylistId = playlistId }, transaction: tx);

                    // Delete the playlist itself
                    await connection.ExecuteAsync(
                        "DELETE FROM Playlists WHERE Id = @Id",
                        new { Id = playlistId }, transaction: tx);

                    tx.Commit();
                }
            }
        }

        public async Task RenamePlaylistAsync(string playlistId, string newName, CancellationToken cancellationToken = default)
        {
            using (var connection = new SqliteConnection(_dbConnectionString))
            {
                await connection.OpenAsync(cancellationToken);
                await connection.ExecuteAsync(
                    "UPDATE Playlists SET Name = @Name, LastModifiedAt = @LastModifiedAt WHERE Id = @Id",
                    new { Name = newName, LastModifiedAt = DateTime.UtcNow.ToString("o"), Id = playlistId });
            }
        }

        public async Task UpdatePlaylistAsync(PlaylistInfo playlist, CancellationToken cancellationToken = default)
        {
            if (playlist == null) throw new ArgumentNullException(nameof(playlist));
            using (var connection = new SqliteConnection(_dbConnectionString))
            {
                await connection.OpenAsync(cancellationToken);
                await connection.ExecuteAsync(
                    @"UPDATE Playlists SET
                        Name = @Name,
                        Description = @Description,
                        CoverPath = @CoverPath,
                        YouTubePlaylistId = @YouTubePlaylistId,
                        SourceType = @SourceType,
                        LastModifiedAt = @LastModifiedAt
                      WHERE Id = @Id",
                    new
                    {
                        playlist.Name,
                        playlist.Description,
                        playlist.CoverPath,
                        playlist.YouTubePlaylistId,
                        playlist.SourceType,
                        LastModifiedAt = DateTime.UtcNow.ToString("o"),
                        playlist.Id
                    });
            }
        }

        // ── Phase 5: Row mappers for Playlists ────────────────────────────

        private static PlaylistInfo RowToPlaylistInfo(PlaylistRow row)
        {
            return new PlaylistInfo
            {
                Id = row.Id,
                Name = row.Name,
                CreatedAt = DateTime.TryParse(row.CreatedAt, out var createdAt) ? createdAt : DateTime.MinValue,
                LastModifiedAt = DateTime.TryParse(row.LastModifiedAt, out var modified) ? (DateTime?)modified : null,
                Description = row.Description,
                CoverPath = row.CoverPath,
                YouTubePlaylistId = row.YouTubePlaylistId,
                SourceType = row.SourceType
            };
        }

        private class PlaylistRow
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public string CreatedAt { get; set; }
            public string LastModifiedAt { get; set; }
            public string Description { get; set; }
            public string CoverPath { get; set; }
            public string YouTubePlaylistId { get; set; }
            public string SourceType { get; set; }
        }
    }
}

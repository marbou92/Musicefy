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

                await connection.ExecuteAsync(@"
                    CREATE TABLE IF NOT EXISTS Playlists (
                        Id        TEXT PRIMARY KEY,
                        Name      TEXT NOT NULL,
                        CreatedAt TEXT NOT NULL
                    );");

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
                    IsFavourite, PlayCount, IsDownloaded
                ) VALUES (
                    @FilePath, @Title, @Artist, @Album, @Year, @Genre, @Duration,
                    @TrackNumber, @Bitrate, @FileSize, @CoverPath, @Lyrics,
                    @SourceUri, @SourceType, @LastModified,
                    0, 0, 0
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
                    LastModified= @LastModified
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

                        // Performer → AlbumArtist → filename
                        if (tag.Tag.Performers != null && tag.Tag.Performers.Length > 0)
                            artist = string.Join(", ", Array.FindAll(tag.Tag.Performers,
                                p => !string.IsNullOrWhiteSpace(p))).Trim();
                        else if (tag.Tag.AlbumArtists != null && tag.Tag.AlbumArtists.Length > 0)
                            artist = string.Join(", ", Array.FindAll(tag.Tag.AlbumArtists,
                                a => !string.IsNullOrWhiteSpace(a))).Trim();

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
                SourceType  = "FileItem"
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

        // ── Private helpers ────────────────────────────────────────────────
        private class FileStamp
        {
            public string FilePath     { get; set; }
            public string LastModified { get; set; }
        }
    }
}

-- 0001_initial_schema.sql
-- Musicefy initial schema: tracks, artists, albums, playlists, sources.
-- This migration is the foundation. Phase 2+ adds more tables.

PRAGMA foreign_keys = ON;

CREATE TABLE IF NOT EXISTS sources (
    id            TEXT PRIMARY KEY,
    name          TEXT NOT NULL,
    type          TEXT NOT NULL,
    url           TEXT,
    username      TEXT,
    password      TEXT,
    is_connected  INTEGER NOT NULL DEFAULT 0,
    client_version TEXT,
    configuration_json TEXT,
    created_at    INTEGER NOT NULL,
    updated_at    INTEGER NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_sources_type ON sources (type);

CREATE TABLE IF NOT EXISTS artists (
    id          TEXT PRIMARY KEY,
    name        TEXT NOT NULL,
    cover_path  TEXT,
    source_type TEXT,
    you_tube_channel_id TEXT,
    description TEXT,
    subscriber_count INTEGER,
    is_followed INTEGER NOT NULL DEFAULT 0,
    last_browsed_at INTEGER
);

CREATE INDEX IF NOT EXISTS idx_artists_name ON artists (name COLLATE NOCASE);
CREATE INDEX IF NOT EXISTS idx_artists_source_type ON artists (source_type);

CREATE TABLE IF NOT EXISTS albums (
    id          TEXT PRIMARY KEY,
    name        TEXT NOT NULL,
    artist      TEXT,
    artist_id   TEXT,
    year        INTEGER,
    cover_path  TEXT,
    source_type TEXT,
    you_tube_album_id TEXT,
    description TEXT,
    genre       TEXT,
    is_saved    INTEGER NOT NULL DEFAULT 0,
    last_browsed_at INTEGER,
    track_count INTEGER NOT NULL DEFAULT 0,
    FOREIGN KEY (artist_id) REFERENCES artists (id) ON DELETE SET NULL
);

CREATE INDEX IF NOT EXISTS idx_albums_name ON albums (name COLLATE NOCASE);
CREATE INDEX IF NOT EXISTS idx_albums_artist ON albums (artist COLLATE NOCASE);
CREATE INDEX IF NOT EXISTS idx_albums_source_type ON albums (source_type);

CREATE TABLE IF NOT EXISTS tracks (
    id              TEXT PRIMARY KEY,
    file_path       TEXT NOT NULL,
    title           TEXT NOT NULL,
    artist          TEXT,
    album           TEXT,
    year            INTEGER,
    genre           TEXT,
    duration_secs   INTEGER NOT NULL DEFAULT 0,
    track_number    INTEGER NOT NULL DEFAULT 0,
    bitrate         INTEGER NOT NULL DEFAULT 0,
    file_size       INTEGER NOT NULL DEFAULT 0,
    lyrics          TEXT,
    cover_path      TEXT,
    source_uri      TEXT,
    source_type     TEXT NOT NULL DEFAULT 'Local',
    source_id       TEXT,
    play_count      INTEGER NOT NULL DEFAULT 0,
    last_played     INTEGER,
    is_favourite    INTEGER NOT NULL DEFAULT 0,
    is_downloaded   INTEGER NOT NULL DEFAULT 0,
    date_added      INTEGER,
    album_artist    TEXT,
    album_id        TEXT,
    artist_id       TEXT,
    you_tube_video_id TEXT,
    you_tube_browse_id TEXT,
    you_tube_playlist_id TEXT,
    you_tube_music_video_type TEXT,
    loudness_db     REAL,
    audio_format    TEXT,
    FOREIGN KEY (album_id) REFERENCES albums (id) ON DELETE SET NULL,
    FOREIGN KEY (artist_id) REFERENCES artists (id) ON DELETE SET NULL,
    FOREIGN KEY (source_id) REFERENCES sources (id) ON DELETE SET NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS idx_tracks_file_path ON tracks (file_path);
CREATE INDEX IF NOT EXISTS idx_tracks_title ON tracks (title COLLATE NOCASE);
CREATE INDEX IF NOT EXISTS idx_tracks_artist ON tracks (artist COLLATE NOCASE);
CREATE INDEX IF NOT EXISTS idx_tracks_album ON tracks (album COLLATE NOCASE);
CREATE INDEX IF NOT EXISTS idx_tracks_source_type ON tracks (source_type);
CREATE INDEX IF NOT EXISTS idx_tracks_favourite ON tracks (is_favourite) WHERE is_favourite = 1;
CREATE INDEX IF NOT EXISTS idx_tracks_you_tube_video ON tracks (you_tube_video_id);

CREATE TABLE IF NOT EXISTS playlists (
    id          TEXT PRIMARY KEY,
    name        TEXT NOT NULL,
    created_at  INTEGER NOT NULL,
    last_modified_at INTEGER,
    description TEXT,
    cover_path  TEXT,
    you_tube_playlist_id TEXT,
    source_type TEXT,
    track_count INTEGER NOT NULL DEFAULT 0,
    total_duration_secs INTEGER NOT NULL DEFAULT 0
);

CREATE INDEX IF NOT EXISTS idx_playlists_name ON playlists (name COLLATE NOCASE);
CREATE INDEX IF NOT EXISTS idx_playlists_source_type ON playlists (source_type);

CREATE TABLE IF NOT EXISTS playlist_tracks (
    playlist_id  TEXT NOT NULL,
    position     INTEGER NOT NULL,
    track_id     TEXT NOT NULL,
    PRIMARY KEY (playlist_id, position),
    FOREIGN KEY (playlist_id) REFERENCES playlists (id) ON DELETE CASCADE,
    FOREIGN KEY (track_id) REFERENCES tracks (id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS idx_playlist_tracks_track ON playlist_tracks (track_id);

CREATE TABLE IF NOT EXISTS queue (
    position     INTEGER PRIMARY KEY,
    track_id     TEXT NOT NULL,
    FOREIGN KEY (track_id) REFERENCES tracks (id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS search_history (
    id              TEXT PRIMARY KEY,
    query           TEXT NOT NULL,
    source_type     TEXT NOT NULL,
    last_searched_at INTEGER NOT NULL,
    result_count    INTEGER NOT NULL DEFAULT 0,
    click_count     INTEGER NOT NULL DEFAULT 0,
    is_suggestion   INTEGER NOT NULL DEFAULT 0
);

CREATE INDEX IF NOT EXISTS idx_search_history_query ON search_history (query COLLATE NOCASE);
CREATE INDEX IF NOT EXISTS idx_search_history_source ON search_history (source_type);

CREATE TABLE IF NOT EXISTS app_state (
    key          TEXT PRIMARY KEY,
    value        TEXT NOT NULL,
    updated_at   INTEGER NOT NULL
);

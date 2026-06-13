#pragma once

#include "../models/MusicFile.h"
#include "../models/ArtistInfo.h"
#include "../models/AlbumInfo.h"
#include "../models/PlaylistInfo.h"
#include "../models/SearchHistory.h"

#include <QString>

#include <optional>
#include <vector>

namespace mf::core::database {

class Database;

class LibraryRepository {
public:
    explicit LibraryRepository(Database& db);

    // Tracks ────────────────────────────────────────────────────────────
    std::optional<mf::core::models::MusicFile> trackByPath(QString filePath) const;
    std::optional<mf::core::models::MusicFile> trackById(QString id) const;
    QList<mf::core::models::MusicFile> allTracks(int limit = -1, int offset = 0) const;
    QList<mf::core::models::MusicFile> tracksForAlbum(QString albumId) const;
    QList<mf::core::models::MusicFile> tracksForArtist(QString artistId) const;
    QList<mf::core::models::MusicFile> favouriteTracks() const;
    /// Top-N most recently played tracks (newest first). Only tracks
    /// that have actually been played (last_played > 0) are returned.
    QList<mf::core::models::MusicFile> recentlyPlayedTracks(int limit) const;
    void upsertTrack(const mf::core::models::MusicFile& track);
    void deleteTrack(QString filePath);
    /// Delete every track whose file_path starts with the given prefix.
    /// Used when a folder is removed from the library — the tracks
    /// are no longer reachable, so the rows must go. Returns the
    /// number of rows removed (0 if nothing matched).
    int  deleteTracksByPathPrefix(QString pathPrefix);
    void incrementPlayCount(QString filePath);
    void toggleFavourite(QString filePath);
    int  trackCount() const;

    // Artists ───────────────────────────────────────────────────────────
    QList<mf::core::models::ArtistInfo> allArtists() const;
    void upsertArtist(const mf::core::models::ArtistInfo& artist);

    // Albums ────────────────────────────────────────────────────────────
    QList<mf::core::models::AlbumInfo> allAlbums() const;
    QList<mf::core::models::AlbumInfo> albumsForArtist(QString artistId) const;
    void upsertAlbum(const mf::core::models::AlbumInfo& album);

    // Playlists ─────────────────────────────────────────────────────────
    QList<mf::core::models::PlaylistInfo> allPlaylists() const;
    void upsertPlaylist(const mf::core::models::PlaylistInfo& playlist);
    void deletePlaylist(QString id);
    void addTrackToPlaylist(QString playlistId, QString trackId, int position);
    void removeTrackFromPlaylist(QString playlistId, int position);

    // Search history ────────────────────────────────────────────────────
    void recordSearch(QString query, QString sourceType, int resultCount);
    QList<QString> recentSearchQueries(int limit) const;
    void bumpSearchClickCount(QString query, QString sourceType);
    void clearAllSearchHistory();
    void clearSearchHistoryForSource(QString sourceType);
    QList<mf::core::models::SearchHistory> recentSearchHistory(int limit) const;
    QList<mf::core::models::SearchHistory> searchHistorySuggestions(QString prefix, int limit) const;

    // App state (key/value) ─────────────────────────────────────────────
    void setAppState(QString key, QString value);
    std::optional<QString> appState(QString key) const;

    /// Expose the underlying Database (needed by LibraryScanner).
    Database& database() { return db_; }

private:
    Database& db_;
};

} // namespace mf::core::database

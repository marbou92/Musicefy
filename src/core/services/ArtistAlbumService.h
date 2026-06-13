// ArtistAlbumService.h
// Provides enriched artist and album lookups by composing the local
// LibraryRepository with remote BrowseService data. Handles:
//   - Merging local track counts with online metadata (YouTube, Subsonic)
//   - Caching last-browsed timestamps so the UI can show "recently viewed"
//   - Providing the full artist/album detail needed by ArtistView/AlbumView

#pragma once

#include "../interfaces/IBrowseService.h"
#include "../models/ArtistInfo.h"
#include "../models/AlbumInfo.h"

#include <QObject>
#include <QHash>

namespace mf::core::database { class LibraryRepository; }

namespace mf::core::services {

class ArtistAlbumService : public QObject {
    Q_OBJECT
public:
    explicit ArtistAlbumService(mf::core::database::LibraryRepository* repo,
                                mf::core::interfaces::IBrowseService* browse,
                                QObject* parent = nullptr);
    ~ArtistAlbumService() override = default;

    /// Get a full ArtistInfo by name. Merges local library data
    /// (track count, albums) with any cached online metadata.
    mf::core::models::ArtistInfo artistByName(const QString& name) const;

    /// Get a full AlbumInfo by name + artist. Merges local library
    /// data with any cached online metadata.
    mf::core::models::AlbumInfo albumByName(const QString& name,
                                            const QString& artist) const;

    /// Get all local artists, enriched with track counts.
    QList<mf::core::models::ArtistInfo> allArtists() const;

    /// Get all local albums, enriched with track counts.
    QList<mf::core::models::AlbumInfo> allAlbums() const;

    /// Get albums by a specific artist (by name).
    QList<mf::core::models::AlbumInfo> albumsForArtist(const QString& artistName) const;

    /// Get tracks for an artist.
    QList<mf::core::models::MusicFile> tracksForArtist(const QString& artistName) const;

    /// Record that the user browsed an artist (updates lastBrowsedAt).
    void recordArtistBrowsed(const QString& artistName);

    /// Record that the user browsed an album.
    void recordAlbumBrowsed(const QString& albumName, const QString& artist);

signals:
    void artistUpdated(const QString& name);
    void albumUpdated(const QString& name, const QString& artist);

private:
    mf::core::database::LibraryRepository* repo_ = nullptr;
    mf::core::interfaces::IBrowseService*  browse_ = nullptr;

    // In-memory cache of browsed artists/albums
    mutable QHash<QString, mf::core::models::ArtistInfo> artistCache_;
    mutable QHash<QString, mf::core::models::AlbumInfo>  albumCache_;
};

} // namespace mf::core::services

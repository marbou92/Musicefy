#pragma once

#include "../models/ArtistInfo.h"
#include "../models/AlbumInfo.h"
#include "../models/PlaylistInfo.h"
#include "../models/MusicFile.h"

#include <QObject>
#include <QString>

#include <functional>

namespace mf::core::interfaces {

class ILibraryService {
public:
    virtual ~ILibraryService() = default;

    using ScanProgressCallback = std::function<void(int current, int total, QString currentFile)>;
    using TrackAddedCallback = std::function<void(mf::core::models::MusicFile)>;
    using TrackRemovedCallback = std::function<void(QString filePath)>;
    using TrackUpdatedCallback = std::function<void(mf::core::models::MusicFile)>;

    virtual void initialize() = 0;
    virtual void scanLibrary() = 0;
    virtual void cancelScan() = 0;
    virtual bool isScanning() const = 0;

    virtual QList<mf::core::models::MusicFile>      allTracks() const = 0;
    virtual QList<mf::core::models::ArtistInfo>     allArtists() const = 0;
    virtual QList<mf::core::models::AlbumInfo>      allAlbums() const = 0;
    virtual QList<mf::core::models::PlaylistInfo>   allPlaylists() const = 0;

    virtual QList<mf::core::models::MusicFile> tracksForAlbum(QString albumId) const = 0;
    virtual QList<mf::core::models::AlbumInfo>  albumsForArtist(QString artistId) const = 0;
    virtual QList<mf::core::models::MusicFile> tracksForArtist(QString artistId) const = 0;

    virtual mf::core::models::MusicFile    trackByPath(QString filePath) const = 0;
    virtual void                           addTrack(mf::core::models::MusicFile track) = 0;
    virtual void                           updateTrack(mf::core::models::MusicFile track) = 0;
    virtual void                           removeTrack(QString filePath) = 0;
    virtual void                           incrementPlayCount(QString filePath) = 0;
    virtual void                           toggleFavourite(QString filePath) = 0;

    // Smart queries (power Home page carousels) ─────────────────────
    virtual QList<mf::core::models::MusicFile> favouriteTracks(int limit = -1) const = 0;
    virtual QList<mf::core::models::MusicFile> recentlyPlayedTracks(int limit = 10) const = 0;
    virtual QList<mf::core::models::MusicFile> mostPlayedTracks(int limit = 10) const = 0;
    virtual QList<mf::core::models::MusicFile> recentlyAddedTracks(int limit = 10) const = 0;
    virtual QList<mf::core::models::MusicFile> forgottenFavourites(int limit = 10) const = 0;
    virtual QList<mf::core::models::MusicFile> randomFavouriteTracks(int limit = 10) const = 0;

    virtual void setOnScanProgress(ScanProgressCallback cb) = 0;
    virtual void setOnTrackAdded(TrackAddedCallback cb) = 0;
    virtual void setOnTrackRemoved(TrackRemovedCallback cb) = 0;
    virtual void setOnTrackUpdated(TrackUpdatedCallback cb) = 0;
};

} // namespace mf::core::interfaces

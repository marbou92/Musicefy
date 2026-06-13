// ArtistAlbumService.cpp
// Implementation of artist/album enrichment service.

#include "ArtistAlbumService.h"

#include "../database/LibraryRepository.h"
#include "../interfaces/IBrowseService.h"

#include <algorithm>

namespace mf::core::services {

using mf::core::database::LibraryRepository;
using mf::core::interfaces::IBrowseService;
using mf::core::models::AlbumInfo;
using mf::core::models::ArtistInfo;
using mf::core::models::MusicFile;

ArtistAlbumService::ArtistAlbumService(LibraryRepository* repo,
                                       IBrowseService* browse,
                                       QObject* parent)
    : QObject(parent)
    , repo_(repo)
    , browse_(browse)
{
}

ArtistInfo ArtistAlbumService::artistByName(const QString& name) const
{
    if (!repo_) return ArtistInfo{};

    // Check cache first.
    if (artistCache_.contains(name)) {
        return artistCache_.value(name);
    }

    // Build from local library data.
    ArtistInfo result;
    result.setName(name);

    const auto allTracks = repo_->allTracks();
    QList<MusicFile> artistTracks;
    for (const auto& t : allTracks) {
        if (t.artist().compare(name, Qt::CaseInsensitive) == 0) {
            artistTracks.append(t);
        }
    }
    result.setTracks(artistTracks);

    // Collect unique albums for this artist.
    QHash<QString, AlbumInfo> albumMap;
    for (const auto& t : artistTracks) {
        if (t.album().isEmpty()) continue;
        if (!albumMap.contains(t.album())) {
            AlbumInfo a;
            a.setName(t.album());
            a.setArtist(name);
            a.setCoverPath(t.coverPath());
            a.setCoverUrl(t.coverUrl());
            a.setYear(t.year());
            albumMap.insert(t.album(), a);
        }
    }
    result.setAlbums(albumMap.values());

    // Cache and return.
    artistCache_.insert(name, result);
    return result;
}

AlbumInfo ArtistAlbumService::albumByName(const QString& name,
                                          const QString& artist) const
{
    if (!repo_) return AlbumInfo{};

    const QString cacheKey = artist.toLower() + QLatin1Char('/') + name.toLower();
    if (albumCache_.contains(cacheKey)) {
        return albumCache_.value(cacheKey);
    }

    AlbumInfo result;
    result.setName(name);
    result.setArtist(artist);

    const auto allTracks = repo_->allTracks();
    QList<MusicFile> albumTracks;
    for (const auto& t : allTracks) {
        if (t.album().compare(name, Qt::CaseInsensitive) == 0 &&
            (artist.isEmpty() ||
             t.artist().compare(artist, Qt::CaseInsensitive) == 0)) {
            albumTracks.append(t);
            // Use first track's metadata as defaults.
            if (result.coverPath().isEmpty()) {
                result.setCoverPath(t.coverPath());
                result.setCoverUrl(t.coverUrl());
            }
            if (result.year() == 0) {
                result.setYear(t.year());
            }
            if (result.genre().isEmpty()) {
                result.setGenre(t.genre());
            }
        }
    }
    result.setTracks(albumTracks);
    result.setTrackCount(albumTracks.size());

    albumCache_.insert(cacheKey, result);
    return result;
}

QList<ArtistInfo> ArtistAlbumService::allArtists() const
{
    if (!repo_) return {};
    // Use the repository's built-in artist list.
    return repo_->allArtists();
}

QList<AlbumInfo> ArtistAlbumService::allAlbums() const
{
    if (!repo_) return {};
    return repo_->allAlbums();
}

QList<AlbumInfo> ArtistAlbumService::albumsForArtist(const QString& artistName) const
{
    if (!repo_) return {};
    // First try the artist's cached albums.
    if (artistCache_.contains(artistName)) {
        return artistCache_.value(artistName).albums();
    }
    // Fall back to building from tracks.
    ArtistInfo a = artistByName(artistName);
    return a.albums();
}

QList<MusicFile> ArtistAlbumService::tracksForArtist(const QString& artistName) const
{
    if (!repo_) return {};
    return artistByName(artistName).tracks();
}

void ArtistAlbumService::recordArtistBrowsed(const QString& artistName)
{
    ArtistInfo a = artistByName(artistName);
    a.setLastBrowsedAt(QDateTime::currentDateTime());
    artistCache_.insert(artistName, a);
    emit artistUpdated(artistName);
}

void ArtistAlbumService::recordAlbumBrowsed(const QString& albumName,
                                            const QString& artist)
{
    const QString cacheKey = artist.toLower() + QLatin1Char('/') + albumName.toLower();
    AlbumInfo a = albumByName(albumName, artist);
    a.setLastBrowsedAt(QDateTime::currentDateTime());
    albumCache_.insert(cacheKey, a);
    emit albumUpdated(albumName, artist);
}

} // namespace mf::core::services

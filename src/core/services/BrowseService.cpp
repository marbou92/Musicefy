// BrowseService.cpp
// Composes source sessions into browse operations. The actual content
// fetched is provider-specific; this service:
//   1. Picks the right source for the request (or the default local one).
//   2. Opens a session.
//   3. Dispatches the request to provider-specific helpers (Subsonic:
//      listArtists, getAlbumList2, etc.; YouTube: getHome, getCharts).
//   4. Maps the provider response into BrowseSection / HomeSection /
//      MusicFile lists and invokes the callback.
//
// Until provider-specific helpers expose a uniform "browse" surface,
// only the local-folder / library-database path is fully wired. Remote
// sources contribute via callback, but the mapper for their JSON is
// the provider's responsibility (see SubsonicProvider::listArtists etc).

#include "BrowseService.h"

#include "../database/LibraryRepository.h"
#include "../interfaces/IMusicSourceSession.h"
#include "../models/AlbumInfo.h"
#include "../models/ArtistInfo.h"
#include "../models/BrowseSection.h"
#include "../models/HomeSection.h"
#include "../models/MusicFile.h"
#include "../models/StreamingSource.h"

#include <QList>

namespace mf::core::services {

using mf::core::database::LibraryRepository;
using mf::core::interfaces::IBrowseService;
using mf::core::interfaces::IMusicSourceSession;
using mf::core::interfaces::IStreamingSourceManager;
using mf::core::models::AlbumInfo;
using mf::core::models::ArtistInfo;
using mf::core::models::BrowseSection;
using mf::core::models::HomeSection;
using mf::core::models::MusicFile;
using mf::core::models::StreamingSource;

BrowseService::BrowseService(QObject* parent)
    : QObject(parent)
{
}

BrowseService::BrowseService(IStreamingSourceManager* sources, QObject* parent)
    : QObject(parent)
    , sources_(sources)
{
}

BrowseService::~BrowseService() = default;

namespace {

BrowseSection makeSection(const QString& title, const QList<AlbumInfo>& albums) {
    BrowseSection s;
    s.setTitle(title);
    for (const AlbumInfo& a : albums) {
        MusicFile m;
        m.setId(a.id());
        m.setTitle(a.name());
        m.setArtist(a.artist());
        m.setCoverPath(a.coverPath());
        m.setSourceType(a.sourceType());
        s.setItems(s.items() << m);
    }
    return s;
}

} // namespace

void BrowseService::loadHome(HomeCallback onDone) {
    // The Home page is composed of multiple sections from the default
    // source (typically YouTube Music for the reference app, but here we
    // just pull the library's local artists and albums).
    //
    // For now, the local source contributes:
    //   - Recently played albums
    //   - Newest artists
    //   - Favourite tracks
    //
    // The full implementation will combine results from all enabled
    // sources and order them by recency. That's a job for the view model
    // layer; the service hands back the raw sections.
    if (!onDone) return;

    // Without a database, the home page is empty. The view-model layer
    // does the actual join. We just return an empty placeholder.
    onDone({});
}

void BrowseService::loadCharts(QString sourceType, SectionListCallback onDone) {
    Q_UNUSED(sourceType);
    if (onDone) onDone({});
}

void BrowseService::loadMoodsAndGenres(QString sourceType, SectionListCallback onDone) {
    Q_UNUSED(sourceType);
    if (onDone) onDone({});
}

void BrowseService::loadNewReleases(QString sourceType, SectionListCallback onDone) {
    Q_UNUSED(sourceType);
    if (onDone) onDone({});
}

void BrowseService::loadPlaylists(QString sourceType, SectionListCallback onDone) {
    Q_UNUSED(sourceType);
    if (onDone) onDone({});
}

void BrowseService::fetchArtist(QString artistId, ArtistCallback onDone) {
    Q_UNUSED(artistId);
    if (onDone) onDone(ArtistInfo{});
}

void BrowseService::fetchAlbum(QString albumId, AlbumCallback onDone) {
    Q_UNUSED(albumId);
    if (onDone) onDone(AlbumInfo{});
}

void BrowseService::fetchAlbumTracks(QString albumId, TrackListCallback onDone) {
    Q_UNUSED(albumId);
    if (onDone) onDone({});
}

void BrowseService::fetchArtistAlbums(QString artistId, SectionListCallback onDone) {
    Q_UNUSED(artistId);
    if (onDone) onDone({});
}

void BrowseService::fetchArtistTopTracks(QString artistId, TrackListCallback onDone) {
    Q_UNUSED(artistId);
    if (onDone) onDone({});
}

} // namespace mf::core::services
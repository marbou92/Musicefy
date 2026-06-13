// BrowseService.h
// Composes source sessions into the high-level browse operations used
// by the UI: home, charts, moods, new releases, playlists, artist and
// album details. The service is a thin coordinator — it dispatches to
// the registered IStreamingSourceManager and forwards the results.

#pragma once

#include "../interfaces/IBrowseService.h"
#include "../interfaces/IStreamingSourceManager.h"

#include <QObject>
#include <QString>

#include <memory>

namespace mf::core::services {

class BrowseService : public QObject, public mf::core::interfaces::IBrowseService {
    Q_OBJECT
public:
    explicit BrowseService(QObject* parent = nullptr);
    explicit BrowseService(mf::core::interfaces::IStreamingSourceManager* sources,
                           QObject* parent = nullptr);
    ~BrowseService() override;

    void loadHome(HomeCallback onDone) override;
    void loadCharts(QString sourceType, SectionListCallback onDone) override;
    void loadMoodsAndGenres(QString sourceType, SectionListCallback onDone) override;
    void loadNewReleases(QString sourceType, SectionListCallback onDone) override;
    void loadPlaylists(QString sourceType, SectionListCallback onDone) override;

    void fetchArtist(QString artistId, ArtistCallback onDone) override;
    void fetchAlbum(QString albumId, AlbumCallback onDone) override;
    void fetchAlbumTracks(QString albumId, TrackListCallback onDone) override;
    void fetchArtistAlbums(QString artistId, SectionListCallback onDone) override;
    void fetchArtistTopTracks(QString artistId, TrackListCallback onDone) override;

private:
    mf::core::interfaces::IStreamingSourceManager* sources_ = nullptr;
};

} // namespace mf::core::services

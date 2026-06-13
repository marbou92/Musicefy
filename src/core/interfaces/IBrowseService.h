#pragma once

#include "../models/ArtistInfo.h"
#include "../models/AlbumInfo.h"
#include "../models/MusicFile.h"
#include "../models/BrowseSection.h"
#include "../models/HomeSection.h"

#include <QString>

#include <functional>

namespace mf::core::interfaces {

class IBrowseService {
public:
    virtual ~IBrowseService() = default;

    using ArtistCallback     = std::function<void(mf::core::models::ArtistInfo)>;
    using AlbumCallback      = std::function<void(mf::core::models::AlbumInfo)>;
    using TrackListCallback  = std::function<void(QList<mf::core::models::MusicFile>)>;
    using SectionListCallback = std::function<void(QList<mf::core::models::BrowseSection>)>;
    using HomeCallback       = std::function<void(QList<mf::core::models::HomeSection>)>;

    virtual void loadHome(HomeCallback onDone) = 0;
    virtual void loadCharts(QString sourceType, SectionListCallback onDone) = 0;
    virtual void loadMoodsAndGenres(QString sourceType, SectionListCallback onDone) = 0;
    virtual void loadNewReleases(QString sourceType, SectionListCallback onDone) = 0;
    virtual void loadPlaylists(QString sourceType, SectionListCallback onDone) = 0;

    virtual void fetchArtist(QString artistId, ArtistCallback onDone) = 0;
    virtual void fetchAlbum(QString albumId, AlbumCallback onDone) = 0;
    virtual void fetchAlbumTracks(QString albumId, TrackListCallback onDone) = 0;
    virtual void fetchArtistAlbums(QString artistId, SectionListCallback onDone) = 0;
    virtual void fetchArtistTopTracks(QString artistId, TrackListCallback onDone) = 0;
};

} // namespace mf::core::interfaces

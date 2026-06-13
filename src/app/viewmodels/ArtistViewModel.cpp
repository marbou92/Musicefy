// ArtistViewModel.cpp
// See header. The view model orchestrates three async lookups
// (fetchArtist + fetchArtistTopTracks + fetchArtistAlbums) and
// publishes the merged result as Q_PROPERTYs.

#include "ArtistViewModel.h"

#include "../core/playback/QueueManager.h"
#include "../core/services/BrowseService.h"
#include "../core/services/ToastService.h"

namespace mf::app::viewmodels {

using mf::core::models::AlbumInfo;
using mf::core::models::ArtistInfo;
using mf::core::models::BrowseSection;
using mf::core::models::MusicFile;
using mf::core::playback::QueueManager;
using mf::core::services::BrowseService;
using mf::core::services::ToastService;

ArtistViewModel::ArtistViewModel(BrowseService* browse,
                                 QueueManager*  queue,
                                 ToastService*  toasts,
                                 QObject*       parent)
    : QObject(parent)
    , browse_(browse)
    , queue_(queue)
    , toasts_(toasts)
{
}

void ArtistViewModel::setInfo(const ArtistInfo& info) {
    info_ = info;
    emit infoChanged();
}

void ArtistViewModel::setLoading(bool v) {
    if (isLoading_ == v) return;
    isLoading_ = v;
    emit loadingChanged();
}

void ArtistViewModel::flattenAlbums(const QList<BrowseSection>& sections) {
    albums_.clear();
    for (const BrowseSection& s : sections) {
        for (const MusicFile& m : s.items()) {
            albums_.append(m);
        }
    }
}

void ArtistViewModel::loadById(const QString& id) {
    pendingId_ = id;
    if (!browse_) {
        emit errorReported(QStringLiteral("Browse service unavailable"));
        return;
    }
    if (id.isEmpty()) {
        emit errorReported(QStringLiteral("Missing artist id"));
        return;
    }
    setLoading(true);

    // Stage 1: artist metadata.
    browse_->fetchArtist(id, [this](ArtistInfo a) {
        if (a.id() != pendingId_) return;
        info_ = a;
        emit infoChanged();
    });

    // Stage 2: top tracks (separate callback chain so the
    // service can short-circuit the metadata path if needed).
    browse_->fetchArtistTopTracks(id, [this, id](QList<MusicFile> tracks) {
        if (id != pendingId_) return;
        topTracks_ = std::move(tracks);
        emit contentChanged();
    });

    // Stage 3: discography.
    browse_->fetchArtistAlbums(id, [this, id](QList<BrowseSection> sections) {
        if (id != pendingId_) return;
        flattenAlbums(sections);
        emit contentChanged();
        setLoading(false);
    });
}

void ArtistViewModel::enqueueAndPlay(int startIndex,
                                     const QList<MusicFile>& list) {
    if (!queue_) { emit errorReported(QStringLiteral("Queue unavailable")); return; }
    if (list.isEmpty()) return;
    if (startIndex < 0 || startIndex >= list.size()) return;
    queue_->clear();
    queue_->enqueueMany(list);
    queue_->setCurrentIndex(startIndex);
}

void ArtistViewModel::playAll() {
    enqueueAndPlay(0, topTracks_);
}

void ArtistViewModel::shufflePlay() {
    if (!queue_) { emit errorReported(QStringLiteral("Queue unavailable")); return; }
    if (topTracks_.isEmpty()) return;
    if (!queue_->isShuffle()) queue_->setShuffle(true);
    queue_->clear();
    queue_->enqueueMany(topTracks_);
    queue_->setCurrentIndex(0);
}

void ArtistViewModel::playTrackAt(int row) {
    if (!queue_) { emit errorReported(QStringLiteral("Queue unavailable")); return; }
    if (row < 0 || row >= topTracks_.size()) return;
    const MusicFile& t = topTracks_.at(row);
    if (t.filePath().isEmpty() && t.sourceUri().isEmpty()) return;
    queue_->clear();
    queue_->enqueue(t);
    queue_->setCurrentIndex(0);
}

void ArtistViewModel::toggleFollowed() {
    info_.toggleFollowed();
    emit followedChanged();
}

void ArtistViewModel::openAlbumAt(int index) {
    if (index < 0 || index >= albums_.size()) return;
    const MusicFile& m = albums_.at(index);
    if (m.id().isEmpty()) return;
    AlbumInfo info;
    info.setId(m.id());
    info.setName(m.title());
    info.setArtist(m.artist());
    info.setCoverPath(m.coverPath());
    info.setSourceType(m.sourceType());
    info.setYear(m.year());
    info.setGenre(m.genre());
    emit openAlbumRequested(info);
}

} // namespace mf::app::viewmodels

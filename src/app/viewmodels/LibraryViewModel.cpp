// LibraryViewModel.cpp
// See header. All heavy lifting is in LibraryRepository; we just
// pull, cache, and emit.

#include "LibraryViewModel.h"

#include "../core/database/LibraryRepository.h"
#include "../core/playback/QueueManager.h"
#include "../core/models/PlaylistInfo.h"

#include <QUuid>

namespace mf::app::viewmodels {

using mf::core::database::LibraryRepository;
using mf::core::playback::QueueManager;
using mf::core::models::MusicFile;
using mf::core::models::ArtistInfo;
using mf::core::models::AlbumInfo;
using mf::core::models::PlaylistInfo;

LibraryViewModel::LibraryViewModel(LibraryRepository* repo,
                                   QueueManager*      queue,
                                   QObject*           parent)
    : QObject(parent)
    , repo_(repo)
    , queue_(queue)
{
    loadAll();
}

void LibraryViewModel::loadAll() {
    if (!repo_) {
        tracks_ = {};
        artists_ = {};
        albums_ = {};
        playlists_ = {};
    } else {
        tracks_    = repo_->allTracks();
        artists_   = repo_->allArtists();
        albums_    = repo_->allAlbums();
        playlists_ = repo_->allPlaylists();
    }
    emit tracksChanged();
    emit artistsChanged();
    emit albumsChanged();
    emit playlistsChanged();
    emit libraryChanged();
}

QList<MusicFile> LibraryViewModel::favouriteTracks() const {
    if (!repo_) return {};
    return repo_->favouriteTracks();
}

void LibraryViewModel::refresh() {
    loadAll();
}

void LibraryViewModel::playTrack(const QString& filePath) {
    if (!repo_ || !queue_) return;
    auto all = repo_->allTracks();
    int idx = -1;
    for (int i = 0; i < all.size(); ++i) {
        if (all[i].filePath() == filePath) { idx = i; break; }
    }
    if (idx < 0) return;
    queue_->clear();
    queue_->enqueueMany(all);
    queue_->setCurrentIndex(idx);
}

void LibraryViewModel::playAll() {
    if (!queue_) return;
    queue_->clear();
    queue_->enqueueMany(tracks_);
    if (!tracks_.isEmpty()) {
        queue_->setCurrentIndex(0);
    }
}

void LibraryViewModel::toggleFavourite(const QString& filePath) {
    if (!repo_) return;
    repo_->toggleFavourite(filePath);
    emit tracksChanged();   // favouriteTracks() changed, and the per-track flag changed
    emit libraryChanged();
}

void LibraryViewModel::deleteTrack(const QString& filePath) {
    if (!repo_) return;
    repo_->deleteTrack(filePath);
    loadAll();
}

void LibraryViewModel::createPlaylist(const QString& name) {
    if (!repo_) return;
    PlaylistInfo p;
    p.setId(QUuid::createUuid().toString(QUuid::WithoutBraces));
    p.setName(name);
    p.setSourceType(QStringLiteral("local"));
    p.setTrackCount(0);
    repo_->upsertPlaylist(p);
    playlists_ = repo_->allPlaylists();
    emit playlistsChanged();
    emit libraryChanged();
}

MusicFile LibraryViewModel::trackAt(int row) const {
    if (row < 0 || row >= tracks_.size()) return MusicFile();
    return tracks_[row];
}

int LibraryViewModel::rowForTrack(const QString& filePath) const {
    for (int i = 0; i < tracks_.size(); ++i) {
        if (tracks_[i].filePath() == filePath) return i;
    }
    return -1;
}

} // namespace mf::app::viewmodels
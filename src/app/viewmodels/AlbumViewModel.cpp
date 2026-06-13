// AlbumViewModel.cpp
// See header. All business logic lives in QueueManager; the view
// model just translates AlbumInfo into Q_PROPERTYs and forwards
// commands.

#include "AlbumViewModel.h"

#include "../core/playback/QueueManager.h"

namespace mf::app::viewmodels {

using mf::core::models::AlbumInfo;
using mf::core::models::MusicFile;
using mf::core::playback::QueueManager;

AlbumViewModel::AlbumViewModel(QueueManager* queue, QObject* parent)
    : QObject(parent)
    , queue_(queue)
{
}

void AlbumViewModel::setInfo(const AlbumInfo& info) {
    const bool savedChanged_ = (info.isSaved() != info_.isSaved());
    info_ = info;
    emit infoChanged();
    if (savedChanged_) emit savedChanged();
}

QString AlbumViewModel::coverUrl() const {
    // AlbumInfo::coverUrl() is a method; we forward it.
    return info_.coverUrl();
}

qint64 AlbumViewModel::totalDurationMs() const {
    qint64 totalSec = 0;
    for (const auto& t : info_.tracks()) totalSec += t.duration().count();
    return totalSec * 1000;
}

void AlbumViewModel::playAll() {
    if (!queue_) { emit errorReported(QStringLiteral("Queue unavailable")); return; }
    if (info_.tracks().isEmpty()) return;
    queue_->clear();
    queue_->enqueueMany(info_.tracks());
    queue_->setCurrentIndex(0);
}

void AlbumViewModel::shufflePlay() {
    if (!queue_) { emit errorReported(QStringLiteral("Queue unavailable")); return; }
    if (info_.tracks().isEmpty()) return;
    if (!queue_->isShuffle()) queue_->setShuffle(true);
    queue_->clear();
    queue_->enqueueMany(info_.tracks());
    queue_->setCurrentIndex(0);
}

void AlbumViewModel::playTrackAt(int row) {
    if (!queue_) { emit errorReported(QStringLiteral("Queue unavailable")); return; }
    if (row < 0 || row >= info_.tracks().size()) return;
    const MusicFile& t = info_.tracks().at(row);
    if (t.filePath().isEmpty() && t.sourceUri().isEmpty()) return;
    queue_->clear();
    queue_->enqueue(t);
    queue_->setCurrentIndex(0);
}

void AlbumViewModel::toggleSaved() {
    info_.toggleSaved();
    emit savedChanged();
}

} // namespace mf::app::viewmodels

// PlaylistViewModel.cpp
// See header.

#include "PlaylistViewModel.h"

#include "../core/database/Database.h"
#include "../core/database/LibraryRepository.h"
#include "../core/models/PlaylistInfo.h"
#include "../core/playback/QueueManager.h"

#include <QDateTime>
#include <QDebug>
#include <QSqlError>
#include <QSqlQuery>
#include <QUuid>
#include <QVariant>

namespace mf::app::viewmodels {

using mf::core::models::MusicFile;
using mf::core::models::PlaylistInfo;
using mf::core::playback::QueueManager;

PlaylistViewModel::PlaylistViewModel(QueueManager* queue,
                                     mf::core::database::LibraryRepository* repo,
                                     QObject* parent)
    : QObject(parent)
    , queue_(queue)
    , repo_(repo)
{
}

void PlaylistViewModel::setInfo(const PlaylistInfo& info) {
    info_ = info;
    emit infoChanged();
    emit tracksChanged();
}

qint64 PlaylistViewModel::totalDurationMs() const {
    return info_.totalDuration().count() * 1000;
}

void PlaylistViewModel::recomputeDuration() {
    qint64 totalSec = 0;
    for (const auto& t : info_.tracks()) totalSec += t.duration().count();
    info_.setTotalDuration(std::chrono::seconds{totalSec});
    info_.setTrackCount(info_.tracks().size());
}

void PlaylistViewModel::playAll() {
    if (!queue_) { emit errorReported(QStringLiteral("Queue unavailable")); return; }
    if (info_.tracks().isEmpty()) return;
    queue_->clear();
    queue_->enqueueMany(info_.tracks());
    queue_->setCurrentIndex(0);
}

void PlaylistViewModel::shufflePlay() {
    if (!queue_) { emit errorReported(QStringLiteral("Queue unavailable")); return; }
    if (info_.tracks().isEmpty()) return;
    if (!queue_->isShuffle()) queue_->setShuffle(true);
    queue_->clear();
    queue_->enqueueMany(info_.tracks());
    queue_->setCurrentIndex(0);
}

void PlaylistViewModel::playTrackAt(int row) {
    if (!queue_) { emit errorReported(QStringLiteral("Queue unavailable")); return; }
    if (row < 0 || row >= info_.tracks().size()) return;
    const MusicFile& t = info_.tracks().at(row);
    if (t.filePath().isEmpty() && t.sourceUri().isEmpty()) return;
    queue_->clear();
    queue_->enqueue(t);
    queue_->setCurrentIndex(0);
}

void PlaylistViewModel::addTrack(const MusicFile& track) {
    if (!canEdit()) { emit errorReported(QStringLiteral("Read-only playlist")); return; }
    QList<MusicFile> tracks = info_.tracks();
    tracks.append(track);
    info_.setTracks(tracks);
    recomputeDuration();
    emit tracksChanged();
    emit trackAdded(tracks.size() - 1);
}

void PlaylistViewModel::removeTrackAt(int row) {
    if (!canEdit()) { emit errorReported(QStringLiteral("Read-only playlist")); return; }
    if (row < 0 || row >= info_.tracks().size()) return;
    QList<MusicFile> tracks = info_.tracks();
    tracks.removeAt(row);
    info_.setTracks(tracks);
    recomputeDuration();
    emit tracksChanged();
    emit trackRemoved(row);
}

bool PlaylistViewModel::reorder(int from, int to) {
    if (!canEdit()) { emit errorReported(QStringLiteral("Read-only playlist")); return false; }
    if (from < 0 || from >= info_.tracks().size()) return false;
    if (to   < 0 || to   >= info_.tracks().size()) return false;
    if (from == to) return true;
    QList<MusicFile> tracks = info_.tracks();
    MusicFile m = tracks.takeAt(from);
    tracks.insert(to, m);
    info_.setTracks(tracks);
    emit tracksChanged();
    emit trackMoved(from, to);
    return true;
}

void PlaylistViewModel::rename(const QString& newName) {
    if (!canEdit()) { emit errorReported(QStringLiteral("Read-only playlist")); return; }
    if (newName.trimmed().isEmpty()) return;
    if (newName == info_.name()) return;
    info_.setName(newName);
    info_.setLastModifiedAt(QDateTime::currentDateTimeUtc());
    emit infoChanged();
    emit nameChanged(newName);
    // Persist the rename to the database.
    if (repo_ && !info_.id().isEmpty()) {
        save();
    }
}

MusicFile PlaylistViewModel::trackAt(int row) const {
    if (row < 0 || row >= info_.tracks().size()) return MusicFile();
    return info_.tracks().at(row);
}

int PlaylistViewModel::rowForTrackId(const QString& trackId) const {
    const auto& tracks = info_.tracks();
    for (int i = 0; i < tracks.size(); ++i) {
        if (tracks[i].id() == trackId) return i;
    }
    return -1;
}

void PlaylistViewModel::save() {
    if (!repo_) { emit errorReported(QStringLiteral("No repository available")); return; }
    if (info_.name().trimmed().isEmpty()) { emit errorReported(QStringLiteral("Playlist has no name")); return; }

    // Ensure ID exists.
    if (info_.id().isEmpty()) {
        info_.setId(QUuid::createUuid().toString(QUuid::WithoutBraces));
    }
    info_.setLastModifiedAt(QDateTime::currentDateTime());
    recomputeDuration();

    // Upsert the playlist metadata.
    repo_->upsertPlaylist(info_);

    // Sync tracks: remove all existing, then re-add in order.
    {
        QSqlQuery q(repo_->database().connection());
        q.prepare(QStringLiteral("DELETE FROM playlist_tracks WHERE playlist_id = ?"));
        q.addBindValue(info_.id());
        if (!q.exec()) {
            qWarning() << "Failed to clear playlist tracks:" << q.lastError().text();
        }
    }

    // Re-add all tracks in order.
    for (int i = 0; i < info_.tracks().size(); ++i) {
        const auto& t = info_.tracks().at(i);
        QString trackId = t.id();
        if (trackId.isEmpty()) continue;
        repo_->addTrackToPlaylist(info_.id(), trackId, i);
    }

    emit saved();
}

void PlaylistViewModel::loadFromDb(const QString& playlistId) {
    if (!repo_) { emit errorReported(QStringLiteral("No repository available")); return; }
    if (playlistId.isEmpty()) return;

    // Find the playlist in the repository.
    const auto all = repo_->allPlaylists();
    for (const auto& p : all) {
        if (p.id() == playlistId) {
            info_ = p;
            // Load tracks for this playlist.
            QList<mf::core::models::MusicFile> tracks;
            QSqlQuery q(repo_->database().connection());
            q.prepare(QStringLiteral(
                "SELECT t.* FROM tracks t"
                " INNER JOIN playlist_tracks pt ON t.id = pt.track_id"
                " WHERE pt.playlist_id = ?"
                " ORDER BY pt.position"));
            q.addBindValue(playlistId);
            if (q.exec()) {
                while (q.next()) {
                    // Manually construct MusicFile from row.
                    mf::core::models::MusicFile m;
                    m.setId(q.value("id").toString());
                    m.setFilePath(q.value("file_path").toString());
                    m.setTitle(q.value("title").toString());
                    m.setArtist(q.value("artist").toString());
                    m.setAlbum(q.value("album").toString());
                    m.setYear(q.value("year").toInt());
                    m.setGenre(q.value("genre").toString());
                    m.setDuration(std::chrono::seconds{q.value("duration_secs").toInt()});
                    m.setTrackNumber(q.value("track_number").toInt());
                    m.setBitrate(q.value("bitrate").toInt());
                    m.setFileSize(q.value("file_size").toLongLong());
                    m.setLyrics(q.value("lyrics").toString());
                    m.setCoverPath(q.value("cover_path").toString());
                    m.setSourceUri(q.value("source_uri").toString());
                    m.setSourceType(q.value("source_type").toString());
                    m.setPlayCount(q.value("play_count").toInt());
                    m.setIsFavourite(q.value("is_favourite").toInt() != 0);
                    m.setIsDownloaded(q.value("is_downloaded").toInt() != 0);
                    tracks.append(m);
                }
            }
            info_.setTracks(tracks);
            recomputeDuration();
            emit infoChanged();
            emit tracksChanged();
            return;
        }
    }
    emit errorReported(QStringLiteral("Playlist not found"));
}

void PlaylistViewModel::deletePlaylist() {
    if (!repo_) { emit errorReported(QStringLiteral("No repository available")); return; }
    if (info_.id().isEmpty()) return;

    // Remove track mappings first.
    {
        QSqlQuery q(repo_->database().connection());
        q.prepare(QStringLiteral("DELETE FROM playlist_tracks WHERE playlist_id = ?"));
        q.addBindValue(info_.id());
        q.exec();
    }
    repo_->deletePlaylist(info_.id());

    // Clear local state.
    info_ = PlaylistInfo();
    emit infoChanged();
    emit tracksChanged();
    emit deleted();
}

} // namespace mf::app::viewmodels

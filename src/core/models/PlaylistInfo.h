// PlaylistInfo.h
// First-class playlist entity. Port of Musicefy.Core.Models.PlaylistInfo.

#pragma once

#include "MusicFile.h"

#include <QDateTime>
#include <QList>
#include <QString>

#include <chrono>

namespace mf::core::models {

class PlaylistInfo {
public:
    PlaylistInfo() = default;

    QString id() const { return id_; }
    void setId(QString v) { id_ = std::move(v); }

    QString name() const { return name_; }
    void setName(QString v) { name_ = std::move(v); }

    QDateTime createdAt() const { return createdAt_; }
    void setCreatedAt(QDateTime v) { createdAt_ = v; }

    std::optional<QDateTime> lastModifiedAt() const { return lastModifiedAt_; }
    void setLastModifiedAt(std::optional<QDateTime> v) { lastModifiedAt_ = v; }

    QString description() const { return description_; }
    void setDescription(QString v) { description_ = std::move(v); }

    QString coverPath() const { return coverPath_; }
    void setCoverPath(QString v) { coverPath_ = std::move(v); }

    QString youTubePlaylistId() const { return youTubePlaylistId_; }
    void setYouTubePlaylistId(QString v) { youTubePlaylistId_ = std::move(v); }

    QString sourceType() const { return sourceType_; }
    void setSourceType(QString v) { sourceType_ = std::move(v); }

    int trackCount() const { return trackCount_; }
    void setTrackCount(int v) { trackCount_ = v; }

    std::chrono::seconds totalDuration() const { return totalDuration_; }
    void setTotalDuration(std::chrono::seconds v) { totalDuration_ = v; }

    QList<MusicFile> tracks() const { return tracks_; }
    void setTracks(QList<MusicFile> v) { tracks_ = std::move(v); }

private:
    QString id_;
    QString name_;
    QDateTime createdAt_;
    std::optional<QDateTime> lastModifiedAt_;
    QString description_;
    QString coverPath_;
    QString youTubePlaylistId_;
    QString sourceType_;
    int trackCount_ = 0;
    std::chrono::seconds totalDuration_{0};
    QList<MusicFile> tracks_;
};

} // namespace mf::core::models

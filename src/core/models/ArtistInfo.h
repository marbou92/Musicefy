// ArtistInfo.h
// First-class artist entity. Port of Musicefy.Core.Models.ArtistInfo.

#pragma once

#include "MusicFile.h"

#include <QDateTime>
#include <QList>
#include <QString>
#include <optional>

namespace mf::core::models {

class AlbumInfo;

class ArtistInfo {
public:
    ArtistInfo() = default;

    QString id() const { return id_; }
    void setId(QString v) { id_ = std::move(v); }

    QString name() const { return name_; }
    void setName(QString v) { name_ = std::move(v); }

    QString coverPath() const { return coverPath_; }
    void setCoverPath(QString v) { coverPath_ = std::move(v); }

    QString sourceType() const { return sourceType_; }
    void setSourceType(QString v) { sourceType_ = std::move(v); }

    QString youTubeChannelId() const { return youTubeChannelId_; }
    void setYouTubeChannelId(QString v) { youTubeChannelId_ = std::move(v); }

    QString description() const { return description_; }
    void setDescription(QString v) { description_ = std::move(v); }

    std::optional<qint64> subscriberCount() const { return subscriberCount_; }
    void setSubscriberCount(std::optional<qint64> v) { subscriberCount_ = v; }

    bool isFollowed() const { return isFollowed_; }
    void setIsFollowed(bool v) { isFollowed_ = v; }
    void toggleFollowed() { isFollowed_ = !isFollowed_; }

    std::optional<QDateTime> lastBrowsedAt() const { return lastBrowsedAt_; }
    void setLastBrowsedAt(std::optional<QDateTime> v) { lastBrowsedAt_ = v; }

    QList<MusicFile> topTracks() const { return topTracks_; }
    void setTopTracks(QList<MusicFile> v) { topTracks_ = std::move(v); }

    QList<MusicFile> tracks() const { return tracks_; }
    void setTracks(QList<MusicFile> v) { tracks_ = std::move(v); }

    QList<AlbumInfo> albums() const;
    void setAlbums(QList<AlbumInfo> v); // defined in AlbumInfo.h (back-reference)

private:
    QString id_;
    QString name_;
    QString coverPath_;
    QString sourceType_;
    QString youTubeChannelId_;
    QString description_;
    std::optional<qint64> subscriberCount_;
    bool isFollowed_ = false;
    std::optional<QDateTime> lastBrowsedAt_;
    QList<MusicFile> topTracks_;
    QList<MusicFile> tracks_;
    QList<AlbumInfo> albums_; // forward decl, defined in AlbumInfo.h
};

} // namespace mf::core::models

// AlbumInfo.h
// First-class album entity. Port of Musicefy.Core.Models.AlbumInfo.

#pragma once

#include "MusicFile.h"

#include <QDateTime>
#include <QList>
#include <QString>
#include <optional>

namespace mf::core::models {

class AlbumInfo {
public:
    AlbumInfo() = default;

    QString id() const { return id_; }
    void setId(QString v) { id_ = std::move(v); }

    QString name() const { return name_; }
    void setName(QString v) { name_ = std::move(v); }

    QString artist() const { return artist_; }
    void setArtist(QString v) { artist_ = std::move(v); }

    QString artistId() const { return artistId_; }
    void setArtistId(QString v) { artistId_ = std::move(v); }

    int year() const { return year_; }
    void setYear(int v) { year_ = v; }

    QString coverPath() const { return coverPath_; }
    void setCoverPath(QString v) { coverPath_ = std::move(v); }

    // Remote cover URL (YouTube, Subsonic, etc.). Falls back to
    // coverPath() for local files.
    QString coverUrl() const;
    void    setCoverUrl(QString v) { coverUrl_ = std::move(v); }

    QString sourceType() const { return sourceType_; }
    void setSourceType(QString v) { sourceType_ = std::move(v); }

    QString youTubeAlbumId() const { return youTubeAlbumId_; }
    void setYouTubeAlbumId(QString v) { youTubeAlbumId_ = std::move(v); }

    QString description() const { return description_; }
    void setDescription(QString v) { description_ = std::move(v); }

    QString genre() const { return genre_; }
    void setGenre(QString v) { genre_ = std::move(v); }

    bool isSaved() const { return isSaved_; }
    void setIsSaved(bool v) { isSaved_ = v; }
    void toggleSaved() { isSaved_ = !isSaved_; }

    std::optional<QDateTime> lastBrowsedAt() const { return lastBrowsedAt_; }
    void setLastBrowsedAt(std::optional<QDateTime> v) { lastBrowsedAt_ = v; }

    int trackCount() const { return trackCount_; }
    void setTrackCount(int v) { trackCount_ = v; }

    QList<MusicFile> tracks() const { return tracks_; }
    void setTracks(QList<MusicFile> v) { tracks_ = std::move(v); }

private:
    QString id_;
    QString name_;
    QString artist_;
    QString artistId_;
    int year_ = 0;
    QString coverPath_;
    QString coverUrl_;
    QString sourceType_;
    QString youTubeAlbumId_;
    QString description_;
    QString genre_;
    bool isSaved_ = false;
    std::optional<QDateTime> lastBrowsedAt_;
    int trackCount_ = 0;
    QList<MusicFile> tracks_;
};

} // namespace mf::core::models

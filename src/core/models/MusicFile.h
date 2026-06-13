// MusicFile.h
// The single most important model: represents one track from any source.
// Port of Musicefy.Core.Models.MusicFile.

#pragma once

#include <QDateTime>
#include <QMetaType>
#include <QString>

#include <chrono>
#include <optional>

namespace mf::core::models {

class MusicFile {
public:
    MusicFile();
    MusicFile(QString title,
              QString artist,
              QString album = QString(),
              int year = 0,
              QString sourceUri = QString(),
              QString filePath = QString(),
              QString genre = QString(),
              std::chrono::seconds duration = std::chrono::seconds{0},
              int trackNumber = 0,
              QString sourceType = QStringLiteral("Local"),
              int bitrate = 0,
              qint64 fileSize = 0,
              QString lyrics = QString(),
              QString coverPath = QString());

    // ── Identity ────────────────────────────────────────────────────────
    QString id() const { return id_; }
    void setId(QString v) { id_ = std::move(v); }

    // ── File path (local) / remote URI ──────────────────────────────────
    QString filePath() const { return filePath_; }
    void setFilePath(QString v) { filePath_ = std::move(v); }

    // ── Core metadata ───────────────────────────────────────────────────
    QString title() const { return title_; }
    void setTitle(QString v) { title_ = std::move(v); }

    QString artist() const { return artist_; }
    void setArtist(QString v) { artist_ = std::move(v); }

    QString album() const { return album_; }
    void setAlbum(QString v) { album_ = std::move(v); }

    int year() const { return year_; }
    void setYear(int v) { year_ = v; }

    QString genre() const { return genre_; }
    void setGenre(QString v) { genre_ = std::move(v); }

    std::chrono::seconds duration() const { return duration_; }
    void setDuration(std::chrono::seconds v) { duration_ = v; }

    int trackNumber() const { return trackNumber_; }
    void setTrackNumber(int v) { trackNumber_ = v; }

    // ── Extended metadata ───────────────────────────────────────────────
    int bitrate() const { return bitrate_; }
    void setBitrate(int v) { bitrate_ = v; }

    qint64 fileSize() const { return fileSize_; }
    void setFileSize(qint64 v) { fileSize_ = v; }

    QString lyrics() const { return lyrics_; }
    void setLyrics(QString v) { lyrics_ = std::move(v); }

    QString coverPath() const { return coverPath_; }
    void setCoverPath(QString v) { coverPath_ = std::move(v); }

    // Remote cover URL (YouTube, Subsonic, etc.). Falls back to coverPath()
    // for local files.
    QString coverUrl() const;
    void    setCoverUrl(QString v) { coverUrl_ = std::move(v); }

    // ── Source info ─────────────────────────────────────────────────────
    QString sourceUri() const { return sourceUri_; }
    void setSourceUri(QString v) { sourceUri_ = std::move(v); }

    QString sourceType() const { return sourceType_; }
    void setSourceType(QString v) { sourceType_ = std::move(v); }

    // ── User interaction ────────────────────────────────────────────────
    int playCount() const { return playCount_; }
    void setPlayCount(int v) { playCount_ = v; }

    QDateTime lastPlayed() const { return lastPlayed_; }
    void setLastPlayed(QDateTime v) { lastPlayed_ = v; }

    bool isFavourite() const { return isFavourite_; }
    void setIsFavourite(bool v) { isFavourite_ = v; }
    void toggleFavourite() { isFavourite_ = !isFavourite_; }

    bool isDownloaded() const { return isDownloaded_; }
    void setIsDownloaded(bool v) { isDownloaded_ = v; }

    // ── AlbumArtist ─────────────────────────────────────────────────────
    QString albumArtist() const { return albumArtist_; }
    void setAlbumArtist(QString v) { albumArtist_ = std::move(v); }

    // ── YouTube browse IDs ──────────────────────────────────────────────
    QString albumBrowseId() const { return albumBrowseId_; }
    void setAlbumBrowseId(QString v) { albumBrowseId_ = std::move(v); }

    QString artistBrowseId() const { return artistBrowseId_; }
    void setArtistBrowseId(QString v) { artistBrowseId_ = std::move(v); }

    // ── When added to library ───────────────────────────────────────────
    std::optional<QDateTime> dateAdded() const { return dateAdded_; }
    void setDateAdded(std::optional<QDateTime> v) { dateAdded_ = v; }

    // ── YouTube-specific metadata ───────────────────────────────────────
    QString youTubeVideoId() const { return youTubeVideoId_; }
    void setYouTubeVideoId(QString v) { youTubeVideoId_ = std::move(v); }

    QString youTubeBrowseId() const { return youTubeBrowseId_; }
    void setYouTubeBrowseId(QString v) { youTubeBrowseId_ = std::move(v); }

    QString youTubePlaylistId() const { return youTubePlaylistId_; }
    void setYouTubePlaylistId(QString v) { youTubePlaylistId_ = std::move(v); }

    QString youTubeMusicVideoType() const { return youTubeMusicVideoType_; }
    void setYouTubeMusicVideoType(QString v) { youTubeMusicVideoType_ = std::move(v); }

    std::optional<double> loudnessDb() const { return loudnessDb_; }
    void setLoudnessDb(std::optional<double> v) { loudnessDb_ = v; }

    QString audioFormat() const { return audioFormat_; }
    void setAudioFormat(QString v) { audioFormat_ = std::move(v); }

    // ── Behavior ────────────────────────────────────────────────────────
    void markPlayed();

    // Equality is by FilePath (case-insensitive). Useful for queue
    // deduplication and library lookup.
    bool operator==(const MusicFile& other) const;
    bool operator!=(const MusicFile& other) const { return !(*this == other); }

    QString toDisplayString() const;

private:
    QString id_;
    QString filePath_;
    QString title_;
    QString artist_;
    QString album_;
    int year_ = 0;
    QString genre_;
    std::chrono::seconds duration_{0};
    int trackNumber_ = 0;
    int bitrate_ = 0;
    qint64 fileSize_ = 0;
    QString lyrics_;
    QString coverPath_;
    QString coverUrl_;
    QString sourceUri_;
    QString sourceType_;
    int playCount_ = 0;
    QDateTime lastPlayed_;
    bool isFavourite_ = false;
    bool isDownloaded_ = false;
    QString albumArtist_;
    QString albumBrowseId_;
    QString artistBrowseId_;
    std::optional<QDateTime> dateAdded_;
    QString youTubeVideoId_;
    QString youTubeBrowseId_;
    QString youTubePlaylistId_;
    QString youTubeMusicVideoType_;
    std::optional<double> loudnessDb_;
    QString audioFormat_;
};

} // namespace mf::core::models

// QVariant interop: lets the queue list view stash the full
// MusicFile in Qt::UserRole so a drag-reorder can read the
// post-move order back out of the model and feed it to
// QueueManager::setOrderFromVisible.
Q_DECLARE_METATYPE(mf::core::models::MusicFile)

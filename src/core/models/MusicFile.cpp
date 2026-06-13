// MusicFile.cpp

#include "MusicFile.h"

#include <QUuid>

namespace mf::core::models {

MusicFile::MusicFile()
    : id_(QUuid::createUuid().toString(QUuid::WithoutBraces))
    , lastPlayed_(QDateTime::fromSecsSinceEpoch(0))
{
}

MusicFile::MusicFile(QString title,
                     QString artist,
                     QString album,
                     int year,
                     QString sourceUri,
                     QString filePath,
                     QString genre,
                     std::chrono::seconds duration,
                     int trackNumber,
                     QString sourceType,
                     int bitrate,
                     qint64 fileSize,
                     QString lyrics,
                     QString coverPath)
    : id_(QUuid::createUuid().toString(QUuid::WithoutBraces))
    , filePath_(filePath.isEmpty() ? sourceUri : filePath)
    , title_(std::move(title))
    , artist_(std::move(artist))
    , album_(std::move(album))
    , year_(year)
    , genre_(std::move(genre))
    , duration_(duration)
    , trackNumber_(trackNumber)
    , bitrate_(bitrate)
    , fileSize_(fileSize)
    , lyrics_(std::move(lyrics))
    , coverPath_(std::move(coverPath))
    , sourceUri_(std::move(sourceUri))
    , sourceType_(std::move(sourceType))
    , playCount_(0)
    , lastPlayed_(QDateTime::fromSecsSinceEpoch(0))
    , isFavourite_(false)
    , isDownloaded_(false)
{
}

void MusicFile::markPlayed() {
    ++playCount_;
    lastPlayed_ = QDateTime::currentDateTime();
}

QString MusicFile::coverUrl() const {
    if (!coverUrl_.isEmpty()) return coverUrl_;
    return coverPath_;
}

bool MusicFile::operator==(const MusicFile& other) const {
    return QString::compare(filePath_, other.filePath_, Qt::CaseInsensitive) == 0;
}

QString MusicFile::toDisplayString() const {
    if (artist_.isEmpty()) {
        return title_;
    }
    return title_ + QStringLiteral(" - ") + artist_;
}

} // namespace mf::core::models

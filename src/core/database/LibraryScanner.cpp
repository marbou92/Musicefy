#include "LibraryScanner.h"

#include "Database.h"
#include "../models/MusicFileExtensions.h"
#include <QDebug>
#include <QVariant>
#include <QDir>
#include <QFileInfo>

#include <QDir>
#include <QDirIterator>
#include <QFileInfo>
#include <QSqlQuery>
#include <QSqlError>
#include <QUuid>

#include <chrono>

namespace mf::core::database {

using mf::core::models::MusicFile;
using mf::core::models::MusicFileExtensions;

LibraryScanner::LibraryScanner(Database& db)
    : db_(db)
{
}

bool LibraryScanner::isSupportedFile(const QString& filePath) {
    QFileInfo fi(filePath);
    QString suffix = fi.suffix().toLower();
    return MusicFileExtensions::Suffixes().contains(suffix);
}

void LibraryScanner::scan(const QStringList& rootFolders, ProgressCallback onProgress) {
    totalProcessed_ = 0;
    totalAdded_     = 0;
    totalUpdated_   = 0;
    cancelled_      = false;

    for (const QString& root : rootFolders) {
        if (cancelled_) break;
        QDir d(root);
        if (!d.exists()) {
            continue;
        }
        walkFolder(root, MusicFileExtensions::SuffixList(), onProgress);
    }
}

void LibraryScanner::walkFolder(const QString& folder, const QStringList& audioExtensions, ProgressCallback onProgress) {
    QDirIterator it(folder, audioExtensions, QDir::Files, QDirIterator::Subdirectories);
    while (it.hasNext()) {
        if (cancelled_) break;
        QString path = it.next();
        if (processFile(path)) {
            ++totalAdded_;
        } else {
            ++totalUpdated_;
        }
        ++totalProcessed_;
        if (onProgress) {
            ScanProgress p;
            p.current = totalProcessed_;
            p.total   = -1;
            p.currentFile = path;
            onProgress(p);
        }
    }
}

bool LibraryScanner::processFile(const QString& filePath) {
    MusicFile m = readTags(filePath);
    m.setFilePath(filePath);
    m.setSourceType(QStringLiteral("Local"));
    if (m.id().isEmpty()) {
        m.setId(QUuid::createUuid().toString(QUuid::WithoutBraces));
    }
    if (m.dateAdded().has_value() == false) {
        m.setDateAdded(QDateTime::currentDateTime());
    }

    QSqlQuery q(db_.connection());
    q.prepare(QStringLiteral(
        "INSERT INTO tracks ("
        "id, file_path, title, artist, album, year, genre,"
        "duration_secs, track_number, bitrate, file_size, source_type,"
        "play_count, last_played, is_favourite, is_downloaded, date_added"
        ") VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)"
        " ON CONFLICT(file_path) DO UPDATE SET"
        " title=excluded.title, artist=excluded.artist, album=excluded.album,"
        " year=excluded.year, genre=excluded.genre,"
        " duration_secs=excluded.duration_secs, track_number=excluded.track_number,"
        " bitrate=excluded.bitrate, file_size=excluded.file_size"
    ));
    q.addBindValue(m.id());
    q.addBindValue(m.filePath());
    q.addBindValue(m.title());
    q.addBindValue(m.artist());
    q.addBindValue(m.album());
    q.addBindValue(m.year());
    q.addBindValue(m.genre());
    q.addBindValue(static_cast<qint64>(m.duration().count()));
    q.addBindValue(m.trackNumber());
    q.addBindValue(m.bitrate());
    q.addBindValue(m.fileSize());
    q.addBindValue(m.sourceType());
    q.addBindValue(m.playCount());
    q.addBindValue(m.lastPlayed().isValid() ? m.lastPlayed().toSecsSinceEpoch() : QVariant());
    q.addBindValue(m.isFavourite() ? 1 : 0);
    q.addBindValue(m.isDownloaded() ? 1 : 0);
    q.addBindValue(m.dateAdded().has_value() ? m.dateAdded()->toSecsSinceEpoch() : QVariant());

    if (!q.exec()) {
        qWarning() << "processFile upsert failed for" << filePath << ":" << q.lastError().text();
        return false;
    }
    return true;
}

void LibraryScanner::cancel() {
    cancelled_ = true;
}

} // namespace mf::core::database

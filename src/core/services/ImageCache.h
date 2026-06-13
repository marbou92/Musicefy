// ImageCache.h
// On-disk + in-memory LRU cache for remote image bytes, keyed by URL.
// Used by the player to download cover art once and re-use it across
// sessions, and by the ArtworkEnrichment service to keep extracted
// seed colors warm.
//
// Disk layout:
//   <cacheDir>/image-cache/<sha1(url)>.bin   - raw image bytes
//   <cacheDir>/image-cache/<sha1(url)>.meta  - JSON sidecar (contentType, expiry, etag)
//
// In-memory LRU is small (default 32 entries) and lives next to the
// disk cache so QImage loads are fast for the current set of tracks.

#pragma once

#include <QByteArray>
#include <QHash>
#include <QList>
#include <QMutex>
#include <QObject>
#include <QString>
#include <QUrl>

#include <functional>
#include <memory>

namespace mf::core::sources { class HttpClient; }

namespace mf::core::services {

class ImageCache : public QObject {
    Q_OBJECT
public:
    struct Entry {
        QByteArray bytes;
        QString    contentType;
        qint64     expiresAtMs = 0; // 0 = never
        qint64     sizeBytes   = 0;
    };

    // Bytes callback: (bytes, contentType, errorMessage). Error is empty on success.
    using BytesCallback = std::function<void(QByteArray, QString, QString)>;

    explicit ImageCache(QObject* parent = nullptr);
    ImageCache(mf::core::sources::HttpClient* http,
               QString cacheDir,
               int  lruCapacity = 32,
               int  defaultTtlSeconds = 60 * 60 * 24 * 30, // 30 days
               qint64 maxDiskBytes = 200LL * 1024 * 1024,   // 200 MB
               QObject* parent = nullptr);
    ~ImageCache() override;

    // True if we already have the image on disk or in memory.
    bool contains(const QUrl& url) const;

    // Synchronous lookup. Returns the bytes (empty if not cached) and
    // the content type (set if known). Refuses expired entries.
    QByteArray peek(const QUrl& url, QString* contentTypeOut = nullptr) const;

    // Async fetch. Hits the cache first; otherwise downloads via HttpClient
    // and stores. The callback always runs; err is empty on success.
    void get(const QUrl& url, BytesCallback cb);

    // Manually store bytes (e.g. from a provider that already downloaded them).
    void put(const QUrl& url, const QByteArray& bytes, const QString& contentType);

    // Drop a single entry or everything.
    void remove(const QUrl& url);
    void clear();

    // Stats.
    qint64 diskBytes() const { return diskBytes_; }
    int    diskCount() const { return diskCount_; }

    // Config.
    void   setDefaultTtl(int seconds) { defaultTtlSec_ = seconds; }
    int    defaultTtl() const { return defaultTtlSec_; }
    void   setMaxDiskBytes(qint64 bytes) { maxDiskBytes_ = bytes; }
    qint64 maxDiskBytes() const { return maxDiskBytes_; }
    void   setCacheDirectory(const QString& dir);
    QString cacheDirectory() const { return cacheDir_; }

signals:
    void evicted(QString url, QString reason);
    void stored(QString url);

private:
    struct DiskFile {
        QByteArray bytes;
        QString    contentType;
        qint64     expiresAtMs = 0;
    };

    QString cacheKey(const QUrl& url) const;
    QString diskPathFor(const QString& key, bool meta) const;
    void    loadFromDisk(const QString& key, DiskFile& out) const;
    void    saveToDisk(const QString& key, const DiskFile& file);
    void    enforceDiskLimit();
    void    touchLru(const QString& key);
    void    evictExpired();

    mf::core::sources::HttpClient* http_ = nullptr;
    QString                        cacheDir_;
    int                            lruCapacity_    = 32;
    int                            defaultTtlSec_   = 60 * 60 * 24 * 30;
    qint64                         maxDiskBytes_   = 200LL * 1024 * 1024;

    mutable QMutex              mutex_;
    QHash<QString, DiskFile>    disk_;        // by cache key
    QStringList                 lruOrder_;    // most-recent at the end
    qint64                      diskBytes_   = 0;
    int                         diskCount_   = 0;
};

} // namespace mf::core::services

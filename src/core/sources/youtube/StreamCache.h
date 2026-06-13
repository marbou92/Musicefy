// StreamCache.h
// In-memory cache for resolved YouTube stream URLs with TTL.
//
// Stream URLs from InnerTube expire after ~6 hours (YouTube's expiry
// is roughly 21 540 s). We cache for slightly less than that to be
// safe and re-resolve on the next play.
//
// Thread-safe: all operations are protected by a QMutex. The cache is
// process-local; persistent caching is out of scope (and would be
// dangerous — the expiry timestamp must always be relative to fetch
// time, not app launch time).

#pragma once

#include <QByteArray>
#include <QHash>
#include <QMutex>
#include <QString>

#include <memory>

namespace mf::core::sources::youtube {

struct CachedStream {
    QString  videoId;
    QString  streamUrl;
    QString  mimeType;
    int      bitrate = 0;
    qint64   expiresAtMs = 0;  // QDateTime::currentMSecsSinceEpoch
};

class StreamCache {
public:
    explicit StreamCache(qint64 defaultTtlMs = kDefaultTtlMs);

    // Cache a resolved stream URL. `expiresInSeconds` is the value
    // returned by InnerTube (typically 21 540); we clamp to the
    // smaller of the two to be safe.
    void put(const QString& videoId,
             const QString& streamUrl,
             const QString& mimeType = QString(),
             int bitrate = 0,
             int expiresInSeconds = 21'540);

    // Returns the cached stream URL, or an empty string on miss.
    QString tryGet(const QString& videoId) const;

    // Returns full entry, or null on miss. Removes expired entries
    // before reporting a miss.
    std::unique_ptr<CachedStream> tryGetFull(const QString& videoId) const;

    void remove(const QString& videoId);
    void clear();
    void purgeExpired();

    int  count() const;
    bool isEmpty() const;

    // Cache TTL defaults. 5 hours is comfortably below YouTube's
    // typical 6 h expiry so we never hand out an expired URL even if
    // the system clock drifts.
    static constexpr qint64 kDefaultTtlMs = 5LL * 60LL * 60LL * 1000LL;
    static constexpr int    kMaxExpiresInSeconds = 21'540;

private:
    mutable QMutex          mutex_;
    qint64                  defaultTtlMs_;
    QHash<QString, CachedStream> entries_;
};

} // namespace mf::core::sources::youtube

// StreamCache.cpp — see StreamCache.h.

#include "StreamCache.h"

#include <QDateTime>
#include <QMutexLocker>

#include <algorithm>

namespace mf::core::sources::youtube {

StreamCache::StreamCache(qint64 defaultTtlMs)
    : defaultTtlMs_(defaultTtlMs) {
}

void StreamCache::put(const QString& videoId,
                      const QString& streamUrl,
                      const QString& mimeType,
                      int bitrate,
                      int expiresInSeconds) {
    if (videoId.isEmpty() || streamUrl.isEmpty()) return;

    QMutexLocker lock(&mutex_);

    // Use the lesser of the default TTL and the InnerTube-provided
    // expiry. InnerTube occasionally returns longer values for
    // bot-detected clients, but we don't want to keep stale URLs
    // around in case the user's IP gets rotated.
    const int safeExpires = std::clamp(expiresInSeconds, 1, kMaxExpiresInSeconds);
    const qint64 ttlMs = std::min<qint64>(defaultTtlMs_,
                                          static_cast<qint64>(safeExpires) * 1000LL);

    CachedStream e;
    e.videoId     = videoId;
    e.streamUrl   = streamUrl;
    e.mimeType    = mimeType;
    e.bitrate     = bitrate;
    e.expiresAtMs = QDateTime::currentMSecsSinceEpoch() + ttlMs;
    entries_.insert(videoId, e);
}

QString StreamCache::tryGet(const QString& videoId) const {
    if (videoId.isEmpty()) return QString();
    QMutexLocker lock(&mutex_);

    auto it = entries_.constFind(videoId);
    if (it == entries_.constEnd()) return QString();

    if (it->expiresAtMs <= QDateTime::currentMSecsSinceEpoch()) {
        // Expired — purge on the way out. We need to release the
        // mutable entry, so we acquire a unique-ptr view instead of
        // returning the URL.
        return QString();
    }
    return it->streamUrl;
}

std::unique_ptr<CachedStream> StreamCache::tryGetFull(const QString& videoId) const {
    if (videoId.isEmpty()) return nullptr;
    QMutexLocker lock(&mutex_);

    auto it = entries_.constFind(videoId);
    if (it == entries_.constEnd()) return nullptr;

    if (it->expiresAtMs <= QDateTime::currentMSecsSinceEpoch()) {
        return nullptr;
    }
    return std::make_unique<CachedStream>(*it);
}

void StreamCache::remove(const QString& videoId) {
    QMutexLocker lock(&mutex_);
    entries_.remove(videoId);
}

void StreamCache::clear() {
    QMutexLocker lock(&mutex_);
    entries_.clear();
}

void StreamCache::purgeExpired() {
    QMutexLocker lock(&mutex_);
    const qint64 now = QDateTime::currentMSecsSinceEpoch();
    for (auto it = entries_.begin(); it != entries_.end(); ) {
        if (it->expiresAtMs <= now) it = entries_.erase(it);
        else                         ++it;
    }
}

int StreamCache::count() const {
    QMutexLocker lock(&mutex_);
    return entries_.size();
}

bool StreamCache::isEmpty() const {
    QMutexLocker lock(&mutex_);
    return entries_.isEmpty();
}

} // namespace mf::core::sources::youtube

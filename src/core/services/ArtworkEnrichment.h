// ArtworkEnrichment.h
// High-level service that combines ImageCache + ColorExtractor to give
// callers a single API for "what color should this cover art seed into
// the theme?" Two flavours:
//   - sync seedFor(track)  — if the image is already on disk/memory
//   - async ensureSeedFor(track, cb)  — downloads on miss, then extracts
//
// Tracks can be passed in two ways:
//   1. By URL (QString/QUrl): the image cache is keyed on the URL.
//   2. By MusicFile (full struct): uses coverUrl() if present, otherwise
//      falls back to a hash of title+album so local files still get a
//      stable (possibly null) seed.

#pragma once

#include "../models/MusicFile.h"

#include <QColor>
#include <QMutex>
#include <QObject>
#include <QString>
#include <QUrl>

#include <functional>
#include <memory>

namespace mf::core::services {
    class ImageCache;
}

namespace mf::core::services {

class ArtworkEnrichment : public QObject {
    Q_OBJECT
public:
    using SeedCallback = std::function<void(QColor seed)>;

    explicit ArtworkEnrichment(QObject* parent = nullptr);
    ArtworkEnrichment(ImageCache* cache, QObject* parent = nullptr);
    ~ArtworkEnrichment() override;

    // Returns the URL the cache would use for the given track. Never empty.
    static QUrl urlFor(const mf::core::models::MusicFile& track);

    // Synchronous seed lookup. Returns invalid QColor if the image isn't
    // cached (use ensureSeedFor for that case).
    QColor seedFor(const QUrl& url) const;

    // Same, but with a MusicFile.
    QColor seedFor(const mf::core::models::MusicFile& track) const;

    // Async. If the image is cached, the callback runs synchronously.
    // Otherwise it downloads, extracts, and calls back.
    void ensureSeedFor(const QUrl& url, SeedCallback cb);
    void ensureSeedFor(const mf::core::models::MusicFile& track, SeedCallback cb);

    // Cached seed-color (in-process) keyed by URL. Avoids re-extracting
    // when the same cover art is shown repeatedly.
    QColor cachedSeed(const QUrl& url) const;
    void   rememberSeed(const QUrl& url, QColor seed);

    void   setImageCache(ImageCache* cache);
    ImageCache* imageCache() const { return cache_; }

signals:
    void seedReady(QString url, QColor seed);
    void seedFailed(QString url, QString reason);

private:
    void onImageBytes(const QUrl& url, SeedCallback cb,
                      QByteArray bytes, QString contentType, QString err);

    ImageCache*                cache_ = nullptr;
    mutable QMutex mutex_;
    mutable QHash<QUrl, QColor> seedByUrl_;
};

} // namespace mf::core::services

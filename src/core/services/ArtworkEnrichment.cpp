// ArtworkEnrichment.cpp

#include "ArtworkEnrichment.h"

#include "ColorExtractor.h"
#include "ImageCache.h"

#include <QCryptographicHash>
#include <QImage>
#include <QImageReader>
#include <QMutexLocker>
#include <QSet>
#include <QtDebug>

namespace mf::core::services {

namespace {
// Some MusicFile covers are already on disk. We hash the local path so
// the cache key is stable but doesn't depend on a remote URL being live.
QString stableKeyForLocal(const QString& path) {
    return QStringLiteral("local://") +
           QCryptographicHash::hash(path.toUtf8(),
                                    QCryptographicHash::Sha1).toHex();
}
} // namespace

ArtworkEnrichment::ArtworkEnrichment(QObject* parent)
    : ArtworkEnrichment(nullptr, parent) {}

ArtworkEnrichment::ArtworkEnrichment(ImageCache* cache, QObject* parent)
    : QObject(parent), cache_(cache) {}

ArtworkEnrichment::~ArtworkEnrichment() = default;

void ArtworkEnrichment::setImageCache(ImageCache* cache) {
    cache_ = cache;
}

QUrl ArtworkEnrichment::urlFor(const mf::core::models::MusicFile& track) {
    // Remote cover URL takes priority (YouTube, Subsonic, etc.).
    if (!track.coverUrl().isEmpty() &&
        QUrl(track.coverUrl()).scheme().startsWith(QStringLiteral("http"))) {
        return QUrl(track.coverUrl());
    }
    // Local file path — never fetched via HttpClient; key only.
    if (!track.coverPath().isEmpty()) {
        return QUrl(stableKeyForLocal(track.coverPath()));
    }
    // Synthesise a stable key from title + album so local files with no
    // embedded artwork still get a unique cache key.
    const QByteArray h = QCryptographicHash::hash(
        (track.title() + QStringLiteral("|") + track.album()).toUtf8(),
        QCryptographicHash::Sha1).toHex();
    return QUrl(QStringLiteral("synth://") + QString::fromLatin1(h));
}

QColor ArtworkEnrichment::seedFor(const QUrl& url) const {
    {
        QMutexLocker lock(&mutex_);
        if (seedByUrl_.contains(url)) return seedByUrl_[url];
    }
    if (!cache_) return {};
    QString contentType;
    const QByteArray bytes = cache_->peek(url, &contentType);
    if (bytes.isEmpty()) return {};
    QImage img;
    img.loadFromData(bytes);
    if (img.isNull()) return {};
    QColor c = ColorExtractor::seedColor(img);
    if (c.isValid()) {
        QMutexLocker lock(&mutex_);
        seedByUrl_.insert(url, c);
    }
    return c;
}

QColor ArtworkEnrichment::seedFor(const mf::core::models::MusicFile& track) const {
    return seedFor(urlFor(track));
}

void ArtworkEnrichment::ensureSeedFor(const QUrl& url, SeedCallback cb) {
    if (cb == nullptr) return;
    QColor already = seedFor(url);
    if (already.isValid()) { cb(already); return; }
    if (!cache_) { cb({}); return; }
    cache_->get(url, [this, url, cb](QByteArray bytes,
                                     QString contentType, QString err) {
        onImageBytes(url, cb, std::move(bytes),
                     std::move(contentType), std::move(err));
    });
}

void ArtworkEnrichment::ensureSeedFor(const mf::core::models::MusicFile& track,
                                      SeedCallback cb) {
    ensureSeedFor(urlFor(track), std::move(cb));
}

QColor ArtworkEnrichment::cachedSeed(const QUrl& url) const {
    QMutexLocker lock(&mutex_);
    return seedByUrl_.value(url, QColor());
}

void ArtworkEnrichment::rememberSeed(const QUrl& url, QColor seed) {
    if (!seed.isValid()) return;
    QMutexLocker lock(&mutex_);
    seedByUrl_.insert(url, seed);
}

void ArtworkEnrichment::onImageBytes(const QUrl& url, SeedCallback cb,
                                     QByteArray bytes, QString /*contentType*/,
                                     QString err) {
    if (!err.isEmpty() || bytes.isEmpty()) {
        cb({});
        emit seedFailed(url.toString(), err);
        return;
    }
    QImage img;
    img.loadFromData(bytes);
    if (img.isNull()) {
        cb({});
        emit seedFailed(url.toString(), QStringLiteral("decode_failed"));
        return;
    }
    const QColor seed = ColorExtractor::seedColor(img);
    if (seed.isValid()) {
        QMutexLocker lock(&mutex_);
        seedByUrl_.insert(url, seed);
    }
    cb(seed);
    if (seed.isValid()) emit seedReady(url.toString(), seed);
}

} // namespace mf::core::services

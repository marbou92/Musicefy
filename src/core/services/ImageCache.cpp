// ImageCache.cpp

#include "ImageCache.h"

#include "../sources/HttpClient.h"

#include <QCryptographicHash>
#include <QDateTime>
#include <QDir>
#include <QFile>
#include <QFileInfo>
#include <QJsonDocument>
#include <QJsonObject>
#include <QMutexLocker>
#include <QSaveFile>
#include <QtDebug>

namespace mf::core::services {

namespace {
QString defaultCacheRoot() {
    const QString base = QDir::homePath() + QStringLiteral("/.cache/musicefy/image-cache");
    return base;
}
} // namespace

ImageCache::ImageCache(QObject* parent)
    : ImageCache(nullptr, defaultCacheRoot(), 32,
                 60 * 60 * 24 * 30, 200LL * 1024 * 1024, parent) {}

ImageCache::ImageCache(mf::core::sources::HttpClient* http,
                       QString cacheDir,
                       int lruCapacity,
                       int defaultTtlSeconds,
                       qint64 maxDiskBytes,
                       QObject* parent)
    : QObject(parent)
    , http_(http)
    , cacheDir_(std::move(cacheDir))
    , lruCapacity_(lruCapacity)
    , defaultTtlSec_(defaultTtlSeconds)
    , maxDiskBytes_(maxDiskBytes)
{
    QDir().mkpath(cacheDir_);

    // Hydrate diskCount_ and diskBytes_ from the directory.
    QDir d(cacheDir_);
    const auto entries = d.entryInfoList(
        {QStringLiteral("*.bin")}, QDir::Files | QDir::NoDotAndDotDot);
    for (const QFileInfo& fi : entries) {
        diskBytes_ += fi.size();
        ++diskCount_;
    }
    evictExpired();
    enforceDiskLimit();
}

void ImageCache::setCacheDirectory(const QString& dir) {
    QMutexLocker lock(&mutex_);
    cacheDir_ = dir;
    QDir().mkpath(cacheDir_);
    disk_.clear();
    lruOrder_.clear();
    diskBytes_ = diskCount_ = 0;
}

QString ImageCache::cacheKey(const QUrl& url) const {
    return QCryptographicHash::hash(url.toString().toUtf8(),
                                    QCryptographicHash::Sha1).toHex();
}

QString ImageCache::diskPathFor(const QString& key, bool meta) const {
    const QString name = key + (meta ? QStringLiteral(".meta")
                                     : QStringLiteral(".bin"));
    return QDir(cacheDir_).filePath(name);
}

void ImageCache::loadFromDisk(const QString& key, DiskFile& out) const {
    QFile bin(diskPathFor(key, /*meta=*/false));
    if (!bin.open(QIODevice::ReadOnly)) return;
    out.bytes = bin.readAll();
    bin.close();

    QFile meta(diskPathFor(key, /*meta=*/true));
    if (meta.open(QIODevice::ReadOnly)) {
        const QByteArray json = meta.readAll();
        meta.close();
        const QJsonDocument doc = QJsonDocument::fromJson(json);
        if (doc.isObject()) {
            const QJsonObject o = doc.object();
            out.contentType = o.value(QStringLiteral("ct")).toString();
            out.expiresAtMs = static_cast<qint64>(
                o.value(QStringLiteral("exp")).toDouble(0));
        }
    }
}

void ImageCache::saveToDisk(const QString& key, const DiskFile& file) {
    QDir().mkpath(cacheDir_);
    const QString binPath  = diskPathFor(key, /*meta=*/false);
    const QString metaPath = diskPathFor(key, /*meta=*/true);

    QSaveFile bin(binPath);
    if (bin.open(QIODevice::WriteOnly)) {
        bin.write(file.bytes);
        bin.commit();
    }
    QJsonObject o;
    o.insert(QStringLiteral("ct"),  file.contentType);
    o.insert(QStringLiteral("exp"), static_cast<double>(file.expiresAtMs));
    QSaveFile meta(metaPath);
    if (meta.open(QIODevice::WriteOnly)) {
        meta.write(QJsonDocument(o).toJson(QJsonDocument::Compact));
        meta.commit();
    }
}

void ImageCache::touchLru(const QString& key) {
    lruOrder_.removeAll(key);
    lruOrder_.append(key);
    while (lruOrder_.size() > lruCapacity_) {
        const QString old = lruOrder_.takeFirst();
        auto it = disk_.find(old);
        if (it != disk_.end()) {
            diskBytes_ -= it->bytes.size();
            --diskCount_;
            QFile::remove(diskPathFor(old, false));
            QFile::remove(diskPathFor(old, true));
            disk_.erase(it);
            emit evicted(old, QStringLiteral("lru"));
        }
    }
}

void ImageCache::evictExpired() {
    if (cacheDir_.isEmpty()) return;
    QDir d(cacheDir_);
    const auto entries = d.entryInfoList(
        {QStringLiteral("*.meta")}, QDir::Files | QDir::NoDotAndDotDot);
    const qint64 now = QDateTime::currentMSecsSinceEpoch();
    for (const QFileInfo& fi : entries) {
        const QString key = fi.completeBaseName(); // strips .meta
        QFile mf(fi.absoluteFilePath());
        if (!mf.open(QIODevice::ReadOnly)) continue;
        const QJsonDocument doc = QJsonDocument::fromJson(mf.readAll());
        mf.close();
        if (!doc.isObject()) continue;
        const qint64 exp = static_cast<qint64>(
            doc.object().value(QStringLiteral("exp")).toDouble(0));
        if (exp > 0 && now >= exp) {
            QFile::remove(diskPathFor(key, false));
            QFile::remove(diskPathFor(key, true));
            --diskCount_;
            emit evicted(key, QStringLiteral("expired"));
        }
    }
}

void ImageCache::enforceDiskLimit() {
    if (maxDiskBytes_ <= 0 || diskBytes_ <= maxDiskBytes_) return;
    QDir d(cacheDir_);
    const auto entries = d.entryInfoList(
        {QStringLiteral("*.bin")}, QDir::Files | QDir::NoDotAndDotDot);
    // sort by last-modified ascending (oldest first)
    QList<QFileInfo> sorted = entries;
    std::sort(sorted.begin(), sorted.end(),
              [](const QFileInfo& a, const QFileInfo& b) {
                  return a.lastModified() < b.lastModified();
              });
    for (const QFileInfo& fi : sorted) {
        if (diskBytes_ <= maxDiskBytes_) break;
        const QString key = fi.completeBaseName();
        QFile::remove(diskPathFor(key, false));
        QFile::remove(diskPathFor(key, true));
        diskBytes_ -= fi.size();
        --diskCount_;
        emit evicted(key, QStringLiteral("oversize"));
    }
}

bool ImageCache::contains(const QUrl& url) const {
    QMutexLocker lock(&mutex_);
    return disk_.contains(cacheKey(url));
}

QByteArray ImageCache::peek(const QUrl& url, QString* contentTypeOut) const {
    QMutexLocker lock(&mutex_);
    const QString key = cacheKey(url);
    auto it = disk_.constFind(key);
    if (it == disk_.constEnd()) {
        DiskFile file;
        loadFromDisk(key, file);
        if (file.bytes.isEmpty()) return {};
        if (file.expiresAtMs > 0 &&
            QDateTime::currentMSecsSinceEpoch() >= file.expiresAtMs) {
            return {}; // expired
        }
        if (contentTypeOut) *contentTypeOut = file.contentType;
        return file.bytes;
    }
    if (it->expiresAtMs > 0 &&
        QDateTime::currentMSecsSinceEpoch() >= it->expiresAtMs) {
        return {}; // expired
    }
    if (contentTypeOut) *contentTypeOut = it->contentType;
    return it->bytes;
}

void ImageCache::get(const QUrl& url, BytesCallback cb) {
    if (cb == nullptr) return;
    {
        QMutexLocker lock(&mutex_);
        const QString key = cacheKey(url);
        auto it = disk_.constFind(key);
        if (it != disk_.constEnd()) {
            const qint64 now = QDateTime::currentMSecsSinceEpoch();
            if (it->expiresAtMs == 0 || now < it->expiresAtMs) {
                touchLru(key);
                QByteArray bytes = it->bytes;
                QString ct = it->contentType;
                // copy cb so we can release the lock
                lock.unlock();
                cb(std::move(bytes), std::move(ct), QString());
                return;
            }
        }
        // Try disk (not yet hydrated into memory)
        DiskFile file;
        loadFromDisk(key, file);
        if (!file.bytes.isEmpty() &&
            (file.expiresAtMs == 0 ||
             QDateTime::currentMSecsSinceEpoch() < file.expiresAtMs)) {
            disk_.insert(key, file);
            touchLru(key);
            QByteArray bytes = file.bytes;
            QString ct = file.contentType;
            lock.unlock();
            cb(std::move(bytes), std::move(ct), QString());
            return;
        }
    }

    // Cache miss: download via HttpClient (if any) or fail.
    if (!http_) {
        cb({}, {}, QStringLiteral("image_cache: no HttpClient configured"));
        return;
    }
    mf::core::sources::HttpRequest req;
    req.url = url.toString();
    req.method = QByteArrayLiteral("GET");
    req.timeoutMs = 20000;
    auto tag = http_->get(req,
        [this, url, cb](mf::core::sources::HttpResponse resp) {
            if (!resp.ok()) {
                cb({}, {},
                   QStringLiteral("image_cache: HTTP %1: %2")
                       .arg(resp.statusCode)
                       .arg(resp.errorMessage));
                return;
            }
            put(url, resp.body,
                resp.headers.value(QStringLiteral("Content-Type")));
            cb(resp.body,
               resp.headers.value(QStringLiteral("Content-Type")),
               QString());
        });
}

void ImageCache::put(const QUrl& url,
                     const QByteArray& bytes,
                     const QString& contentType) {
    if (bytes.isEmpty()) return;
    QMutexLocker lock(&mutex_);
    const QString key = cacheKey(url);
    DiskFile file;
    file.bytes = bytes;
    file.contentType = contentType;
    file.expiresAtMs = defaultTtlSec_ > 0
        ? QDateTime::currentMSecsSinceEpoch() +
              qint64(defaultTtlSec_) * 1000LL
        : 0;


    auto it = disk_.find(key);
    if (it != disk_.end()) {
        diskBytes_ -= it->bytes.size();
    }
    disk_.insert(key, file);
    diskBytes_ += bytes.size();
    if (!disk_.contains(key)) ++diskCount_;
    touchLru(key);
    saveToDisk(key, file);
    lock.unlock();
    enforceDiskLimit();
    emit stored(url.toString());
}

void ImageCache::remove(const QUrl& url) {
    QMutexLocker lock(&mutex_);
    const QString key = cacheKey(url);
    auto it = disk_.find(key);
    if (it == disk_.end()) return;
    diskBytes_ -= it->bytes.size();
    --diskCount_;
    QFile::remove(diskPathFor(key, false));
    QFile::remove(diskPathFor(key, true));
    disk_.erase(it);
    lruOrder_.removeAll(key);
    emit evicted(key, QStringLiteral("removed"));
}

void ImageCache::clear() {
    QMutexLocker lock(&mutex_);
    QDir d(cacheDir_);
    for (const QFileInfo& fi : d.entryInfoList(
             QDir::Files | QDir::NoDotAndDotDot)) {
        QFile::remove(fi.absoluteFilePath());
    }
    disk_.clear();
    lruOrder_.clear();
    diskBytes_ = diskCount_ = 0;
}

ImageCache::~ImageCache() = default;

} // namespace mf::core::services

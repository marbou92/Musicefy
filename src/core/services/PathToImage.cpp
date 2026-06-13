// PathToImage.cpp

#include "PathToImage.h"

#include "ImageCache.h"

#include <QFile>
#include <QFileInfo>

namespace mf::core::services {

PathToImage::PathToImage(ImageCache* cache) : cache_(cache) {}

QImage PathToImage::load(const QString& pathOrUrl) const {
    if (pathOrUrl.isEmpty()) return {};

    // Local filesystem path: read directly. QImage::load handles all
    // supported formats via the Qt image plugin set.
    if (!pathOrUrl.contains(QStringLiteral("://"))) {
        if (!QFile::exists(pathOrUrl)) return {};
        QImage img(pathOrUrl);
        return img;
    }

    // Anything with a scheme goes through the cache.
    return load(QUrl(pathOrUrl));
}

QImage PathToImage::load(const QUrl& url) const {
    if (!url.isValid() || url.scheme().isEmpty()) return {};
    const QString scheme = url.scheme();

    // Stable keys are only meaningful if someone has put bytes under
    // that key. The cache lookup handles that uniformly.
    if (scheme == QStringLiteral("synth") ||
        scheme == QStringLiteral("local")) {
        if (!cache_) return {};
        QString ct;
        const QByteArray bytes = cache_->peek(url, &ct);
        if (bytes.isEmpty()) return {};
        QImage img;
        img.loadFromData(bytes);
        return img;
    }

    if (scheme == QStringLiteral("file")) {
        const QString local = url.toLocalFile();
        if (local.isEmpty() || !QFile::exists(local)) return {};
        return QImage(local);
    }

    // http(s) — cache-only, no network.
    if (scheme == QStringLiteral("http") ||
        scheme == QStringLiteral("https")) {
        if (!cache_) return {};
        QString ct;
        const QByteArray bytes = cache_->peek(url, &ct);
        if (bytes.isEmpty()) return {};
        QImage img;
        img.loadFromData(bytes);
        return img;
    }

    return {};
}

bool PathToImage::isAvailable(const QString& pathOrUrl) const {
    return !load(pathOrUrl).isNull();
}

bool PathToImage::isAvailable(const QUrl& url) const {
    return !load(url).isNull();
}

} // namespace mf::core::services

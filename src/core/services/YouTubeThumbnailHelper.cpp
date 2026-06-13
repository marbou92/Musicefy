// YouTubeThumbnailHelper.cpp
// See header for design notes.

#include "YouTubeThumbnailHelper.h"
#include "ImageCache.h"

#include <QJsonArray>
#include <QJsonDocument>
#include <QJsonObject>
#include <QUrl>
#include <QUrlQuery>

namespace mf::core::services {

namespace {
QString qualitySuffix(YouTubeThumbnailHelper::ThumbnailQuality q) {
    switch (q) {
    case YouTubeThumbnailHelper::ThumbnailQuality::Default:  return QStringLiteral("default");
    case YouTubeThumbnailHelper::ThumbnailQuality::Medium:   return QStringLiteral("mqdefault");
    case YouTubeThumbnailHelper::ThumbnailQuality::High:     return QStringLiteral("hqdefault");
    case YouTubeThumbnailHelper::ThumbnailQuality::Standard: return QStringLiteral("sddefault");
    case YouTubeThumbnailHelper::ThumbnailQuality::MaxRes:   return QStringLiteral("maxresdefault");
    }
    return QStringLiteral("hqdefault");
}

int qualityToInt(YouTubeThumbnailHelper::ThumbnailQuality q) {
    return static_cast<int>(q);
}
} // namespace

YouTubeThumbnailHelper::YouTubeThumbnailHelper(QObject* parent)
    : YouTubeThumbnailHelper(nullptr, parent) {}

YouTubeThumbnailHelper::YouTubeThumbnailHelper(ImageCache* cache, QObject* parent)
    : QObject(parent), cache_(cache) {}

YouTubeThumbnailHelper::~YouTubeThumbnailHelper() = default;

void YouTubeThumbnailHelper::setImageCache(ImageCache* cache) {
    cache_ = cache;
}

QUrl YouTubeThumbnailHelper::thumbnailUrl(const QString& videoId,
                                          ThumbnailQuality quality) {
    if (videoId.isEmpty()) return QUrl();
    return QUrl(QStringLiteral("https://%1%2%3/%4.jpg")
                    .arg(QString::fromLatin1(kHost))
                    .arg(QString::fromLatin1(kPath))
                    .arg(videoId)
                    .arg(qualitySuffix(quality)));
}

QUrl YouTubeThumbnailHelper::albumArtUrl(const QString& browseId,
                                         ThumbnailQuality quality) {
    if (browseId.isEmpty()) return QUrl();
    // YouTube Music album art follows: https://lh3.googleusercontent.com/...
    // We use the standard YouTube thumbnail path as a fallback
    // since YouTube Music doesn't expose a simple CDN thumbnail URL.
    // The browseId is used with a ytimg path.
    return QUrl(QStringLiteral("https://%1%2%3/%4.jpg")
                    .arg(QString::fromLatin1(kHost))
                    .arg(QString::fromLatin1(kPath))
                    .arg(browseId)
                    .arg(qualitySuffix(quality)));
}

QString YouTubeThumbnailHelper::extractVideoId(const QString& url) {
    if (url.isEmpty()) return QString();

    // Standard: https://www.youtube.com/watch?v=VIDEO_ID
    QUrl parsed(url);
    if (parsed.host().contains(QStringLiteral("youtube")) ||
        parsed.host().contains(QStringLiteral("youtu.be"))) {
        QUrlQuery q(parsed);
        if (q.hasQueryItem(QStringLiteral("v"))) {
            return q.queryItemValue(QStringLiteral("v"));
        }
        // Short form: https://youtu.be/VIDEO_ID
        QString path = parsed.path();
        if (!path.isEmpty()) {
            path = path.mid(1); // strip leading /
            if (!path.contains('/')) return path;
        }
    }
    // Embedded: https://www.youtube.com/embed/VIDEO_ID
    // Shorts: https://www.youtube.com/shorts/VIDEO_ID
    QString path = parsed.path();
    static const QStringList prefixes = {
        QStringLiteral("/embed/"),
        QStringLiteral("/shorts/"),
        QStringLiteral("/v/")
    };
    for (const auto& prefix : prefixes) {
        if (path.startsWith(prefix)) {
            QString id = path.mid(prefix.length());
            int amp = id.indexOf('?');
            if (amp >= 0) id = id.left(amp);
            return id;
        }
    }

    return QString();
}

bool YouTubeThumbnailHelper::isYouTubeThumbnail(const QUrl& url) {
    return url.host() == QString::fromLatin1(kHost) &&
           url.path().contains(QString::fromLatin1(kPath));
}

void YouTubeThumbnailHelper::fetchThumbnail(const QString& videoId,
                                            ThumbnailQuality quality,
                                            ThumbnailCallback callback) {
    if (videoId.isEmpty()) {
        if (callback) callback(QUrl(), false);
        return;
    }

    // Check URL cache first.
    QPair<QString, int> key{videoId, qualityToInt(quality)};
    {
        auto it = urlCache_.find(key);
        if (it != urlCache_.end()) {
            if (callback) callback(it.value(), true);
            return;
        }
    }

    QUrl url = thumbnailUrl(videoId, quality);
    urlCache_.insert(key, url);

    // If we have an image cache, try to download.
    if (cache_) {
        cache_->get(url, [this, videoId, quality, callback, url](QByteArray bytes,
                                                                  QString /*contentType*/,
                                                                  QString err) {
            if (err.isEmpty() && !bytes.isEmpty()) {
                emit thumbnailReady(videoId, url);
                if (callback) callback(url, false);
            } else {
                emit thumbnailFailed(videoId, err);
                if (callback) callback(QUrl(), false);
            }
        });
    } else {
        // No image cache — just return the URL (it will be fetched by
        // the widget's image loading code).
        emit thumbnailReady(videoId, url);
        if (callback) callback(url, false);
    }
}

QUrl YouTubeThumbnailHelper::cachedThumbnail(const QString& videoId,
                                             ThumbnailQuality quality) const {
    QPair<QString, int> key{videoId, qualityToInt(quality)};
    auto it = urlCache_.find(key);
    if (it != urlCache_.end()) return it.value();
    return QUrl();
}

} // namespace mf::core::services

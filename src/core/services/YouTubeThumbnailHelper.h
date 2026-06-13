// YouTubeThumbnailHelper.h
// Utility service for building and fetching YouTube thumbnail URLs.
// YouTube serves thumbnails at predictable URLs based on video ID:
//   https://img.youtube.com/vi/{videoId}/default.jpg   (120x90)
//   https://img.youtube.com/vi/{videoId}/mqdefault.jpg  (320x180)
//   https://img.youtube.com/vi/{videoId}/hqdefault.jpg  (480x360)
//   https://img.youtube.com/vi/{videoId}/sddefault.jpg  (640x480)
//   https://img.youtube.com/vi/{videoId}/maxresdefault.jpg (1920x1080)
//
// This helper also supports YouTube Music album art which follows
// a different URL pattern based on browseId.

#pragma once

#include <QColor>
#include <QHash>
#include <QObject>
#include <QString>
#include <QUrl>

namespace mf::core::services {

class ImageCache;

class YouTubeThumbnailHelper : public QObject {
    Q_OBJECT
public:
    enum class ThumbnailQuality {
        Default,   // 120x90  — list rows
        Medium,    // 320x180 — grid cards
        High,      // 480x360 — detail views
        Standard,  // 640x480 — hero sections
        MaxRes     // 1920x1080 — full-screen backgrounds
    };

    explicit YouTubeThumbnailHelper(QObject* parent = nullptr);
    YouTubeThumbnailHelper(ImageCache* cache, QObject* parent = nullptr);
    ~YouTubeThumbnailHelper() override;

    /// Build a standard YouTube thumbnail URL from a video ID.
    static QUrl thumbnailUrl(const QString& videoId,
                             ThumbnailQuality quality = ThumbnailQuality::High);

    /// Build a YouTube Music album art URL from a browse ID.
    /// YouTube Music uses a different CDN path for album art.
    static QUrl albumArtUrl(const QString& browseId,
                            ThumbnailQuality quality = ThumbnailQuality::High);

    /// Extract the video ID from a full YouTube URL.
    /// Returns empty string if the URL is not a valid YouTube video URL.
    static QString extractVideoId(const QString& url);

    /// Check if a URL is a YouTube thumbnail URL.
    static bool isYouTubeThumbnail(const QUrl& url);

    /// Asynchronously fetch a thumbnail and cache it.
    /// Calls back with the local cache path (or empty on failure).
    using ThumbnailCallback = std::function<void(QUrl thumbnailUrl, bool fromCache)>;
    void fetchThumbnail(const QString& videoId,
                        ThumbnailQuality quality,
                        ThumbnailCallback callback);

    /// Synchronous lookup: returns the cached URL if available.
    QUrl cachedThumbnail(const QString& videoId,
                         ThumbnailQuality quality = ThumbnailQuality::High) const;

    void setImageCache(ImageCache* cache);
    ImageCache* imageCache() const { return cache_; }

signals:
    void thumbnailReady(const QString& videoId, const QUrl& url);
    void thumbnailFailed(const QString& videoId, const QString& reason);

private:
    static constexpr const char* kHost = "img.youtube.com";
    static constexpr const char* kPath = "/vi/";

    ImageCache* cache_ = nullptr;
    mutable QHash<QPair<QString, int>, QUrl> urlCache_;
};

} // namespace mf::core::services

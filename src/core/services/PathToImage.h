// PathToImage.h
// Resolves a cover-art "source" into a QImage. Three accepted forms:
//   1. A local filesystem path    — read directly via QImage(path).
//   2. An http(s):// URL          — looked up synchronously in the
//                                   ImageCache (so no network on the
//                                   UI thread).
//   3. A stable key (synth:// or
//      local:// prefixed)         — looked up in the ImageCache, which
//                                   is a no-op for these keys (caller
//                                   must have stored bytes under the
//                                   same key via ArtworkEnrichment).
//
// Async loading is *not* exposed here; UI code should call
// ArtworkEnrichment::ensureSeedFor or AppContainer::imageCache()->get()
// for network access. PathToImage is the cheap synchronous fast path
// used by the play queue and the player bar.

#pragma once

#include <QImage>
#include <QString>
#include <QUrl>

namespace mf::core::services { class ImageCache; }

namespace mf::core::services {

class PathToImage {
public:
    explicit PathToImage(ImageCache* cache);

    // Synchronous load. Returns a null QImage on miss/error.
    QImage load(const QString& pathOrUrl) const;
    QImage load(const QUrl& url) const;

    // Convenience: was the source already cached (or, for local paths,
    // does the file exist + decode successfully)?
    bool isAvailable(const QString& pathOrUrl) const;
    bool isAvailable(const QUrl& url) const;

    void   setImageCache(ImageCache* cache) { cache_ = cache; }
    ImageCache* imageCache() const { return cache_; }

private:
    ImageCache* cache_ = nullptr;
};

} // namespace mf::core::services

// CoverImage.h
// QLabel subclass that knows how to render a cover-art image from any
// of the supported source kinds (local file path, http(s) URL, or a
// stable local:// / synth:// cache key). Uses PathToImage for the sync
// fast path. Falls back to a generated placeholder (initial letter
// or ♪) when no image is available, with the background hue derived
// from a stable hash of the seed so the same cover always paints the
// same way.

#pragma once

#include <QColor>
#include <QLabel>
#include <QString>
#include <QUrl>

#include <functional>

class QPixmap;

namespace mf::core::services { class ImageCache; class PathToImage; }

namespace mf::app::widgets {

class CoverImage : public QLabel {
    Q_OBJECT
public:
    // Async image loader. Invoked when setSource() is given an http(s)
    // URL. The implementation must call `cb(bytes, contentType, error)`
    // on the GUI thread, either synchronously (cache hit) or after some
    // delay (network miss). An empty `bytes` + non-empty `error` is a
    // miss; an empty `bytes` + empty `error` is also a miss.
    using BytesCallback = std::function<void(QByteArray, QString, QString)>;
    using ImageLoader   = std::function<void(const QUrl&, BytesCallback)>;

    explicit CoverImage(QWidget* parent = nullptr);
    CoverImage(mf::core::services::ImageCache* cache, QWidget* parent = nullptr);
    ~CoverImage() override;

    // Set the image source. Pass an empty QString to clear back to the
    // placeholder. The label re-applies on themeChange + cache updates.
    // For http(s) URLs, the load is async; the placeholder is shown
    // immediately and replaced when the loader's callback fires.
    void setSource(const QString& pathOrUrl, const QString& fallbackText = QString());
    void setSource(const QUrl& url,    const QString& fallbackText = QString());

    // Set just the placeholder text (no image). Useful when the source
    // is unknown and we want to display a stable letter.
    void setPlaceholderText(const QString& text);

    // Re-render the placeholder (called automatically on theme change).
    void refreshPlaceholder();

    // The cache used to resolve URLs. Owned by AppContainer.
    void setImageCache(mf::core::services::ImageCache* cache);
    mf::core::services::ImageCache* imageCache() const { return cache_; }

    // Replace the async image loader. Default uses ImageCache::get().
    // Tests use this to inject a controllable loader (e.g. to delay
    // the callback and exercise cancel-in-flight).
    void setImageLoader(ImageLoader loader) { loader_ = std::move(loader); }
    ImageLoader imageLoader() const         { return loader_; }

    // The hue (0-360) currently used for the placeholder background.
    int placeholderHue() const { return hue_; }

    // True while a load is in flight (callback hasn't fired yet, or
    // has fired but a newer request superseded it). Mostly for tests.
    bool isLoading() const { return pendingRequestId_ != 0; }

    // ── Static helpers (testable without instantiating a widget) ──
    // The hue (0-360) we'd use for a given seed string.
    static int placeholderHueFor(const QString& seed);
    // The background color for a given seed.
    static QColor placeholderColorFor(const QString& seed);
    // Build a fully-rendered placeholder QPixmap (rounded rect, initial
    // letter or ♪). Returns an empty QPixmap if `side <= 0`.
    static QPixmap buildPlaceholder(int side, const QString& seedText,
                                    const QString& sourceText);

protected:
    void resizeEvent(QResizeEvent* e) override;

private:
    void renderPlaceholder();
    void renderFromCache();
    void startAsyncLoad(const QUrl& url, quint64 requestId);
    void applyLoadedImage(const QImage& img, quint64 requestId);

    // The default loader — wired to ImageCache::get().
    static void defaultLoader(mf::core::services::ImageCache* cache,
                              const QUrl& url, BytesCallback cb);

    mf::core::services::ImageCache*   cache_ = nullptr;
    ImageLoader                       loader_;
    QString currentSource_;
    QString currentFallbackText_;
    int     hue_ = 200;
    // Monotonically-increasing token. Each setSource() bumps it, so
    // an in-flight callback can detect it's been superseded and drop
    // the result. 0 == "no request in flight".
    quint64 nextRequestId_     = 0;
    quint64 pendingRequestId_  = 0;
};

} // namespace mf::app::widgets

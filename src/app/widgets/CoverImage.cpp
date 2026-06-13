// CoverImage.cpp

#include "CoverImage.h"

#include "../core/services/ImageCache.h"
#include "../core/services/PathToImage.h"

#include <QBuffer>
#include <QFont>
#include <QFontMetrics>
#include <QImage>
#include <QImageReader>
#include <QPainter>
#include <QPainterPath>
#include <QPointer>
#include <QResizeEvent>

namespace mf::app::widgets {

using mf::core::services::PathToImage;

CoverImage::CoverImage(QWidget* parent) : QLabel(parent) {
    setAlignment(Qt::AlignCenter);
    setMinimumSize(48, 48);
    // No cache → no async path. setSource() will fall through to
    // the sync loader which always returns null for URLs, so the
    // placeholder stays. That's the right behaviour when there's
    // no image source at all.
}

CoverImage::CoverImage(mf::core::services::ImageCache* cache, QWidget* parent)
    : QLabel(parent), cache_(cache) {
    setAlignment(Qt::AlignCenter);
    setMinimumSize(48, 48);
    // Default loader: ImageCache::get(). ImageCache callbacks fire on
    // the thread that constructed the HttpClient (the GUI thread in
    // production via AppContainer), so no thread-hopping needed.
    if (cache_) {
        loader_ = [cache](const QUrl& url, BytesCallback cb) {
            cache->get(url, std::move(cb));
        };
    }
}

CoverImage::~CoverImage() {
    // Mark every in-flight request as superseded. Captured lambdas
    // check pendingRequestId_ before applying results and will
    // become no-ops.
    pendingRequestId_ = 0;
    nextRequestId_    = 0;
}

void CoverImage::setImageCache(mf::core::services::ImageCache* cache) {
    cache_ = cache;
    if (cache_ && !loader_) {
        loader_ = [cache](const QUrl& url, BytesCallback cb) {
            cache->get(url, std::move(cb));
        };
    }
}

void CoverImage::setSource(const QString& pathOrUrl,
                           const QString& fallbackText) {
    currentSource_       = pathOrUrl;
    currentFallbackText_ = fallbackText;
    hue_ = qHash(pathOrUrl.isEmpty() ? fallbackText : pathOrUrl) % 360;

    // Decide sync vs. async. URLs get the async path so a slow
    // network response doesn't freeze the UI. Everything else
    // (local files, synth:// / local:// cache keys) is read
    // synchronously — PathToImage already handles those fast.
    const QUrl u(pathOrUrl);
    const bool isHttp = u.isValid() &&
        (u.scheme() == QStringLiteral("http") ||
         u.scheme() == QStringLiteral("https"));

    if (isHttp && loader_) {
        // Bump the request token: any prior in-flight callback will
        // see the mismatch and drop its result.
        const quint64 rid = ++nextRequestId_;
        pendingRequestId_ = rid;
        // Show the placeholder immediately so the user sees
        // something the moment the source changes — the real
        // image replaces it when the callback fires.
        renderPlaceholder();
        startAsyncLoad(u, rid);
        return;
    }

    // Non-URL or no loader: synchronous fast path.
    pendingRequestId_ = 0;
    renderFromCache();
}

void CoverImage::setSource(const QUrl& url, const QString& fallbackText) {
    setSource(url.toString(), fallbackText);
}

void CoverImage::setPlaceholderText(const QString& text) {
    currentFallbackText_ = text;
    hue_ = qHash(text) % 360;
    pendingRequestId_ = 0;
    renderFromCache();
}

void CoverImage::refreshPlaceholder() {
    renderFromCache();
}

void CoverImage::resizeEvent(QResizeEvent* e) {
    QLabel::resizeEvent(e);
    // Re-render at the new size so the pixmap stays crisp. If a
    // load is in flight, the placeholder at the new size will be
    // replaced when the callback fires.
    renderFromCache();
}

void CoverImage::startAsyncLoad(const QUrl& url, quint64 requestId) {
    QPointer<CoverImage> self(this);
    loader_(url, [self, requestId](QByteArray bytes,
                                   QString contentType,
                                   QString error) {
        // The widget may have been destroyed (or the request
        // superseded by a newer setSource) between dispatch and
        // callback. In both cases, drop the result.
        if (!self) return;
        if (self->pendingRequestId_ != requestId) return;

        if (bytes.isEmpty() || !error.isEmpty()) {
            // Miss / error — keep the placeholder.
            self->pendingRequestId_ = 0;
            return;
        }

        QImage img;
        if (!img.loadFromData(bytes)) {
            self->pendingRequestId_ = 0;
            return;
        }
        self->applyLoadedImage(img, requestId);
    });
}

void CoverImage::applyLoadedImage(const QImage& img, quint64 requestId) {
    if (pendingRequestId_ != requestId) return; // superseded
    pendingRequestId_ = 0;
    if (img.isNull()) {
        renderFromCache();
        return;
    }
    const int side = qMin(width(), height());
    if (side <= 0) return; // not laid out yet; resizeEvent will retry
    QPixmap pm = QPixmap::fromImage(img).scaled(
        side, side, Qt::KeepAspectRatioByExpanding, Qt::SmoothTransformation);
    QRect cr = pm.rect();
    if (cr.width() > side || cr.height() > side) {
        cr.setX((cr.width()  - side) / 2);
        cr.setY((cr.height() - side) / 2);
        cr.setSize(QSize(side, side));
        pm = pm.copy(cr);
    }
    setPixmap(pm);
}

void CoverImage::renderPlaceholder() {
    setPixmap(buildPlaceholder(qMin(width(), height()),
                                currentFallbackText_,
                                currentSource_));
}

void CoverImage::renderFromCache() {
    if (currentSource_.isEmpty()) {
        setPixmap(buildPlaceholder(qMin(width(), height()),
                                    currentFallbackText_,
                                    currentSource_));
        return;
    }
    PathToImage p2i(cache_);
    QImage img = p2i.load(currentSource_);
    if (img.isNull()) {
        setPixmap(buildPlaceholder(qMin(width(), height()),
                                    currentFallbackText_,
                                    currentSource_));
        return;
    }
    const int side = qMin(width(), height());
    QPixmap pm = QPixmap::fromImage(img).scaled(
        side, side, Qt::KeepAspectRatioByExpanding, Qt::SmoothTransformation);
    // Center-crop the scaled pixmap.
    QRect cr = pm.rect();
    if (cr.width() > side || cr.height() > side) {
        cr.setX((cr.width()  - side) / 2);
        cr.setY((cr.height() - side) / 2);
        cr.setSize(QSize(side, side));
        pm = pm.copy(cr);
    }
    setPixmap(pm);
}

int CoverImage::placeholderHueFor(const QString& seed) {
    return qHash(seed.isEmpty() ? QStringLiteral("?") : seed) % 360;
}

QColor CoverImage::placeholderColorFor(const QString& seed) {
    QColor c;
    c.setHsv(placeholderHueFor(seed), 160, 220);
    return c;
}

QPixmap CoverImage::buildPlaceholder(int side,
                                     const QString& seedText,
                                     const QString& sourceText) {
    if (side <= 0) return QPixmap();
    QPixmap pm(side, side);
    pm.fill(Qt::transparent);

    QPainter p(&pm);
    p.setRenderHint(QPainter::Antialiasing, true);

    const QString effectiveSeed = sourceText.isEmpty() ? seedText : sourceText;
    QColor bg = placeholderColorFor(effectiveSeed);
    QColor fg = QColor(255, 255, 255, 230);

    QPainterPath pp;
    pp.addRoundedRect(QRectF(0, 0, side, side), 8.0, 8.0);
    p.fillPath(pp, bg);

    QString letter = seedText;
    if (letter.isEmpty()) letter = QStringLiteral("\u266B");
    if (letter.size() > 1) letter = letter.left(1).toUpper();

    QFont f = p.font();
    f.setPointSize(int(side * 0.45));
    f.setBold(true);
    p.setFont(f);
    p.setPen(fg);
    p.drawText(pm.rect(), Qt::AlignCenter, letter);

    p.end();
    return pm;
}

} // namespace mf::app::widgets

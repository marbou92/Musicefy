// testcoverimage.cpp
// Unit tests for the CoverImage placeholder helpers. The widget itself
// is a QWidget and is exercised manually, but its placeholder-color /
// placeholder-pixmap logic is exposed as static methods so it can be
// tested headlessly under the "offscreen" QPA.
//
// The QPixmap static needs a QGuiApplication (QPixmap is a paint
// device), so we set up our own main rather than QTEST_GUILESS_MAIN.

#include <QtTest/QtTest>
#include <QBuffer>
#include <QByteArray>
#include <QColor>
#include <QGuiApplication>
#include <QImage>
#include <QList>
#include <QPixmap>
#include <QSet>
#include <QString>
#include <QUrl>

#include "widgets/CoverImage.h"

using mf::app::widgets::CoverImage;

class TestCoverImage : public QObject {
    Q_OBJECT

private slots:
    void initTestCase() {
        // Sanity: the offscreen QPA lets us allocate paint devices
        // without a real display.
        QVERIFY(QGuiApplication::instance() != nullptr);
    }

    void placeholderHue_deterministic() {
        // Same seed → same hue.
        const int a = CoverImage::placeholderHueFor(QStringLiteral("Hello"));
        const int b = CoverImage::placeholderHueFor(QStringLiteral("Hello"));
        QCOMPARE(a, b);
        // Range: 0..359.
        QVERIFY(a >= 0);
        QVERIFY(a < 360);
    }

    void placeholderHue_emptySeedIsStable() {
        const int a = CoverImage::placeholderHueFor(QString());
        const int b = CoverImage::placeholderHueFor(QString());
        QCOMPARE(a, b);
        QVERIFY(a >= 0 && a < 360);
    }

    void placeholderHue_differentSeedsSpreadOver360() {
        // Sample 16 distinct seeds and check that the hue distribution
        // is non-degenerate (at least 8 distinct hues). This is a soft
        // assertion: a perfect 360-bucket hash would give 16 distinct
        // values, and even a poor mod-360 spread would still spread
        // out for 16 different seeds. Anything less than 8 distinct
        // hues would indicate the hash is broken.
        QSet<int> hues;
        for (int i = 0; i < 16; ++i) {
            hues.insert(CoverImage::placeholderHueFor(
                QStringLiteral("seed_%1").arg(i)));
        }
        QVERIFY2(hues.size() >= 8,
                 qPrintable(QStringLiteral("expected at least 8 distinct hues, got %1")
                                  .arg(hues.size())));
    }

    void placeholderColor_isHsvDerived() {
        // The constructor pins (S, V) = (160, 220). The hue should be
        // in 0..359 and the color should be valid + opaque.
        const QColor c = CoverImage::placeholderColorFor(
            QStringLiteral("Artist Name"));
        QVERIFY(c.isValid());
        QCOMPARE(c.alpha(), 255);
        // 160/255 ≈ 0.627, 220/255 ≈ 0.863.
        QVERIFY(qAbs(c.hsvSaturationF() - 160.0 / 255.0) < 0.05);
        QVERIFY(qAbs(c.valueF()        - 220.0 / 255.0) < 0.05);
    }

    void buildPlaceholder_returnsValidPixmap() {
        const QPixmap pm = CoverImage::buildPlaceholder(
            64, QStringLiteral("T"), QStringLiteral("https://example/x"));
        QVERIFY(!pm.isNull());
        QCOMPARE(pm.width(), 64);
        QCOMPARE(pm.height(), 64);
    }

    void buildPlaceholder_emptySideReturnsEmpty() {
        const QPixmap pm = CoverImage::buildPlaceholder(
            0, QStringLiteral("T"), QString());
        QVERIFY(pm.isNull());
    }

    void buildPlaceholder_negativeSideReturnsEmpty() {
        const QPixmap pm = CoverImage::buildPlaceholder(
            -1, QStringLiteral("T"), QString());
        QVERIFY(pm.isNull());
    }

    void buildPlaceholder_isCenteredLetter() {
        // We can't read the rendered glyph back deterministically
        // (font metrics vary by environment), but we can confirm the
        // pixmap has the right size and is not all-transparent.
        const QPixmap pm = CoverImage::buildPlaceholder(
            128, QStringLiteral("Z"), QString());
        QVERIFY(!pm.isNull());
        QCOMPARE(pm.size(), QSize(128, 128));
        // Check that the pixmap has SOME non-transparent pixel — the
        // rounded rect + letter must paint at least one opaque pixel.
        const QImage img = pm.toImage();
        bool anyOpaque = false;
        for (int y = 0; y < img.height(); ++y) {
            for (int x = 0; x < img.width(); ++x) {
                if (qAlpha(img.pixel(x, y)) > 0) {
                    anyOpaque = true;
                    break;
                }
            }
            if (anyOpaque) break;
        }
        QVERIFY(anyOpaque);
    }

    void buildPlaceholder_seedTextLongerThan1() {
        // Multiple-char seed text should be truncated to its first
        // character, uppercased. Confirm the pixmap still renders.
        const QPixmap pm = CoverImage::buildPlaceholder(
            64, QStringLiteral("Hello"), QString());
        QVERIFY(!pm.isNull());
    }

    void buildPlaceholder_emptySeedUsesMusicalGlyph() {
        // Empty seed text + empty source → ♪ fallback glyph.
        const QPixmap pm = CoverImage::buildPlaceholder(64, {}, {});
        QVERIFY(!pm.isNull());
    }

    // ── Async loading (5.5.E) ──────────────────────────────────────

    // Helper: synthesise a tiny PNG byte array. We don't need a
    // particular image — the test only cares that bytes decode to a
    // non-null QImage and the path "image was applied" is taken.
    QByteArray tinyPng(int w = 16, int h = 16) {
        QImage img(w, h, QImage::Format_ARGB32);
        img.fill(QColor(220, 30, 30));
        QByteArray bytes;
        QBuffer buf(&bytes);
        buf.open(QIODevice::WriteOnly);
        img.save(&buf, "PNG");
        return bytes;
    }

    void async_localPathIsSync() {
        // Local files take the sync fast path: no async loader is
        // invoked, and the image renders immediately.
        bool loaderCalled = false;
        CoverImage::ImageLoader loader =
            [&loaderCalled](const QUrl&, CoverImage::BytesCallback) {
                loaderCalled = true;
            };
        CoverImage img;
        img.resize(64, 64);
        img.setImageLoader(loader);
        img.setSource(QStringLiteral("C:/music/cover.jpg"),
                      QStringLiteral("L"));
        QVERIFY(!loaderCalled);
        QVERIFY(!img.isLoading());
        QVERIFY(!img.pixmap(Qt::ReturnByValue).isNull());
    }

    void async_urlTriggersLoader() {
        // An http(s) URL kicks the async loader; isLoading() flips
        // true and the placeholder is shown while the request is
        // in flight.
        bool loaderCalled = false;
        QUrl seenUrl;
        CoverImage::ImageLoader loader =
            [&loaderCalled, &seenUrl](const QUrl& u,
                                       CoverImage::BytesCallback) {
                loaderCalled = true;
                seenUrl = u;
            };
        CoverImage img;
        img.resize(64, 64);
        img.setImageLoader(loader);
        img.setSource(QStringLiteral("https://example.test/cover.png"),
                      QStringLiteral("U"));
        QVERIFY(loaderCalled);
        QCOMPARE(seenUrl, QUrl(QStringLiteral("https://example.test/cover.png")));
        QVERIFY(img.isLoading());
        QVERIFY(!img.pixmap(Qt::ReturnByValue).isNull());
    }

    void async_urlRendersFromLoaderCallback() {
        // When the loader's callback fires with bytes, the widget
        // decodes them, applies the image, and clears isLoading().
        CoverImage::ImageLoader loader =
            [this](const QUrl&, CoverImage::BytesCallback cb) {
                cb(tinyPng(), QStringLiteral("image/png"), QString());
            };
        CoverImage img;
        img.resize(64, 64);
        img.setImageLoader(loader);
        img.setSource(QStringLiteral("https://example.test/cover.png"),
                      QStringLiteral("U"));
        QVERIFY(!img.isLoading());
        QVERIFY(!img.pixmap(Qt::ReturnByValue).isNull());
    }

    void async_urlFallsBackOnError() {
        // Cache miss / network error: loader callback fires with
        // empty bytes + non-empty error. Widget must keep the
        // placeholder and clear isLoading().
        CoverImage::ImageLoader loader =
            [](const QUrl&, CoverImage::BytesCallback cb) {
                cb({}, {}, QStringLiteral("network down"));
            };
        CoverImage img;
        img.resize(64, 64);
        img.setImageLoader(loader);
        img.setSource(QStringLiteral("https://example.test/missing.png"),
                      QStringLiteral("M"));
        QVERIFY(!img.isLoading());
        QVERIFY(!img.pixmap(Qt::ReturnByValue).isNull());
    }

    void async_urlFallsBackOnEmptyBytes() {
        // Some loaders signal "miss" via empty bytes + empty error.
        // The widget must treat that as a miss too.
        CoverImage::ImageLoader loader =
            [](const QUrl&, CoverImage::BytesCallback cb) {
                cb({}, {}, QString());
            };
        CoverImage img;
        img.resize(64, 64);
        img.setImageLoader(loader);
        img.setSource(QStringLiteral("https://example.test/missing.png"),
                      QStringLiteral("M"));
        QVERIFY(!img.isLoading());
    }

    void async_rapidSetSource_dropsStaleCallback() {
        // Rapid setSource() calls must only commit the *latest*
        // result. Older in-flight callbacks are dropped via the
        // request-id check.
        QList<CoverImage::BytesCallback> pending;
        CoverImage::ImageLoader loader =
            [&pending](const QUrl&, CoverImage::BytesCallback cb) {
                pending.append(cb);
            };
        CoverImage img;
        img.resize(64, 64);
        img.setImageLoader(loader);

        img.setSource(QStringLiteral("https://a.test/1.png"),
                      QStringLiteral("A"));
        QCOMPARE(pending.size(), 1);
        QVERIFY(img.isLoading());

        img.setSource(QStringLiteral("https://b.test/2.png"),
                      QStringLiteral("B"));
        QCOMPARE(pending.size(), 2);
        QVERIFY(img.isLoading()); // still loading, now for B

        // Fire the stale A callback with valid bytes — must be a
        // no-op. isLoading() stays true (we're still waiting on B).
        pending.at(0)(tinyPng(), QStringLiteral("image/png"), QString());
        QVERIFY(img.isLoading());

        // Fire the B callback — must apply and clear isLoading().
        pending.at(1)(tinyPng(), QStringLiteral("image/png"), QString());
        QVERIFY(!img.isLoading());
    }

    void async_cancelOnDtor() {
        // The widget is destroyed while a load is in flight. The
        // captured QPointer<CoverImage> goes null and the callback
        // must be a safe no-op.
        QList<CoverImage::BytesCallback> pending;
        CoverImage::ImageLoader loader =
            [&pending](const QUrl&, CoverImage::BytesCallback cb) {
                pending.append(cb);
            };
        {
            CoverImage img;
            img.resize(64, 64);
            img.setImageLoader(loader);
            img.setSource(QStringLiteral("https://example.test/slow.png"),
                          QStringLiteral("S"));
            QCOMPARE(pending.size(), 1);
            QVERIFY(img.isLoading());
        } // widget destroyed
        // Fire the captured callback. Must not crash.
        pending.takeFirst()(tinyPng(),
                            QStringLiteral("image/png"),
                            QString());
    }

    void async_loaderNotInvokedForFileUrl() {
        // file:// URLs use the sync path (PathToImage reads via
        // QImage::load). The async loader must not be called.
        bool loaderCalled = false;
        CoverImage::ImageLoader loader =
            [&loaderCalled](const QUrl&, CoverImage::BytesCallback) {
                loaderCalled = true;
            };
        CoverImage img;
        img.resize(64, 64);
        img.setImageLoader(loader);
        img.setSource(QStringLiteral("file:///C:/music/cover.jpg"),
                      QStringLiteral("F"));
        QVERIFY(!loaderCalled);
        QVERIFY(!img.isLoading());
    }

    void async_localPathFollowedByUrl_usesAsync() {
        // After a sync local-path render, switching to an http URL
        // must invoke the async loader and flip isLoading() true.
        bool loaderCalled = false;
        CoverImage::ImageLoader loader =
            [&loaderCalled](const QUrl&, CoverImage::BytesCallback) {
                loaderCalled = true;
            };
        CoverImage img;
        img.resize(64, 64);
        img.setImageLoader(loader);
        img.setSource(QStringLiteral("C:/music/cover.jpg"),
                      QStringLiteral("L"));
        QVERIFY(!loaderCalled);
        QVERIFY(!img.isLoading());

        img.setSource(QStringLiteral("https://example.test/c.png"),
                      QStringLiteral("U"));
        QVERIFY(loaderCalled);
        QVERIFY(img.isLoading());
    }
};

// Custom main: QGuiApplication (not QApplication) because we only need
// a paint device for QPixmap. The offscreen QPA is set by the test
// properties in tests/CMakeLists.txt.
int main(int argc, char* argv[]) {
    QGuiApplication app(argc, argv);
    TestCoverImage tc;
    return QTest::qExec(&tc, argc, argv);
}

#include "testcoverimage.moc"

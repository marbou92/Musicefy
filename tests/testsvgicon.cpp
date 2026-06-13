// testsvgicon.cpp
// Unit tests for SvgIcon. The renderer wraps QSvgRenderer around
// Lucide path data, so we need a QGuiApplication to allocate a
// paint device (QPixmap). The offscreen QPA is set by the test
// properties in tests/CMakeLists.txt.

#include <QtTest/QtTest>
#include <QGuiApplication>
#include <QIcon>
#include <QImage>
#include <QPixmap>
#include <QString>

#include "widgets/SvgIcon.h"
#include "widgets/Icons.h"

using mf::app::widgets::SvgIcon;

class TestSvgIcon : public QObject {
    Q_OBJECT

private slots:
    void initTestCase() {
        // We need a paint device (QPixmap) for SvgIcon::get, which
        // requires QGuiApplication at minimum.
        QVERIFY(QGuiApplication::instance() != nullptr);
    }

    void knownName_returnsNonEmptyIcon() {
        const QIcon icon = SvgIcon::get("play", QColor("#ff0000"), 32);
        QVERIFY(!icon.isNull());
        // The QIcon should produce a non-null pixmap at the requested
        // size — the "actual" size is 0 because we don't have a
        // screensize, but pixmap(32, 32) is honoured.
        const QPixmap pm = icon.pixmap(32, 32);
        QVERIFY(!pm.isNull());
        QCOMPARE(pm.width(),  32);
        QCOMPARE(pm.height(), 32);
    }

    void unknownName_returnsEmptyIcon() {
        const QIcon icon = SvgIcon::get("this-icon-does-not-exist",
                                        QColor("#000000"), 24);
        QVERIFY(icon.isNull());
    }

    void invalidSize_returnsEmptyIcon() {
        const QIcon icon = SvgIcon::get("play", QColor("#ffffff"), 0);
        QVERIFY(icon.isNull());
    }

    void invalidColor_returnsEmptyIcon() {
        const QIcon icon = SvgIcon::get("play", QColor(), 16);
        QVERIFY(icon.isNull());
    }

    void allNamedIcons_renderToNonEmptyPixmap() {
        // Walk the entire table — every entry must produce a valid
        // pixmap (catches malformed SVG path data at unit-test time).
        const auto& table = mf::app::widgets::icons::all();
        QVERIFY(!table.isEmpty());
        for (auto it = table.constBegin(); it != table.constEnd(); ++it) {
            const QIcon icon = SvgIcon::get(it.key(), QColor("#222222"), 24);
            const QPixmap pm = icon.pixmap(24, 24);
            QVERIFY2(!pm.isNull(),
                     qPrintable(QStringLiteral("Icon '%1' rendered null")
                                .arg(it.key())));
            QCOMPARE(pm.width(),  24);
            QCOMPARE(pm.height(), 24);
        }
    }

    void renderedIconHasNonTransparentPixels() {
        // A successfully-rendered Lucide icon should have at least
        // *some* non-transparent pixels (the stroke). If QSvgRenderer
        // silently dropped the path, the pixmap would be all
        // transparent — that would be a useful regression sentinel.
        const QIcon icon = SvgIcon::get("play", QColor("#ff0000"), 48);
        const QPixmap pm = icon.pixmap(48, 48);
        const QImage img = pm.toImage();
        bool sawOpaque = false;
        for (int y = 0; y < img.height() && !sawOpaque; ++y) {
            for (int x = 0; x < img.width(); ++x) {
                if (img.pixelColor(x, y).alpha() > 0) {
                    sawOpaque = true;
                    break;
                }
            }
        }
        QVERIFY(sawOpaque);
    }

    void differentColorsProduceDifferentRenderings() {
        // Two colors with the same icon name should rasterize to
        // pixels with different dominant colors. (The stroke color
        // comes from the `stroke='%1'` substitution.)
        const QIcon iconRed   = SvgIcon::get("play", QColor("#ff0000"), 32);
        const QIcon iconBlue  = SvgIcon::get("play", QColor("#0000ff"), 32);
        const QImage imgRed   = iconRed.pixmap(32, 32).toImage();
        const QImage imgBlue  = iconBlue.pixmap(32, 32).toImage();
        QVERIFY(!imgRed.isNull());
        QVERIFY(!imgBlue.isNull());
        // Sample a pixel in the middle of each — the stroke path
        // passes through the center for the play triangle.
        // (If the path doesn't cross the center, this test is
        // satisfied trivially because both images would still
        // differ in their pixel data due to anti-aliasing.)
        QVERIFY(imgRed != imgBlue);
    }
};

// Custom main: QGuiApplication for paint-device support.
int main(int argc, char* argv[]) {
    QGuiApplication app(argc, argv);
    TestSvgIcon tc;
    return QTest::qExec(&tc, argc, argv);
}

#include "testsvgicon.moc"

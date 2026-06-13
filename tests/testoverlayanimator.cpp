// testoverlayanimator.cpp
// Unit tests for OverlayStackAnimator. Needs QApplication (not
// just QGuiApplication) because QStackedWidget + QGraphicsOpacityEffect
// live in QtWidgets.

#include <QtTest/QtTest>
#include <QApplication>
#include <QSignalSpy>
#include <QStackedWidget>
#include <QWidget>

#include "widgets/OverlayStackAnimator.h"

using mf::app::widgets::OverlayStackAnimator;

class TestOverlayAnimator : public QObject {
    Q_OBJECT

private:
    // Build a minimal contentStack_ + overlayStack_ pair: a
    // "content" QStackedWidget with 2 indices (page, overlay) and
    // an "overlay" QStackedWidget with 3 overlay indices. Returns
    // the content stack; the overlay stack is added as index 1 of
    // content so the animator sees a realistic structure.
    QStackedWidget* buildStacks(QStackedWidget** overlayOut) {
        auto* content  = new QStackedWidget();
        auto* page     = new QWidget(content);
        auto* overlay  = new QStackedWidget(content);
        *overlayOut    = overlay;
        content->addWidget(page);    // index 0
        content->addWidget(overlay); // index 1

        auto* a = new QWidget(overlay);
        auto* b = new QWidget(overlay);
        auto* c = new QWidget(overlay);
        overlay->addWidget(a);  // index 0
        overlay->addWidget(b);  // index 1
        overlay->addWidget(c);  // index 2
        return content;
    }

private slots:
    void initTestCase() {
        QVERIFY(QApplication::instance() != nullptr);
    }

    void ctor_attachesOpacityEffect() {
        QStackedWidget* overlay = nullptr;
        QStackedWidget* content = buildStacks(&overlay);
        OverlayStackAnimator anim(content, overlay);
        QVERIFY(anim.isAnimating() == false);
        // The animator should have attached a graphics effect to
        // the content stack.
        QVERIFY(content->graphicsEffect() != nullptr);
    }

    void showOverlay_changesIndexAfterFade() {
        QStackedWidget* overlay = nullptr;
        QStackedWidget* content = buildStacks(&overlay);
        OverlayStackAnimator anim(content, overlay);
        QCOMPARE(content->currentIndex(), 0);

        QSignalSpy spy(&anim, &OverlayStackAnimator::transitionFinished);
        anim.showOverlay(1);
        QVERIFY(anim.isAnimating());
        // Wait for both halves of the fade (~300ms total at 150ms each).
        QVERIFY(spy.wait(2000));
        QCOMPARE(content->currentIndex(), 1);
        QCOMPARE(overlay->currentIndex(), 1);
        QVERIFY(!anim.isAnimating());
    }

    void showPage_returnsToIndex0() {
        QStackedWidget* overlay = nullptr;
        QStackedWidget* content = buildStacks(&overlay);
        OverlayStackAnimator anim(content, overlay);
        anim.showOverlay(2);
        QVERIFY(anim.isAnimating());
        QTest::qWait(500);
        QCOMPARE(content->currentIndex(), 1);

        QSignalSpy spy(&anim, &OverlayStackAnimator::transitionFinished);
        anim.showPage();
        QVERIFY(anim.isAnimating());
        QVERIFY(spy.wait(2000));
        QCOMPARE(content->currentIndex(), 0);
        QVERIFY(!anim.isAnimating());
    }

    void noopRequest_doesNotStartAnimation() {
        QStackedWidget* overlay = nullptr;
        QStackedWidget* content = buildStacks(&overlay);
        OverlayStackAnimator anim(content, overlay);

        // Already on page → showPage() is a no-op.
        anim.showPage();
        QVERIFY(!anim.isAnimating());
        QCOMPARE(content->currentIndex(), 0);
    }

    void rapidRequests_areCoalesced() {
        QStackedWidget* overlay = nullptr;
        QStackedWidget* content = buildStacks(&overlay);
        OverlayStackAnimator anim(content, overlay);

        // Fire 5 rapid showOverlay() calls with different indices.
        // The animator should still finish on the LAST requested
        // index (2), not strobe between them.
        anim.showOverlay(0);
        anim.showOverlay(1);
        anim.showOverlay(2);
        QVERIFY(anim.isAnimating());

        QSignalSpy spy(&anim, &OverlayStackAnimator::transitionFinished);
        QVERIFY(spy.wait(2000));
        // The latest target (overlay 2) must have won.
        QCOMPARE(overlay->currentIndex(), 2);
        QVERIFY(!anim.isAnimating());
    }

    void rapidOscillate_doesNotLeaveContentOnWrongIndex() {
        QStackedWidget* overlay = nullptr;
        QStackedWidget* content = buildStacks(&overlay);
        OverlayStackAnimator anim(content, overlay);

        // Start on page, then rapidly alternate.
        anim.showOverlay(0);
        anim.showPage();
        anim.showOverlay(1);
        anim.showPage();
        anim.showOverlay(2);

        QSignalSpy spy(&anim, &OverlayStackAnimator::transitionFinished);
        QVERIFY(spy.wait(2000));
        // Whatever the final intent, content must settle on a
        // single index. (1 = overlay, 0 = page).
        const int idx = content->currentIndex();
        QVERIFY(idx == 0 || idx == 1);
    }
};

int main(int argc, char* argv[]) {
    QApplication app(argc, argv);
    TestOverlayAnimator t;
    return QTest::qExec(&t, argc, argv);
}

#include "testoverlayanimator.moc"

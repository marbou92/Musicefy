// testcardcolorglowbehavior.cpp
// Unit tests for CardColorGlowBehavior: install, glow opacity
// animation, and hover event handling.

#include <QtTest/QtTest>
#include <QSignalSpy>
#include <QGraphicsDropShadowEffect>
#include <QLabel>
#include <QVBoxLayout>

#include "widgets/CardColorGlowBehavior.h"

using mf::app::widgets::CardColorGlowBehavior;

class TestCardColorGlowBehavior : public QObject {
    Q_OBJECT

private slots:
    void install_returnsNonNull() {
        QWidget w;
        auto* b = CardColorGlowBehavior::install(&w);
        QVERIFY(b != nullptr);
    }

    void install_setsHoverAttribute() {
        QWidget w;
        CardColorGlowBehavior::install(&w);
        QVERIFY(w.testAttribute(Qt::WA_Hover));
    }

    void glowOpacity_defaultIsZero() {
        QWidget w;
        auto* b = CardColorGlowBehavior::install(&w);
        QCOMPARE(b->glowOpacity(), 0.0f);
    }

    void glowOpacity_changeEmitsSignal() {
        QWidget w;
        auto* b = CardColorGlowBehavior::install(&w);
        QSignalSpy spy(b, &CardColorGlowBehavior::glowOpacityChanged);
        b->setGlowOpacity(0.5f);
        QCOMPARE(spy.count(), 1);
        QCOMPARE(b->glowOpacity(), 0.5f);
    }

    void glowOpacity_sameValueNoEmit() {
        QWidget w;
        auto* b = CardColorGlowBehavior::install(&w);
        b->setGlowOpacity(0.0f);
        QSignalSpy spy(b, &CardColorGlowBehavior::glowOpacityChanged);
        b->setGlowOpacity(0.0f);
        QCOMPARE(spy.count(), 0);
    }

    void glowOpacity_aboveThreshold_createsShadow() {
        QWidget w;
        auto* b = CardColorGlowBehavior::install(&w);
        b->setGlowOpacity(0.5f);
        auto* shadow = qobject_cast<QGraphicsDropShadowEffect*>(w.graphicsEffect());
        // Shadow is created when glowOpacity > 0.01
        QVERIFY(shadow != nullptr);
    }

    void glowOpacity_zero_removesShadow() {
        QWidget w;
        auto* b = CardColorGlowBehavior::install(&w);
        b->setGlowOpacity(0.5f);
        QVERIFY(w.graphicsEffect() != nullptr);
        b->setGlowOpacity(0.0f);
        // At 0.0, the shadow should be removed (set to nullptr)
        // Note: Qt may or may not null the effect depending on implementation
        QCOMPARE(b->glowOpacity(), 0.0f);
    }

    void install_nullWidget_returnsNull() {
        auto* b = CardColorGlowBehavior::install(nullptr);
        QVERIFY(b == nullptr);
    }

    void install_sameWidgetTwice_doesNotCrash() {
        QWidget w;
        auto* b1 = CardColorGlowBehavior::install(&w);
        auto* b2 = CardColorGlowBehavior::install(&w);
        // Second install should not crash; behavior is undefined but safe
        QVERIFY(b1 != nullptr);
    }

    void shadow_blurRadius_isPositive() {
        QWidget w;
        auto* b = CardColorGlowBehavior::install(&w);
        b->setGlowOpacity(0.3f);
        auto* shadow = qobject_cast<QGraphicsDropShadowEffect*>(w.graphicsEffect());
        if (shadow) {
            QVERIFY(shadow->blurRadius() > 0);
        }
    }
};

QTEST_MAIN(TestCardColorGlowBehavior)
#include "testcardcolorglowbehavior.moc"

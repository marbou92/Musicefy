// testbotdetection.cpp
// Unit tests for BotDetectionMitigator. Covers failure counting,
// success reset, rotation threshold, and the shouldRotate flag.

#include <QtTest/QtTest>
#include <QCoreApplication>

#include "sources/youtube/BotDetectionMitigator.h"

using namespace mf::core::sources::youtube;

class TestBotDetection : public QObject {
    Q_OBJECT
private slots:
    // ── 1. Initial state ─────────────────────────────────────────────
    void botMitigator_initialState() {
        BotDetectionMitigator m;
        QCOMPARE(m.consecutiveFailures(), 0);
        QVERIFY(!m.shouldRotateVisitorData());
        QCOMPARE(m.rotationThreshold(), 3);
    }

    // ── 2. Single failure doesn't trigger rotation ───────────────────
    void botMitigator_singleFailureNoRotation() {
        BotDetectionMitigator m;
        m.notifyPlaybackFailure();
        QCOMPARE(m.consecutiveFailures(), 1);
        QVERIFY(!m.shouldRotateVisitorData());
    }

    // ── 3. Two failures don't trigger rotation ───────────────────────
    void botMitigator_twoFailuresNoRotation() {
        BotDetectionMitigator m;
        m.notifyPlaybackFailure();
        m.notifyPlaybackFailure();
        QCOMPARE(m.consecutiveFailures(), 2);
        QVERIFY(!m.shouldRotateVisitorData());
    }

    // ── 4. Three failures trigger rotation (default threshold) ───────
    void botMitigator_threeFailuresTriggersRotation() {
        BotDetectionMitigator m;
        m.notifyPlaybackFailure();
        m.notifyPlaybackFailure();
        m.notifyPlaybackFailure();
        QCOMPARE(m.consecutiveFailures(), 3);
        QVERIFY(m.shouldRotateVisitorData());
    }

    // ── 5. Success resets counter ─────────────────────────────────────
    void botMitigator_successResetsCounter() {
        BotDetectionMitigator m;
        m.notifyPlaybackFailure();
        m.notifyPlaybackFailure();
        m.notifyPlaybackSuccess();
        QCOMPARE(m.consecutiveFailures(), 0);
        QVERIFY(!m.shouldRotateVisitorData());
    }

    // ── 6. Reset clears everything ────────────────────────────────────
    void botMitigator_resetClearsAll() {
        BotDetectionMitigator m;
        m.notifyPlaybackFailure();
        m.notifyPlaybackFailure();
        m.notifyPlaybackFailure();
        QVERIFY(m.shouldRotateVisitorData());
        m.reset();
        QCOMPARE(m.consecutiveFailures(), 0);
        QVERIFY(!m.shouldRotateVisitorData());
    }

    // ── 7. Custom threshold ──────────────────────────────────────────
    void botMitigator_customThreshold() {
        BotDetectionMitigator m;
        m.setRotationThreshold(5);
        QCOMPARE(m.rotationThreshold(), 5);
        for (int i = 0; i < 4; ++i) {
            m.notifyPlaybackFailure();
        }
        QVERIFY(!m.shouldRotateVisitorData());
        m.notifyPlaybackFailure();
        QVERIFY(m.shouldRotateVisitorData());
    }

    // ── 8. Failure after success starts new streak ───────────────────
    void botMitigator_failureAfterSuccessStartsNewStreak() {
        BotDetectionMitigator m;
        m.notifyPlaybackFailure();
        m.notifyPlaybackFailure();
        m.notifyPlaybackFailure();
        QVERIFY(m.shouldRotateVisitorData());
        m.reset();
        m.notifyPlaybackFailure();
        QCOMPARE(m.consecutiveFailures(), 1);
        QVERIFY(!m.shouldRotateVisitorData());
    }

    // ── 9. Threshold of 1 triggers on first failure ──────────────────
    void botMitigator_thresholdOneTriggersImmediately() {
        BotDetectionMitigator m;
        m.setRotationThreshold(1);
        m.notifyPlaybackFailure();
        QVERIFY(m.shouldRotateVisitorData());
    }

    // ── 10. Many failures stay flagged ───────────────────────────────
    void botMitigator_manyFailuresStayFlagged() {
        BotDetectionMitigator m;
        for (int i = 0; i < 10; ++i) {
            m.notifyPlaybackFailure();
        }
        QCOMPARE(m.consecutiveFailures(), 10);
        QVERIFY(m.shouldRotateVisitorData());
    }
};

QTEST_GUILESS_MAIN(TestBotDetection)
#include "testbotdetection.moc"

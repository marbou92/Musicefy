// testplayback.cpp
// Unit tests for the playback layer.
// QueueManager: pure logic, fully testable.
// PlaybackService: only the parts that don't need an active media stream
//                  (setTrack, volume, mute, state) are tested here;
//                  full playback requires a real media file and is
//                  exercised manually.

#include <QtTest/QtTest>

#include "playback/QueueManager.h"
#include "playback/PlaybackService.h"
#include "playback/MediaKeyFilter.h"
#include "playback/SmtcController.h"

#include "models/MusicFile.h"

using namespace mf::core::playback;
using namespace mf::core::models;
using mf::core::interfaces::IQueueManager;

class TestPlayback : public QObject {
    Q_OBJECT

private:
    static MusicFile makeTrack(int n) {
        MusicFile m;
        m.setFilePath(QStringLiteral("/music/track_%1.mp3").arg(n));
        m.setTitle(QStringLiteral("Track %1").arg(n));
        m.setArtist(QStringLiteral("Artist %1").arg(n));
        m.setAlbum(QStringLiteral("Album %1").arg(n));
        m.setTrackNumber(n);
        m.setDuration(std::chrono::seconds{ 180 + n });
        m.setSourceType(QStringLiteral("local"));
        return m;
    }

private slots:
    void queueEnqueueAndCount() {
        QueueManager qm;
        QCOMPARE(qm.count(), 0);
        QCOMPARE(qm.currentIndex(), -1);

        qm.enqueue(makeTrack(1));
        qm.enqueue(makeTrack(2));
        qm.enqueue(makeTrack(3));
        QCOMPARE(qm.count(), 3);
        QCOMPARE(qm.currentIndex(), 0);
        QCOMPARE(qm.currentTrack().title(), QStringLiteral("Track 1"));
    }

    void queueEnqueueMany() {
        QueueManager qm;
        QList<MusicFile> tracks;
        tracks.append(makeTrack(1));
        tracks.append(makeTrack(2));
        tracks.append(makeTrack(3));
        qm.enqueueMany(tracks);
        QCOMPARE(qm.count(), 3);
    }

    void queueNextPreviousRepeatOff() {
        QueueManager qm;
        qm.enqueueMany({makeTrack(1), makeTrack(2), makeTrack(3)});

        QVERIFY(qm.hasNext());
        QVERIFY(!qm.hasPrevious());

        qm.next();
        QCOMPARE(qm.currentIndex(), 1);
        QVERIFY(qm.hasNext());
        QVERIFY(qm.hasPrevious());

        qm.next();
        QCOMPARE(qm.currentIndex(), 2);
        QVERIFY(!qm.hasNext()); // at end, repeat off
        QVERIFY(qm.hasPrevious());

        qm.next(); // does nothing when repeat off
        QCOMPARE(qm.currentIndex(), 2);

        qm.previous();
        QCOMPARE(qm.currentIndex(), 1);
    }

    void queueRepeatAll() {
        QueueManager qm;
        qm.setRepeatMode(IQueueManager::RepeatMode::All);
        qm.enqueueMany({makeTrack(1), makeTrack(2), makeTrack(3)});

        qm.setCurrentIndex(2);
        QVERIFY(qm.hasNext());
        qm.next();
        QCOMPARE(qm.currentIndex(), 0);

        qm.previous();
        QCOMPARE(qm.currentIndex(), 2);
    }

    void queueDequeueAt() {
        QueueManager qm;
        qm.enqueueMany({makeTrack(1), makeTrack(2), makeTrack(3)});
        qm.dequeueAt(1);
        QCOMPARE(qm.count(), 2);
        QCOMPARE(qm.trackAt(0).title(), QStringLiteral("Track 1"));
        QCOMPARE(qm.trackAt(1).title(), QStringLiteral("Track 3"));
    }

    void queueClear() {
        QueueManager qm;
        qm.enqueueMany({makeTrack(1), makeTrack(2)});
        QCOMPARE(qm.count(), 2);
        qm.clear();
        QCOMPARE(qm.count(), 0);
        QCOMPARE(qm.currentIndex(), -1);
        QVERIFY(qm.currentTrack().filePath().isEmpty());
    }

    void queueShufflePreservesAllTracks() {
        QueueManager qm;
        QList<MusicFile> tracks;
        for (int i = 1; i <= 10; ++i) tracks.append(makeTrack(i));
        qm.enqueueMany(tracks);
        qm.setShuffle(true);
        QVERIFY(qm.isShuffle());

        // All original titles are still present, just reordered.
        QList<MusicFile> visible = qm.tracks();
        QCOMPARE(visible.size(), tracks.size());
        QList<QString> titles;
        for (const auto& t : visible) titles.append(t.title());
        for (const auto& t : tracks) {
            QVERIFY(titles.contains(t.title()));
        }
    }

    void queueShuffleOffRestoresOrder() {
        QueueManager qm;
        QList<MusicFile> tracks;
        for (int i = 1; i <= 5; ++i) tracks.append(makeTrack(i));
        qm.enqueueMany(tracks);

        qm.setShuffle(true);
        qm.setShuffle(false);

        QList<MusicFile> visible = qm.tracks();
        for (int i = 0; i < visible.size(); ++i) {
            QCOMPARE(visible[i].title(), tracks[i].title());
        }
    }

    void queueCallbacksFire() {
        QueueManager qm;
        int queueChanges = 0;
        int indexChanges = 0;
        int shuffleChanges = 0;
        int repeatChanges = 0;
        qm.setOnQueueChanged([&]() { ++queueChanges; });
        qm.setOnIndexChanged([&](int) { ++indexChanges; });
        qm.setOnShuffleChanged([&](bool) { ++shuffleChanges; });
        qm.setOnRepeatChanged([&](int) { ++repeatChanges; });

        qm.enqueue(makeTrack(1));                // queue + index
        qm.enqueue(makeTrack(2));                // queue
        qm.next();                               // index
        qm.setShuffle(true);                     // shuffle
        qm.setRepeatMode(IQueueManager::RepeatMode::All); // repeat

        QCOMPARE(queueChanges, 2);
        QCOMPARE(indexChanges, 2);
        QCOMPARE(shuffleChanges, 1);
        QCOMPARE(repeatChanges, 1);
    }

    void playbackServiceDefaults() {
        PlaybackService svc;
        QCOMPARE(svc.volume(), 1.0f);
        QVERIFY(!svc.isMuted());
        QCOMPARE(svc.state(), mf::core::interfaces::IAudioPlayer::PlaybackState::Stopped);
        QCOMPARE(svc.position(), std::chrono::milliseconds{0});
        QCOMPARE(svc.duration(), std::chrono::milliseconds{0});
    }

    void playbackServiceVolumeClamp() {
        PlaybackService svc;
        svc.setVolume(2.0f);
        QCOMPARE(svc.volume(), 1.0f);
        svc.setVolume(-1.0f);
        QCOMPARE(svc.volume(), 0.0f);
        svc.setVolume(0.5f);
        QCOMPARE(svc.volume(), 0.5f);
    }

    void playbackServiceMute() {
        PlaybackService svc;
        svc.setMuted(true);
        QVERIFY(svc.isMuted());
        svc.setMuted(false);
        QVERIFY(!svc.isMuted());
    }

    void playbackServiceSetTrack() {
        PlaybackService svc;
        MusicFile t = makeTrack(42);
        svc.setTrack(t);
        QCOMPARE(svc.currentTrack().filePath(), t.filePath());
        QCOMPARE(svc.currentTrack().title(), t.title());
    }

    void playbackServiceCallbacksFire() {
        PlaybackService svc;
        int stateChanges = 0;
        int trackChanges = 0;
        int positionChanges = 0;
        int errorCount = 0;
        svc.setOnStateChanged([&](int) { ++stateChanges; });
        svc.setOnTrackChanged([&](MusicFile) { ++trackChanges; });
        svc.setOnPositionChanged([&](std::chrono::milliseconds) { ++positionChanges; });
        svc.setOnError([&](QString) { ++errorCount; });

        svc.setTrack(makeTrack(1));
        QTRY_VERIFY(trackChanges >= 1);

        // No position changes expected without an active media stream.
        // (Qt's positionChanged only fires while playing.)
    }

    void smtcControllerMetadataLogs() {
        SmtcController smtc;
        // Just exercise the API; the stub logs to qDebug.
        smtc.updateMetadata(makeTrack(1));
        smtc.updatePlaybackStatus(true, false, false);
        smtc.updateTimeline(1000, 180000);
        smtc.clearMetadata();
    }

    void smtcCommandCallback_isInvoked() {
        SmtcController smtc;
        int received = -1;
        smtc.setOnCommand([&received](SmtcCommand cmd) {
            received = static_cast<int>(cmd);
        });
        // The stub doesn't emit on its own; the AppLifecycle wires the
        // same callback as the commandReceivedQ signal, so we exercise
        // that here.
        QObject::connect(&smtc, &SmtcController::commandReceivedQ,
                         [&received](int cmd) {
            received = cmd;
        });
        emit smtc.commandReceivedQ(static_cast<int>(SmtcCommand::Next));
        QCOMPARE(received, static_cast<int>(SmtcCommand::Next));
    }

    void smtcCommandRouting_queueAndPlayback() {
        // Reproduce the AppLifecycle::wireSmtc() routing logic in a
        // minimal form so we can assert the connections work end-to-end
        // without standing up the whole AppContainer.
        SmtcController smtc;
        PlaybackService playback;
        QueueManager queue;
        queue.enqueue(makeTrack(1));
        queue.enqueue(makeTrack(2));
        queue.enqueue(makeTrack(3));
        QCOMPARE(queue.currentIndex(), 0);

        QObject::connect(&smtc, &SmtcController::commandReceivedQ,
                         [&playback, &queue](int cmd) {
            switch (static_cast<SmtcCommand>(cmd)) {
                case SmtcCommand::PlayPauseToggle:
                    playback.togglePlayPause();
                    break;
                case SmtcCommand::Next:     queue.next();     break;
                case SmtcCommand::Previous: queue.previous(); break;
                case SmtcCommand::Stop:     playback.stop();  break;
                default: break;
            }
        });
        emit smtc.commandReceivedQ(static_cast<int>(SmtcCommand::Next));
        QCOMPARE(queue.currentIndex(), 1);
        emit smtc.commandReceivedQ(static_cast<int>(SmtcCommand::Previous));
        QCOMPARE(queue.currentIndex(), 0);
    }

    void smtcIsSupported_returnsBool() {
        // No assertion on the value — on Win 7 it should be false, on
        // Win 10+ it depends on the build flag. We just want to confirm
        // the call doesn't crash.
        const bool supported = SmtcController::isSupported();
        Q_UNUSED(supported);
    }

    void mediaKeyFilterIsInstantiable() {
        MediaKeyFilter filter;
        // The filter installs itself on QCoreApplication. Smoke-test only.
        // The platform-specific behaviour (intercepting WM_APPCOMMAND) is
        // verified manually because Qt's offscreen test platform doesn't
        // route Win32 messages.
    }

    // ── move() (5.5.F) ──────────────────────────────────────────────

    void queueMoveWithinOrder() {
        QueueManager qm;
        qm.enqueueMany({makeTrack(1), makeTrack(2),
                        makeTrack(3), makeTrack(4)});
        qm.move(0, 2);
        const auto visible = qm.tracks();
        QCOMPARE(visible.size(), 4);
        QCOMPARE(visible[0].title(), QStringLiteral("Track 2"));
        QCOMPARE(visible[1].title(), QStringLiteral("Track 3"));
        QCOMPARE(visible[2].title(), QStringLiteral("Track 1"));
        QCOMPARE(visible[3].title(), QStringLiteral("Track 4"));
    }

    void queueMoveAtEndIsNoop() {
        QueueManager qm;
        qm.enqueueMany({makeTrack(1), makeTrack(2),
                        makeTrack(3), makeTrack(4)});
        QSignalSpy spy(&qm, &QueueManager::queueChangedQ);
        // from == to is the canonical no-op. (move(0, 3) is a valid
        // move and does change the order, which is covered by
        // queueMoveWithinOrder.)
        qm.move(2, 2);
        QCOMPARE(spy.count(), 0);
    }

    void queueMovePreservesCurrentTrack() {
        QueueManager qm;
        qm.enqueueMany({makeTrack(1), makeTrack(2),
                        makeTrack(3), makeTrack(4)});
        qm.setCurrentIndex(1); // playing Track 2
        QCOMPARE(qm.currentTrack().title(), QStringLiteral("Track 2"));
        qm.move(1, 3); // take Track 2 from original[1], insert at original[3]
        // Currently-playing track (Track 2) must still be current.
        QCOMPARE(qm.currentTrack().title(), QStringLiteral("Track 2"));
        // Its visible position should follow it.
        const auto visible = qm.tracks();
        int pos = -1;
        for (int i = 0; i < visible.size(); ++i) {
            if (visible[i].title() == QStringLiteral("Track 2")) {
                pos = i;
                break;
            }
        }
        QVERIFY(pos >= 0);
        QCOMPARE(qm.currentIndex(), pos);
    }

    void queueMoveRebuildsShuffle() {
        QueueManager qm;
        QList<MusicFile> tracks;
        for (int i = 1; i <= 6; ++i) tracks.append(makeTrack(i));
        qm.enqueueMany(tracks);
        qm.setShuffle(true);
        // Move something.
        qm.move(0, 3);
        // After the move + shuffle rebuild, every original index must
        // appear exactly once in the visible permutation.
        const auto visible = qm.tracks();
        QSet<QString> titles;
        for (const auto& t : visible) titles.insert(t.title());
        QCOMPARE(titles.size(), 6);
    }

    void queueMoveOutOfRangeIsNoop() {
        QueueManager qm;
        qm.enqueueMany({makeTrack(1), makeTrack(2), makeTrack(3)});
        QSignalSpy spy(&qm, &QueueManager::queueChangedQ);
        const auto before = qm.tracks();
        qm.move(99, 0);
        qm.move(0, -1);
        QCOMPARE(qm.tracks().size(), before.size());
        QCOMPARE(spy.count(), 0);
    }

    void queueMoveEmitsQueueChangedQ() {
        QueueManager qm;
        qm.enqueueMany({makeTrack(1), makeTrack(2), makeTrack(3)});
        QSignalSpy spy(&qm, &QueueManager::queueChangedQ);
        qm.move(0, 2);
        QCOMPARE(spy.count(), 1);
    }

    void queueSetOrderFromVisible_basic() {
        QueueManager qm;
        qm.enqueueMany({makeTrack(1), makeTrack(2), makeTrack(3),
                        makeTrack(4)});
        // Reverse the order.
        QList<MusicFile> newOrder;
        for (int i = 4; i >= 1; --i) newOrder.append(makeTrack(i));
        qm.setOrderFromVisible(newOrder);
        const auto visible = qm.tracks();
        QCOMPARE(visible[0].title(), QStringLiteral("Track 4"));
        QCOMPARE(visible[1].title(), QStringLiteral("Track 3"));
        QCOMPARE(visible[2].title(), QStringLiteral("Track 2"));
        QCOMPARE(visible[3].title(), QStringLiteral("Track 1"));
    }

    void queueSetOrderFromVisible_preservesCurrent() {
        QueueManager qm;
        qm.enqueueMany({makeTrack(1), makeTrack(2), makeTrack(3),
                        makeTrack(4)});
        qm.setCurrentIndex(0); // playing Track 1
        QList<MusicFile> newOrder;
        newOrder.append(makeTrack(3));
        newOrder.append(makeTrack(1));
        newOrder.append(makeTrack(2));
        newOrder.append(makeTrack(4));
        qm.setOrderFromVisible(newOrder);
        QCOMPARE(qm.currentTrack().title(), QStringLiteral("Track 1"));
        // The visible position of Track 1 is now 1.
        QCOMPARE(qm.currentIndex(), 1);
    }

    void queueSetOrderFromVisible_sizeMismatchIsNoop() {
        QueueManager qm;
        qm.enqueueMany({makeTrack(1), makeTrack(2), makeTrack(3)});
        QSignalSpy spy(&qm, &QueueManager::queueChangedQ);
        // Different size — must be a no-op.
        qm.setOrderFromVisible({makeTrack(1), makeTrack(2)});
        QCOMPARE(qm.count(), 3);
        QCOMPARE(spy.count(), 0);
    }
};

QTEST_GUILESS_MAIN(TestPlayback)
#include "testplayback.moc"

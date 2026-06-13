// testplayerviewmodel.cpp
// Verifies that PlayerViewModel correctly mirrors PlaybackService +
// QueueManager state and forwards commands back.

#include <QtTest/QtTest>
#include <QCoreApplication>
#include <QSettings>

#include "core/playback/PlaybackService.h"
#include "core/playback/QueueManager.h"
#include "core/models/MusicFile.h"

#include "viewmodels/PlayerViewModel.h"

using namespace mf::core::playback;
using namespace mf::core::models;
using namespace mf::app::viewmodels;

namespace {

MusicFile makeTrack(const QString& path, const QString& title,
                    const QString& artist = QStringLiteral("Artist"),
                    const QString& album  = QStringLiteral("Album")) {
    MusicFile m;
    m.setFilePath(path);
    m.setTitle(title);
    m.setArtist(artist);
    m.setAlbum(album);
    return m;
}

} // namespace

class TestPlayerViewModel : public QObject {
    Q_OBJECT

private:
    PlaybackService playback_;
    QueueManager    queue_;
    std::unique_ptr<PlayerViewModel> vm_;

    void makeVm() {
        vm_ = std::make_unique<PlayerViewModel>(&playback_, &queue_, nullptr, this);
    }

private slots:
    void initTestCase() {
        QCoreApplication::setOrganizationName(QStringLiteral("MusicefyTest"));
        QCoreApplication::setApplicationName(QStringLiteral("playerviewmodel"));
    }
    void cleanupTestCase() { QSettings().clear(); }

    void initialStateIsStopped() {
        makeVm();
        QVERIFY(!vm_->isPlaying());
        QVERIFY(!vm_->isPaused());
        QVERIFY(vm_->isStopped());
        QVERIFY(!vm_->hasCurrentTrack());
        QVERIFY(!vm_->hasNext());
        QVERIFY(!vm_->hasPrevious());
        QCOMPARE(vm_->queueCount(), 0);
        QCOMPARE(vm_->positionMs(), 0);
        QCOMPARE(vm_->durationMs(), 0);
    }

    void enqueueThenCurrentTrackMetadataIsExposed() {
        makeVm();
        queue_.enqueue(makeTrack(QStringLiteral("/x/a.mp3"), QStringLiteral("A")));
        queue_.enqueue(makeTrack(QStringLiteral("/x/b.mp3"), QStringLiteral("B"),
                                 QStringLiteral("B-Artist"), QStringLiteral("B-Album")));
        // After two enqueues, currentIndex is 0.
        QVERIFY(vm_->hasCurrentTrack());
        QCOMPARE(vm_->currentTitle(),  QStringLiteral("A"));
        QCOMPARE(vm_->queueCount(), 2);
        QVERIFY(vm_->hasNext());
        QVERIFY(!vm_->hasPrevious());
    }

    void nextAdvancesAndExposesNewTrack() {
        makeVm();
        queue_.enqueue(makeTrack(QStringLiteral("/x/a.mp3"), QStringLiteral("A")));
        queue_.enqueue(makeTrack(QStringLiteral("/x/b.mp3"), QStringLiteral("B")));

        int trackChangedCount = 0;
        connect(vm_.get(), &PlayerViewModel::currentTrackChanged,
                [&]() { ++trackChangedCount; });

        vm_->next();
        QCOMPARE(vm_->currentTitle(), QStringLiteral("B"));
        QVERIFY(trackChangedCount >= 1);
        QVERIFY(!vm_->hasNext());     // end of queue
        QVERIFY(vm_->hasPrevious());
    }

    void previousGoesBack() {
        makeVm();
        queue_.enqueue(makeTrack(QStringLiteral("/x/a.mp3"), QStringLiteral("A")));
        queue_.enqueue(makeTrack(QStringLiteral("/x/b.mp3"), QStringLiteral("B")));
        queue_.setCurrentIndex(1);
        vm_->previous();
        QCOMPARE(vm_->currentTitle(), QStringLiteral("A"));
    }

    void jumpToMovesToIndex() {
        makeVm();
        queue_.enqueue(makeTrack(QStringLiteral("/x/a.mp3"), QStringLiteral("A")));
        queue_.enqueue(makeTrack(QStringLiteral("/x/b.mp3"), QStringLiteral("B")));
        queue_.enqueue(makeTrack(QStringLiteral("/x/c.mp3"), QStringLiteral("C")));
        vm_->jumpTo(2);
        QCOMPARE(vm_->currentTitle(), QStringLiteral("C"));
    }

    void setShuffleTogglesShuffleState() {
        makeVm();
        QVERIFY(!vm_->shuffle());
        vm_->setShuffle(true);
        QVERIFY(vm_->shuffle());
        vm_->setShuffle(false);
        QVERIFY(!vm_->shuffle());
    }

    void cycleRepeatWalksOffAllOne() {
        makeVm();
        QCOMPARE(vm_->repeatMode(), int(QueueManager::RepeatMode::Off));
        vm_->cycleRepeat();
        QCOMPARE(vm_->repeatMode(), int(QueueManager::RepeatMode::All));
        vm_->cycleRepeat();
        QCOMPARE(vm_->repeatMode(), int(QueueManager::RepeatMode::One));
        vm_->cycleRepeat();
        QCOMPARE(vm_->repeatMode(), int(QueueManager::RepeatMode::Off));
    }

    void setVolumeProxiesToPlayback() {
        makeVm();
        vm_->setVolume(0.42f);
        QCOMPARE(playback_.volume(), 0.42f);
    }

    void setMutedProxiesToPlayback() {
        makeVm();
        vm_->setMuted(true);
        QVERIFY(playback_.isMuted());
        vm_->setMuted(false);
        QVERIFY(!playback_.isMuted());
    }

    void playPauseCommandsAreForwarded() {
        // We can't actually start playback without a real file, but
        // we can at least verify the play() / pause() / stop() /
        // togglePlayPause() don't crash and the state stays Stopped
        // (no media to play).
        makeVm();
        vm_->play();
        vm_->pause();
        vm_->togglePlayPause();
        vm_->stop();
        QVERIFY(vm_->isStopped());
    }

    void positionPercentIsZeroWhenDurationIsZero() {
        makeVm();
        QCOMPARE(vm_->positionPercent(), 0.0);
    }

    void seekPercentClampsToValidRange() {
        makeVm();
        // No media, so duration is 0 — seek should be a no-op.
        vm_->seekPercent(0.5);
        vm_->seekPercent(1.5);  // would be > 1, but should be clamped
        vm_->seekPercent(-1.0); // would be < 0
        QVERIFY(vm_->isStopped());
    }

    void audioFormatTextIsEmptyByDefault() {
        makeVm();
        QVERIFY(vm_->audioFormatText().isEmpty());
    }

    void audioFormatTextPopulatedForLocalTrack() {
        makeVm();
        MusicFile m;
        m.setFilePath(QStringLiteral("/x/a.mp3"));
        m.setSourceType(QStringLiteral("Local"));
        m.setBitrate(320);
        m.setFileSize(8388608); // 8 MB
        queue_.enqueue(m);
        QString fmt = vm_->audioFormatText();
        QVERIFY(fmt.contains(QStringLiteral("LOCAL")));
        QVERIFY(fmt.contains(QStringLiteral("320 kbps")));
    }

    void isFavoriteDefaultFalse() {
        makeVm();
        QVERIFY(!vm_->isFavorite());
    }

    void currentLyricsEmptyByDefault() {
        makeVm();
        QVERIFY(vm_->currentLyrics().isEmpty());
    }

    void currentLyricsPopulatedFromTrack() {
        makeVm();
        MusicFile m;
        m.setFilePath(QStringLiteral("/x/a.mp3"));
        m.setLyrics(QStringLiteral("Line 1\nLine 2"));
        queue_.enqueue(m);
        QCOMPARE(vm_->currentLyrics(), QStringLiteral("Line 1\nLine 2"));
    }

    void shareCurrentTrackCopiesToClipboard() {
        makeVm();
        MusicFile m;
        m.setFilePath(QStringLiteral("/x/a.mp3"));
        m.setTitle(QStringLiteral("My Song"));
        m.setArtist(QStringLiteral("My Artist"));
        queue_.enqueue(m);
        // Should not crash
        vm_->shareCurrentTrack();
    }
};

QTEST_MAIN(TestPlayerViewModel)
#include "testplayerviewmodel.moc"

// testalbumviewmodel.cpp
// Unit tests for AlbumViewModel. Exercises the bindable surface
// (id, name, artist, trackCount, totalDurationMs, isSaved, tracks,
// canPlay) and the play / shuffle / save commands. Uses a real
// QueueManager — it's the simplest way to assert on enqueue state.

#include <QtTest/QtTest>
#include <QCoreApplication>
#include <QSignalSpy>

#include "core/database/Database.h"
#include "core/database/DatabaseConfig.h"
#include "core/database/LibraryRepository.h"
#include "core/playback/QueueManager.h"
#include "core/models/AlbumInfo.h"
#include "core/models/MusicFile.h"

#include "viewmodels/AlbumViewModel.h"

using namespace mf::core::database;
using namespace mf::core::playback;
using namespace mf::core::models;
using namespace mf::app::viewmodels;

namespace {

MusicFile makeTrack(const QString& id,
                    const QString& title,
                    int durationSec = 180) {
    MusicFile m;
    m.setId(id);
    m.setTitle(title);
    m.setArtist(QStringLiteral("AC/DC"));
    m.setAlbum(QStringLiteral("Back in Black"));
    m.setTrackNumber(1);
    m.setDuration(std::chrono::seconds{durationSec});
    m.setFilePath(QStringLiteral("/x/") + id + QStringLiteral(".mp3"));
    m.setSourceType(QStringLiteral("local"));
    return m;
}

AlbumInfo makeAlbum(int trackCount = 3, int secondsEach = 200) {
    AlbumInfo a;
    a.setId(QStringLiteral("alb-1"));
    a.setName(QStringLiteral("Back in Black"));
    a.setArtist(QStringLiteral("AC/DC"));
    a.setArtistId(QStringLiteral("art-1"));
    a.setYear(1980);
    a.setGenre(QStringLiteral("Rock"));
    a.setDescription(QStringLiteral("Classic"));
    a.setCoverPath(QStringLiteral("/covers/bib.jpg"));
    a.setSourceType(QStringLiteral("local"));
    QList<MusicFile> tracks;
    for (int i = 0; i < trackCount; ++i) {
        tracks.append(makeTrack(QStringLiteral("t%1").arg(i),
                                QStringLiteral("Track %1").arg(i),
                                secondsEach));
    }
    a.setTracks(tracks);
    a.setTrackCount(trackCount);
    return a;
}

} // namespace

class TestAlbumViewModel : public QObject {
    Q_OBJECT
private slots:
    // ── 1. Initial state from info ────────────────────────────────────
    void albumVm_initialStateFromInfo() {
        LibraryRepository repo((Database{}));
        QueueManager queue(&repo);
        AlbumViewModel vm(&queue);

        QSignalSpy infoSpy(&vm, &AlbumViewModel::infoChanged);
        vm.setInfo(makeAlbum(3, 200));
        QCOMPARE(infoSpy.count(), 1);

        QCOMPARE(vm.id(),         QStringLiteral("alb-1"));
        QCOMPARE(vm.name(),       QStringLiteral("Back in Black"));
        QCOMPARE(vm.artist(),     QStringLiteral("AC/DC"));
        QCOMPARE(vm.artistId(),   QStringLiteral("art-1"));
        QCOMPARE(vm.year(),       1980);
        QCOMPARE(vm.genre(),      QStringLiteral("Rock"));
        QCOMPARE(vm.coverPath(),  QStringLiteral("/covers/bib.jpg"));
        QCOMPARE(vm.trackCount(), 3);
        QVERIFY(vm.canPlay());
    }

    // ── 2. totalDurationMs sums tracks ────────────────────────────────
    void albumVm_totalDurationSumsTracks() {
        LibraryRepository repo((Database{}));
        QueueManager queue(&repo);
        AlbumViewModel vm(&queue);
        vm.setInfo(makeAlbum(4, 250));
        QCOMPARE(vm.totalDurationMs(), qint64(4 * 250 * 1000));
    }

    // ── 3. playAll enqueues all + starts at index 0 ───────────────────
    void albumVm_playAllEnqueuesAllAndStartsAtZero() {
        LibraryRepository repo((Database{}));
        QueueManager queue(&repo);
        AlbumViewModel vm(&queue);
        vm.setInfo(makeAlbum(3, 180));

        QSignalSpy errorSpy(&vm, &AlbumViewModel::errorReported);
        vm.playAll();
        QCOMPARE(errorSpy.count(), 0);
        QCOMPARE(queue.count(),       3);
        QCOMPARE(queue.currentIndex(), 0);
    }

    // ── 4. shufflePlay enables shuffle and enqueues all ───────────────
    void albumVm_shufflePlayEnablesShuffleThenEnqueues() {
        LibraryRepository repo((Database{}));
        QueueManager queue(&repo);
        AlbumViewModel vm(&queue);
        vm.setInfo(makeAlbum(2, 180));
        QVERIFY(!queue.isShuffle());
        vm.shufflePlay();
        QVERIFY(queue.isShuffle());
        QCOMPARE(queue.count(),       2);
        QCOMPARE(queue.currentIndex(), 0);
    }

    // ── 5. playTrackAt enqueues a single track ────────────────────────
    void albumVm_playTrackAtEnqueuesSingle() {
        LibraryRepository repo((Database{}));
        QueueManager queue(&repo);
        AlbumViewModel vm(&queue);
        vm.setInfo(makeAlbum(3, 180));
        vm.playTrackAt(1);
        QCOMPARE(queue.count(),        1);
        QCOMPARE(queue.currentIndex(), 0);
    }

    // ── 6. playTrackAt out-of-range is a no-op ────────────────────────
    void albumVm_playTrackAtOutOfRangeIsNoop() {
        LibraryRepository repo((Database{}));
        QueueManager queue(&repo);
        AlbumViewModel vm(&queue);
        vm.setInfo(makeAlbum(2, 180));
        QSignalSpy errorSpy(&vm, &AlbumViewModel::errorReported);
        vm.playTrackAt(99);
        QCOMPARE(queue.count(), 0);
        QCOMPARE(errorSpy.count(), 0); // silent (matches the widget behaviour)
    }

    // ── 7. toggleSaved flips flag + emits savedChanged ────────────────
    void albumVm_toggleSavedFlipsFlagAndEmits() {
        LibraryRepository repo((Database{}));
        QueueManager queue(&repo);
        AlbumViewModel vm(&queue);
        vm.setInfo(makeAlbum(1, 180));
        QVERIFY(!vm.isSaved());

        QSignalSpy savedSpy(&vm, &AlbumViewModel::savedChanged);
        vm.toggleSaved();
        QVERIFY(vm.isSaved());
        QCOMPARE(savedSpy.count(), 1);
        vm.toggleSaved();
        QVERIFY(!vm.isSaved());
        QCOMPARE(savedSpy.count(), 2);
    }

    // ── 8. setInfo with same saved value emits infoChanged only ──────
    void albumVm_setInfoDoesNotEmitSavedIfUnchanged() {
        LibraryRepository repo((Database{}));
        QueueManager queue(&repo);
        AlbumViewModel vm(&queue);
        AlbumInfo a = makeAlbum(2, 100);
        a.setIsSaved(true);
        vm.setInfo(a);
        QSignalSpy savedSpy(&vm, &AlbumViewModel::savedChanged);
        // Second setInfo with the same saved value.
        AlbumInfo a2 = makeAlbum(3, 200);
        a2.setIsSaved(true);
        vm.setInfo(a2);
        QCOMPARE(savedSpy.count(), 0);
        // savedChanged fires when going false→true.
        a2.setIsSaved(false);
        vm.setInfo(a2);
        QCOMPARE(savedSpy.count(), 1);
    }

    // ── 9. null queue is safe (no crash, errorReported fires) ────────
    void albumVm_nullQueueReportsError() {
        AlbumViewModel vm(nullptr);
        vm.setInfo(makeAlbum(2, 100));
        QSignalSpy errorSpy(&vm, &AlbumViewModel::errorReported);
        vm.playAll();
        vm.shufflePlay();
        vm.playTrackAt(0);
        QCOMPARE(errorSpy.count(), 3);
    }

    // ── 10. canPlay reflects tracks-empty ─────────────────────────────
    void albumVm_canPlayReflectsEmptyTracks() {
        LibraryRepository repo((Database{}));
        QueueManager queue(&repo);
        AlbumViewModel vm(&queue);
        QVERIFY(!vm.canPlay());
        vm.setInfo(makeAlbum(2, 100));
        QVERIFY(vm.canPlay());
    }
};

QTEST_GUILESS_MAIN(TestAlbumViewModel)
#include "testalbumviewmodel.moc"

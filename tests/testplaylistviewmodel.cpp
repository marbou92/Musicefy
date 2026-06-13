// testplaylistviewmodel.cpp
// Unit tests for PlaylistViewModel. Covers the bindable surface,
// the play / shuffle / track commands, and the local edit operations
// (add / remove / reorder / rename). Read-only playlists
// (youTubePlaylistId set) reject edit operations.

#include <QtTest/QtTest>
#include <QCoreApplication>
#include <QSignalSpy>

#include "core/database/Database.h"
#include "core/database/DatabaseConfig.h"
#include "core/database/LibraryRepository.h"
#include "core/playback/QueueManager.h"
#include "core/models/MusicFile.h"
#include "core/models/PlaylistInfo.h"

#include "viewmodels/PlaylistViewModel.h"

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
    m.setArtist(QStringLiteral("Band"));
    m.setAlbum(QStringLiteral("LP"));
    m.setTrackNumber(1);
    m.setDuration(std::chrono::seconds{durationSec});
    m.setFilePath(QStringLiteral("/x/") + id + QStringLiteral(".mp3"));
    m.setSourceType(QStringLiteral("local"));
    return m;
}

PlaylistInfo makePlaylist(int trackCount = 3, int secondsEach = 200,
                          const QString& ytId = QString()) {
    PlaylistInfo p;
    p.setId(QStringLiteral("pl-1"));
    p.setName(QStringLiteral("My Mix"));
    p.setDescription(QStringLiteral("Top picks"));
    p.setSourceType(QStringLiteral("local"));
    p.setYouTubePlaylistId(ytId);   // empty = editable
    QList<MusicFile> tracks;
    for (int i = 0; i < trackCount; ++i) {
        tracks.append(makeTrack(QStringLiteral("t%1").arg(i),
                                QStringLiteral("Track %1").arg(i),
                                secondsEach));
    }
    p.setTracks(tracks);
    p.setTrackCount(trackCount);
    qint64 totalSec = qint64(trackCount) * secondsEach;
    p.setTotalDuration(std::chrono::seconds{totalSec});
    return p;
}

} // namespace

class TestPlaylistViewModel : public QObject {
    Q_OBJECT
private slots:
    // ── 1. Initial state from info ────────────────────────────────────
    void playlistVm_initialStateFromInfo() {
        LibraryRepository repo((Database{}));
        QueueManager queue(&repo);
        PlaylistViewModel vm(&queue);
        QSignalSpy infoSpy(&vm, &PlaylistViewModel::infoChanged);
        QSignalSpy tracksSpy(&vm, &PlaylistViewModel::tracksChanged);
        vm.setInfo(makePlaylist(3, 200));
        QCOMPARE(infoSpy.count(), 1);
        QCOMPARE(tracksSpy.count(), 1);
        QCOMPARE(vm.id(),          QStringLiteral("pl-1"));
        QCOMPARE(vm.name(),        QStringLiteral("My Mix"));
        QCOMPARE(vm.description(), QStringLiteral("Top picks"));
        QCOMPARE(vm.trackCount(),  3);
        QCOMPARE(vm.totalDurationMs(), qint64(3 * 200 * 1000));
        QVERIFY(vm.canPlay());
        QVERIFY(vm.canEdit());
    }

    // ── 2. playAll enqueues all ───────────────────────────────────────
    void playlistVm_playAllEnqueuesAll() {
        LibraryRepository repo((Database{}));
        QueueManager queue(&repo);
        PlaylistViewModel vm(&queue);
        vm.setInfo(makePlaylist(3, 180));
        vm.playAll();
        QCOMPARE(queue.count(),       3);
        QCOMPARE(queue.currentIndex(), 0);
    }

    // ── 3. shufflePlay enables shuffle and enqueues ───────────────────
    void playlistVm_shufflePlayEnablesShuffle() {
        LibraryRepository repo((Database{}));
        QueueManager queue(&repo);
        PlaylistViewModel vm(&queue);
        vm.setInfo(makePlaylist(2, 180));
        QVERIFY(!queue.isShuffle());
        vm.shufflePlay();
        QVERIFY(queue.isShuffle());
        QCOMPARE(queue.count(), 2);
    }

    // ── 4. playTrackAt enqueues a single track ────────────────────────
    void playlistVm_playTrackAtEnqueuesSingle() {
        LibraryRepository repo((Database{}));
        QueueManager queue(&repo);
        PlaylistViewModel vm(&queue);
        vm.setInfo(makePlaylist(3, 180));
        vm.playTrackAt(0);
        QCOMPARE(queue.count(), 1);
    }

    // ── 5. addTrack appends + emits signals ──────────────────────────
    void playlistVm_addTrackAppendsAndEmits() {
        LibraryRepository repo((Database{}));
        QueueManager queue(&repo);
        PlaylistViewModel vm(&queue);
        vm.setInfo(makePlaylist(1, 100));
        QSignalSpy tracksSpy(&vm, &PlaylistViewModel::tracksChanged);
        QSignalSpy addSpy(&vm,    &PlaylistViewModel::trackAdded);
        vm.addTrack(makeTrack(QStringLiteral("t-new"), QStringLiteral("New"), 60));
        QCOMPARE(vm.trackCount(), 2);
        QCOMPARE(vm.tracks().last().id(), QStringLiteral("t-new"));
        QCOMPARE(tracksSpy.count(), 1);
        QCOMPARE(addSpy.count(),    1);
        QCOMPARE(addSpy.last().first().toInt(), 1);
    }

    // ── 6. removeTrackAt removes + emits ─────────────────────────────
    void playlistVm_removeTrackAtRemovesAndEmits() {
        LibraryRepository repo((Database{}));
        QueueManager queue(&repo);
        PlaylistViewModel vm(&queue);
        vm.setInfo(makePlaylist(3, 100));
        QSignalSpy tracksSpy(&vm, &PlaylistViewModel::tracksChanged);
        QSignalSpy rmSpy(&vm,    &PlaylistViewModel::trackRemoved);
        vm.removeTrackAt(1);
        QCOMPARE(vm.trackCount(), 2);
        QVERIFY(vm.rowForTrackId(QStringLiteral("t2")) == -1);
        QCOMPARE(tracksSpy.count(), 1);
        QCOMPARE(rmSpy.count(),    1);
        QCOMPARE(rmSpy.last().first().toInt(), 1);
    }

    // ── 7. reorder moves tracks + emits ──────────────────────────────
    void playlistVm_reorderTracksMovesAndEmits() {
        LibraryRepository repo((Database{}));
        QueueManager queue(&repo);
        PlaylistViewModel vm(&queue);
        vm.setInfo(makePlaylist(3, 100));
        QSignalSpy tracksSpy(&vm, &PlaylistViewModel::tracksChanged);
        QSignalSpy movedSpy(&vm,  &PlaylistViewModel::trackMoved);
        QVERIFY(vm.reorder(0, 2));
        QCOMPARE(vm.tracks()[0].id(), QStringLiteral("t1"));
        QCOMPARE(vm.tracks()[2].id(), QStringLiteral("t0"));
        QCOMPARE(tracksSpy.count(), 1);
        QCOMPARE(movedSpy.count(),  1);
        QCOMPARE(movedSpy.last().first().toInt(),  0);
        QCOMPARE(movedSpy.last().last().toInt(),   2);
    }

    // ── 8. rename updates name + emits ───────────────────────────────
    void playlistVm_renameUpdatesNameAndEmits() {
        LibraryRepository repo((Database{}));
        QueueManager queue(&repo);
        PlaylistViewModel vm(&queue);
        vm.setInfo(makePlaylist(1, 100));
        QSignalSpy infoSpy(&vm,  &PlaylistViewModel::infoChanged);
        QSignalSpy nameSpy(&vm,  &PlaylistViewModel::nameChanged);
        vm.rename(QStringLiteral("Renamed Mix"));
        QCOMPARE(vm.name(), QStringLiteral("Renamed Mix"));
        QCOMPARE(infoSpy.count(), 1);
        QCOMPARE(nameSpy.count(), 1);
        QCOMPARE(nameSpy.last().first().toString(),
                 QStringLiteral("Renamed Mix"));
    }

    // ── 9. rename with empty/whitespace is ignored ───────────────────
    void playlistVm_renameWithEmptyIsIgnored() {
        LibraryRepository repo((Database{}));
        QueueManager queue(&repo);
        PlaylistViewModel vm(&queue);
        vm.setInfo(makePlaylist(1, 100));
        QSignalSpy infoSpy(&vm, &PlaylistViewModel::infoChanged);
        vm.rename(QStringLiteral(""));
        vm.rename(QStringLiteral("   "));
        QCOMPARE(vm.name(), QStringLiteral("My Mix"));
        QCOMPARE(infoSpy.count(), 0);
    }

    // ── 10. read-only playlist (youTubePlaylistId) rejects edits ─────
    void playlistVm_youtubePlaylistIsReadOnly() {
        LibraryRepository repo((Database{}));
        QueueManager queue(&repo);
        PlaylistViewModel vm(&queue);
        vm.setInfo(makePlaylist(1, 100,
                                QStringLiteral("PL-remote-1")));
        QVERIFY(!vm.canEdit());
        QSignalSpy errorSpy(&vm, &PlaylistViewModel::errorReported);
        vm.addTrack(makeTrack(QStringLiteral("t-x"), QStringLiteral("X")));
        vm.removeTrackAt(0);
        vm.reorder(0, 0);
        vm.rename(QStringLiteral("nope"));
        QCOMPARE(errorSpy.count(), 4);
        QCOMPARE(vm.trackCount(), 1);
        QCOMPARE(vm.name(), QStringLiteral("My Mix"));
    }

    // ── 11. rowForTrackId returns -1 for missing id ─────────────────
    void playlistVm_rowForTrackIdReturnsMinusOne() {
        LibraryRepository repo((Database{}));
        QueueManager queue(&repo);
        PlaylistViewModel vm(&queue);
        vm.setInfo(makePlaylist(2, 100));
        QCOMPARE(vm.rowForTrackId(QStringLiteral("t0")), 0);
        QCOMPARE(vm.rowForTrackId(QStringLiteral("t1")), 1);
        QCOMPARE(vm.rowForTrackId(QStringLiteral("nope")), -1);
    }

    // ── 12. null queue is safe + errorReported fires ────────────────
    void playlistVm_nullQueueReportsError() {
        PlaylistViewModel vm(nullptr);
        vm.setInfo(makePlaylist(2, 100));
        QSignalSpy errorSpy(&vm, &PlaylistViewModel::errorReported);
        vm.playAll();
        vm.shufflePlay();
        vm.playTrackAt(0);
        QCOMPARE(errorSpy.count(), 3);
    }

    // ── 13. addTrack + removeTrackAt keep VM consistent ─────────────
    void playlistVm_addThenRemoveMaintainsConsistency() {
        LibraryRepository repo((Database{}));
        QueueManager queue(&repo);
        PlaylistViewModel vm(&queue);
        vm.setInfo(makePlaylist(2, 100));
        // Add a track
        vm.addTrack(makeTrack(QStringLiteral("t-new"), QStringLiteral("New"), 50));
        QCOMPARE(vm.trackCount(), 3);
        QCOMPARE(vm.tracks()[2].id(), QStringLiteral("t-new"));
        // Remove the middle track
        vm.removeTrackAt(1);
        QCOMPARE(vm.trackCount(), 2);
        QCOMPARE(vm.tracks()[0].id(), QStringLiteral("t0"));
        QCOMPARE(vm.tracks()[1].id(), QStringLiteral("t-new"));
    }

    // ── 14. reorder with invalid indices is no-op ──────────────────
    void playlistVm_reorderInvalidIndicesIsNoop() {
        LibraryRepository repo((Database{}));
        QueueManager queue(&repo);
        PlaylistViewModel vm(&queue);
        vm.setInfo(makePlaylist(3, 100));
        QSignalSpy tracksSpy(&vm, &PlaylistViewModel::tracksChanged);
        // Out-of-range from
        QVERIFY(!vm.reorder(-1, 0));
        QVERIFY(!vm.reorder(5, 0));
        // Out-of-range to
        QVERIFY(!vm.reorder(0, 5));
        // Same index
        QVERIFY(!vm.reorder(1, 1));
        QCOMPARE(tracksSpy.count(), 0);
        QCOMPARE(vm.trackCount(), 3);
    }
};

QTEST_GUILESS_MAIN(TestPlaylistViewModel)
#include "testplaylistviewmodel.moc"

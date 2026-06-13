// testartistviewmodel.cpp
// Unit tests for ArtistViewModel. Exercises the bindable surface
// (id, name, description, subscriberCount, isFollowed, topTracks,
// albums, canPlay) and the play / shuffle / follow / load commands.
// Uses a fake IBrowseService that captures callbacks so the tests
// can drive the async resolution synchronously.

#include <QtTest/QtTest>
#include <QCoreApplication>
#include <QSignalSpy>

#include "core/database/Database.h"
#include "core/database/DatabaseConfig.h"
#include "core/database/LibraryRepository.h"
#include "core/playback/QueueManager.h"
#include "core/services/BrowseService.h"
#include "core/models/AlbumInfo.h"
#include "core/models/ArtistInfo.h"
#include "core/models/BrowseSection.h"
#include "core/models/MusicFile.h"

#include "viewmodels/ArtistViewModel.h"

using namespace mf::core::database;
using namespace mf::core::playback;
using namespace mf::core::models;
using namespace mf::core::services;
using namespace mf::core::interfaces;
using namespace mf::app::viewmodels;

namespace {

class FakeBrowseService : public IBrowseService {
public:
    // Captured callbacks so the test can fire them manually.
    IBrowseService::ArtistCallback     lastArtistCb;
    IBrowseService::TrackListCallback  lastTopCb;
    IBrowseService::SectionListCallback lastAlbumsCb;

    // Canned return data.
    ArtistInfo          nextArtist;
    QList<MusicFile>    nextTopTracks;
    QList<BrowseSection> nextAlbums;

    void loadHome(HomeCallback) override {}
    void loadCharts(QString, SectionListCallback) override {}
    void loadMoodsAndGenres(QString, SectionListCallback) override {}
    void loadNewReleases(QString, SectionListCallback) override {}
    void loadPlaylists(QString, SectionListCallback) override {}

    void fetchArtist(QString, ArtistCallback cb) override { lastArtistCb = std::move(cb); }
    void fetchAlbum(QString, AlbumCallback) override {}
    void fetchAlbumTracks(QString, TrackListCallback) override {}
    void fetchArtistAlbums(QString, SectionListCallback cb) override { lastAlbumsCb = std::move(cb); }
    void fetchArtistTopTracks(QString, TrackListCallback cb) override { lastTopCb = std::move(cb); }
};

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

ArtistInfo makeArtist(qint64 subs = 12345) {
    ArtistInfo a;
    a.setId(QStringLiteral("art-1"));
    a.setName(QStringLiteral("Test Artist"));
    a.setCoverPath(QStringLiteral("/covers/artist.jpg"));
    a.setDescription(QStringLiteral("An artist for unit tests."));
    a.setSubscriberCount(subs);
    a.setIsFollowed(false);
    return a;
}

} // namespace

class TestArtistViewModel : public QObject {
    Q_OBJECT
private slots:
    // ── 1. loadById populates metadata from fake service ──────────────
    void artistVm_loadByIdPopulatesMetadata() {
        FakeBrowseService browse;
        LibraryRepository repo((Database{}));
        QueueManager queue(&repo);
        ArtistViewModel vm(&browse, &queue, nullptr);

        QVERIFY(browse.lastArtistCb);
        vm.loadById(QStringLiteral("art-1"));
        QVERIFY(vm.isLoading());
        QVERIFY(browse.lastAlbumsCb);
        QVERIFY(browse.lastTopCb);

        browse.nextArtist = makeArtist(99000);
        browse.lastArtistCb(makeArtist(99000));

        QCOMPARE(vm.id(),              QStringLiteral("art-1"));
        QCOMPARE(vm.name(),            QStringLiteral("Test Artist"));
        QCOMPARE(vm.coverPath(),       QStringLiteral("/covers/artist.jpg"));
        QCOMPARE(vm.description(),     QStringLiteral("An artist for unit tests."));
        QCOMPARE(vm.subscriberCount(), qint64(99000));
        QVERIFY(!vm.isFollowed());
    }

    // ── 2. loadById populates top tracks + albums ─────────────────────
    void artistVm_loadByIdPopulatesTopAndAlbums() {
        FakeBrowseService browse;
        LibraryRepository repo((Database{}));
        QueueManager queue(&repo);
        ArtistViewModel vm(&browse, &queue, nullptr);

        vm.loadById(QStringLiteral("art-1"));
        QList<MusicFile> top;
        top.append(makeTrack("t1", "Hit A"));
        top.append(makeTrack("t2", "Hit B"));
        browse.lastTopCb(top);

        BrowseSection albumSec;
        albumSec.setTitle(QStringLiteral("Albums"));
        albumSec.setItems({ makeTrack("alb-1", "Album One"),
                            makeTrack("alb-2", "Album Two") });
        browse.lastAlbumsCb(QList<BrowseSection>{ albumSec });

        QCOMPARE(vm.topTracks().size(), 2);
        QCOMPARE(vm.albums().size(),    2);
        QVERIFY(vm.canPlay());
        QVERIFY(!vm.isLoading());
    }

    // ── 3. loadById out-of-range id with null service is a no-op ─────
    void artistVm_nullBrowseReportsError() {
        LibraryRepository repo((Database{}));
        QueueManager queue(&repo);
        ArtistViewModel vm(nullptr, &queue, nullptr);

        QSignalSpy errorSpy(&vm, &ArtistViewModel::errorReported);
        vm.loadById(QStringLiteral("x"));
        QCOMPARE(errorSpy.count(), 1);
    }

    // ── 4. loadById with empty id is rejected ─────────────────────────
    void artistVm_emptyIdReportsError() {
        FakeBrowseService browse;
        LibraryRepository repo((Database{}));
        QueueManager queue(&repo);
        ArtistViewModel vm(&browse, &queue, nullptr);

        QSignalSpy errorSpy(&vm, &ArtistViewModel::errorReported);
        vm.loadById(QString());
        QCOMPARE(errorSpy.count(), 1);
        QVERIFY(!browse.lastArtistCb);
    }

    // ── 5. playAll enqueues top tracks + starts at 0 ──────────────────
    void artistVm_playAllEnqueuesTopTracks() {
        FakeBrowseService browse;
        LibraryRepository repo((Database{}));
        QueueManager queue(&repo);
        ArtistViewModel vm(&browse, &queue, nullptr);

        vm.loadById(QStringLiteral("art-1"));
        QList<MusicFile> top;
        top.append(makeTrack("t1", "A"));
        top.append(makeTrack("t2", "B"));
        top.append(makeTrack("t3", "C"));
        browse.lastTopCb(top);

        QSignalSpy errorSpy(&vm, &ArtistViewModel::errorReported);
        vm.playAll();
        QCOMPARE(errorSpy.count(), 0);
        QCOMPARE(queue.count(),        3);
        QCOMPARE(queue.currentIndex(), 0);
    }

    // ── 6. shufflePlay enables shuffle and enqueues ───────────────────
    void artistVm_shufflePlayEnablesShuffle() {
        FakeBrowseService browse;
        LibraryRepository repo((Database{}));
        QueueManager queue(&repo);
        ArtistViewModel vm(&browse, &queue, nullptr);

        vm.loadById(QStringLiteral("art-1"));
        browse.lastTopCb({ makeTrack("t1", "A"), makeTrack("t2", "B") });
        QVERIFY(!queue.isShuffle());

        vm.shufflePlay();
        QVERIFY(queue.isShuffle());
        QCOMPARE(queue.count(), 2);
    }

    // ── 7. playTrackAt out of range is a silent no-op ─────────────────
    void artistVm_playTrackAtOutOfRangeIsNoop() {
        FakeBrowseService browse;
        LibraryRepository repo((Database{}));
        QueueManager queue(&repo);
        ArtistViewModel vm(&browse, &queue, nullptr);

        vm.loadById(QStringLiteral("art-1"));
        browse.lastTopCb({ makeTrack("t1", "A") });
        QSignalSpy errorSpy(&vm, &ArtistViewModel::errorReported);
        vm.playTrackAt(99);
        QCOMPARE(queue.count(), 0);
        QCOMPARE(errorSpy.count(), 0);
    }

    // ── 8. toggleFollowed flips flag and emits ────────────────────────
    void artistVm_toggleFollowedFlipsAndEmits() {
        FakeBrowseService browse;
        LibraryRepository repo((Database{}));
        QueueManager queue(&repo);
        ArtistViewModel vm(&browse, &queue, nullptr);

        vm.loadById(QStringLiteral("art-1"));
        browse.lastArtistCb(makeArtist());

        QVERIFY(!vm.isFollowed());
        QSignalSpy spy(&vm, &ArtistViewModel::followedChanged);
        vm.toggleFollowed();
        QVERIFY(vm.isFollowed());
        QCOMPARE(spy.count(), 1);
        vm.toggleFollowed();
        QVERIFY(!vm.isFollowed());
        QCOMPARE(spy.count(), 2);
    }

    // ── 9. openAlbumAt emits openAlbumRequested with id ───────────────
    void artistVm_openAlbumAtEmitsSignal() {
        FakeBrowseService browse;
        LibraryRepository repo((Database{}));
        QueueManager queue(&repo);
        ArtistViewModel vm(&browse, &queue, nullptr);

        vm.loadById(QStringLiteral("art-1"));
        BrowseSection sec;
        sec.setItems({ makeTrack("alb-1", "Album One"),
                       makeTrack("alb-2", "Album Two") });
        browse.lastAlbumsCb(QList<BrowseSection>{ sec });

        QSignalSpy spy(&vm, &ArtistViewModel::openAlbumRequested);
        vm.openAlbumAt(1);
        QCOMPARE(spy.count(), 1);
        const auto args = spy.takeFirst();
        const auto album = args.at(0).value<AlbumInfo>();
        QCOMPARE(album.id(),   QStringLiteral("alb-2"));
        QCOMPARE(album.name(), QStringLiteral("Album Two"));
    }

    // ── 10. null queue is safe + errorReported fires ──────────────────
    void artistVm_nullQueueReportsError() {
        FakeBrowseService browse;
        ArtistViewModel vm(&browse, nullptr, nullptr);

        vm.loadById(QStringLiteral("art-1"));
        browse.lastTopCb({ makeTrack("t1", "A") });

        QSignalSpy errorSpy(&vm, &ArtistViewModel::errorReported);
        vm.playAll();
        vm.shufflePlay();
        vm.playTrackAt(0);
        QCOMPARE(errorSpy.count(), 3);
    }
};

QTEST_GUILESS_MAIN(TestArtistViewModel)
#include "testartistviewmodel.moc"

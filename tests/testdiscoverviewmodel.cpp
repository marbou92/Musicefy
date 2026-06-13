// testdiscoverviewmodel.cpp
// Unit tests for DiscoverViewModel. Exercises the three feeds
// (charts, moods, new releases) and the per-feed play / shuffle
// commands. Uses a fake IBrowseService that captures callbacks so
// the test can drive the async resolution synchronously.

#include <QtTest/QtTest>
#include <QCoreApplication>
#include <QSignalSpy>

#include "core/database/Database.h"
#include "core/database/DatabaseConfig.h"
#include "core/database/LibraryRepository.h"
#include "core/playback/QueueManager.h"
#include "core/services/BrowseService.h"
#include "core/models/BrowseSection.h"
#include "core/models/MusicFile.h"

#include "viewmodels/DiscoverViewModel.h"

using namespace mf::core::database;
using namespace mf::core::playback;
using namespace mf::core::models;
using namespace mf::core::services;
using namespace mf::core::interfaces;
using namespace mf::app::viewmodels;

namespace {

class FakeBrowseService : public IBrowseService {
public:
    IBrowseService::SectionListCallback lastCharts;
    IBrowseService::SectionListCallback lastMoods;
    IBrowseService::SectionListCallback lastNew;

    QList<BrowseSection> nextCharts;
    QList<BrowseSection> nextMoods;
    QList<BrowseSection> nextNew;

    void loadHome(HomeCallback) override {}
    void loadCharts(QString, SectionListCallback cb) override        { lastCharts = std::move(cb); }
    void loadMoodsAndGenres(QString, SectionListCallback cb) override { lastMoods  = std::move(cb); }
    void loadNewReleases(QString, SectionListCallback cb) override   { lastNew    = std::move(cb); }
    void loadPlaylists(QString, SectionListCallback) override {}

    void fetchArtist(QString, ArtistCallback) override {}
    void fetchAlbum(QString, AlbumCallback) override {}
    void fetchAlbumTracks(QString, TrackListCallback) override {}
    void fetchArtistAlbums(QString, SectionListCallback) override {}
    void fetchArtistTopTracks(QString, TrackListCallback) override {}
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

BrowseSection makeSection(const QString& title,
                          std::initializer_list<QString> trackIds) {
    BrowseSection s;
    s.setTitle(title);
    QList<MusicFile> items;
    for (const QString& id : trackIds) {
        items.append(makeTrack(id, id));
    }
    s.setItems(items);
    return s;
}

} // namespace

class TestDiscoverViewModel : public QObject {
    Q_OBJECT
private slots:
    // ── 1. Initial state: not loading, no content ─────────────────────
    void discoverVm_initialState() {
        FakeBrowseService browse;
        LibraryRepository repo((Database{}));
        QueueManager queue(&repo);
        DiscoverViewModel vm(&browse, &queue, nullptr);

        QVERIFY(!vm.isLoading());
        QVERIFY(!vm.hasContent());
        QCOMPARE(vm.chartCount(),      0);
        QCOMPARE(vm.moodCount(),       0);
        QCOMPARE(vm.newReleaseCount(), 0);
    }

    // ── 2. refresh fires all three feeds and clears loading ───────────
    void discoverVm_refreshFiresAllFeeds() {
        FakeBrowseService browse;
        LibraryRepository repo((Database{}));
        QueueManager queue(&repo);
        DiscoverViewModel vm(&browse, &queue, nullptr);

        vm.refresh();
        QVERIFY(vm.isLoading());
        QVERIFY(browse.lastCharts);
        QVERIFY(browse.lastMoods);
        QVERIFY(browse.lastNew);

        browse.lastCharts({ makeSection(QStringLiteral("Top"),
                                         { "c1", "c2", "c3" }) });
        QVERIFY(vm.isLoading());

        browse.lastMoods({ makeSection(QStringLiteral("Chill"),
                                       { "m1", "m2" }) });
        QVERIFY(vm.isLoading());

        browse.lastNew({ makeSection(QStringLiteral("Fresh"),
                                     { "n1" }) });
        QVERIFY(!vm.isLoading());
        QVERIFY(vm.hasContent());
    }

    // ── 3. refresh populates all three feeds from service ─────────────
    void discoverVm_refreshPopulatesFeeds() {
        FakeBrowseService browse;
        LibraryRepository repo((Database{}));
        QueueManager queue(&repo);
        DiscoverViewModel vm(&browse, &queue, nullptr);

        vm.refresh();
        browse.lastCharts({ makeSection(QStringLiteral("Top"),
                                         { "c1", "c2" }) });
        browse.lastMoods({ makeSection(QStringLiteral("Workout"),
                                       { "m1" }) });
        browse.lastNew({ makeSection(QStringLiteral("Friday"),
                                     { "n1", "n2", "n3" }) });

        QCOMPARE(vm.chartCount(),      1);
        QCOMPARE(vm.moodCount(),       1);
        QCOMPARE(vm.newReleaseCount(), 1);
        QCOMPARE(vm.charts().at(0).items().size(),       2);
        QCOMPARE(vm.moods().at(0).items().size(),        1);
        QCOMPARE(vm.newReleases().at(0).items().size(),  3);
    }

    // ── 4. refreshIfStale refreshes once, then no-ops ─────────────────
    void discoverVm_refreshIfStaleOnlyRefreshesOnce() {
        FakeBrowseService browse;
        LibraryRepository repo((Database{}));
        QueueManager queue(&repo);
        DiscoverViewModel vm(&browse, &queue, nullptr);

        vm.refreshIfStale();
        QVERIFY(browse.lastCharts);
        browse.lastCharts({});
        browse.lastMoods({});
        browse.lastNew({});

        // Second call should NOT re-fetch.
        browse.lastCharts = nullptr;
        browse.lastMoods  = nullptr;
        browse.lastNew    = nullptr;
        vm.refreshIfStale();
        QVERIFY(!browse.lastCharts);
    }

    // ── 5. playAllFromCharts enqueues all items in section 0 ──────────
    void discoverVm_playAllFromChartsEnqueues() {
        FakeBrowseService browse;
        LibraryRepository repo((Database{}));
        QueueManager queue(&repo);
        DiscoverViewModel vm(&browse, &queue, nullptr);

        vm.refresh();
        browse.lastCharts({ makeSection(QStringLiteral("Top"),
                                         { "c1", "c2", "c3" }) });
        browse.lastMoods({});
        browse.lastNew({});

        QSignalSpy errorSpy(&vm, &DiscoverViewModel::errorReported);
        vm.playAllFromCharts(0);
        QCOMPARE(errorSpy.count(),    0);
        QCOMPARE(queue.count(),        3);
        QCOMPARE(queue.currentIndex(), 0);
    }

    // ── 6. shuffleFromMoods enables shuffle ───────────────────────────
    void discoverVm_shuffleFromMoodsEnablesShuffle() {
        FakeBrowseService browse;
        LibraryRepository repo((Database{}));
        QueueManager queue(&repo);
        DiscoverViewModel vm(&browse, &queue, nullptr);

        vm.refresh();
        browse.lastCharts({});
        browse.lastMoods({ makeSection(QStringLiteral("Workout"),
                                       { "m1", "m2" }) });
        browse.lastNew({});

        QVERIFY(!queue.isShuffle());
        vm.shuffleFromMoods(0);
        QVERIFY(queue.isShuffle());
        QCOMPARE(queue.count(), 2);
    }

    // ── 7. playTrackInNewReleases enqueues a single track ─────────────
    void discoverVm_playTrackInNewReleasesEnqueuesSingle() {
        FakeBrowseService browse;
        LibraryRepository repo((Database{}));
        QueueManager queue(&repo);
        DiscoverViewModel vm(&browse, &queue, nullptr);

        vm.refresh();
        browse.lastCharts({});
        browse.lastMoods({});
        browse.lastNew({ makeSection(QStringLiteral("Friday"),
                                     { "n1", "n2", "n3" }) });

        vm.playTrackInNewReleases(0, 1);
        QCOMPARE(queue.count(),        1);
        QCOMPARE(queue.currentIndex(), 0);
    }

    // ── 8. out-of-range indices are silent no-ops ─────────────────────
    void discoverVm_outOfRangeIsNoop() {
        FakeBrowseService browse;
        LibraryRepository repo((Database{}));
        QueueManager queue(&repo);
        DiscoverViewModel vm(&browse, &queue, nullptr);

        vm.refresh();
        browse.lastCharts({ makeSection(QStringLiteral("Top"),
                                         { "c1" }) });
        browse.lastMoods({});
        browse.lastNew({});

        QSignalSpy errorSpy(&vm, &DiscoverViewModel::errorReported);
        vm.playAllFromCharts(99);
        vm.playTrackInCharts(0, 99);
        QCOMPARE(queue.count(),     0);
        QCOMPARE(errorSpy.count(),  0);
    }

    // ── 9. null browse service reports error on refresh ───────────────
    void discoverVm_nullBrowseReportsError() {
        LibraryRepository repo((Database{}));
        QueueManager queue(&repo);
        DiscoverViewModel vm(nullptr, &queue, nullptr);

        QSignalSpy errorSpy(&vm, &DiscoverViewModel::errorReported);
        vm.refresh();
        QCOMPARE(errorSpy.count(), 1);
    }

    // ── 10. null queue is safe + errorReported fires ──────────────────
    void discoverVm_nullQueueReportsError() {
        FakeBrowseService browse;
        DiscoverViewModel vm(&browse, nullptr, nullptr);

        vm.refresh();
        browse.lastCharts({ makeSection(QStringLiteral("Top"),
                                         { "c1", "c2" }) });
        browse.lastMoods({});
        browse.lastNew({});

        QSignalSpy errorSpy(&vm, &DiscoverViewModel::errorReported);
        vm.playAllFromCharts(0);
        vm.shuffleFromCharts(0);
        vm.playTrackInCharts(0, 0);
        QCOMPARE(errorSpy.count(), 3);
    }
};

QTEST_GUILESS_MAIN(TestDiscoverViewModel)
#include "testdiscoverviewmodel.moc"

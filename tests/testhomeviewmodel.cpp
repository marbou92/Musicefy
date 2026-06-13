// testhomeviewmodel.cpp
// Verifies the Home page's view model: the four sections read from
// the LibraryRepository, the empty-state flag is correct, content
// refreshes on tracksChanged, and the play/open commands enqueue
// the right tracks / fire the right navigation events.

#include <QtTest/QtTest>
#include <QCoreApplication>
#include <QDir>
#include <QFile>
#include <QSettings>
#include <QSignalSpy>
#include <QTemporaryDir>

#include "database/Database.h"
#include "database/DatabaseConfig.h"
#include "database/LibraryRepository.h"
#include "playback/QueueManager.h"
#include "services/LibraryService.h"
#include "services/NavigationService.h"
#include "services/ToastService.h"

#include "viewmodels/HomeViewModel.h"

#include "models/MusicFile.h"
#include "models/PlaylistInfo.h"

#include <QUuid>

using namespace mf::core::database;
using namespace mf::core::playback;
using namespace mf::core::services;
using namespace mf::core::models;
using namespace mf::app::viewmodels;

class TestHomeViewModel : public QObject {
    Q_OBJECT

private:
    std::unique_ptr<QTemporaryDir>      tmpRoot_;
    std::unique_ptr<Database>           db_;
    std::unique_ptr<LibraryRepository>  repo_;
    std::unique_ptr<QueueManager>       queue_;
    std::unique_ptr<LibraryService>     libSvc_;
    std::unique_ptr<ToastService>       toasts_;
    std::unique_ptr<NavigationService>  nav_;
    std::unique_ptr<HomeViewModel>      vm_;
    int                                testCounter_ = 0;

    DatabaseConfig makeConfig(const QString& tag) {
        DatabaseConfig c;
        QString dir = tmpRoot_->path() + QStringLiteral("/") + tag;
        QDir().mkpath(dir);
        QString migrationDir = dir + QStringLiteral("/migrations");
        QDir().mkpath(migrationDir);
        QString src = QCoreApplication::applicationDirPath()
                    + QStringLiteral("/migrations/0001_initial_schema.sql");
        QString dst = migrationDir + QStringLiteral("/0001_initial_schema.sql");
        if (!QFile::exists(dst)) {
            QFile::remove(dst);
            if (!QFile::copy(src, dst)) {
                qFatal("Could not copy migration from %s to %s",
                       qUtf8Printable(src), qUtf8Printable(dst));
            }
        }
        c.setFilePath(dir + QStringLiteral("/musicefy.db"));
        c.setMigrationFiles({migrationDir});
        return c;
    }

    void rebuild() {
        ++testCounter_;
        QSettings().clear();
        vm_.reset();
        libSvc_.reset();
        nav_.reset();
        toasts_.reset();
        queue_.reset();
        repo_.reset();
        db_.reset();

        DatabaseConfig cfg = makeConfig(QStringLiteral("home_%1").arg(testCounter_));
        db_      = std::make_unique<Database>(cfg);
        QVERIFY(db_->open());
        repo_    = std::make_unique<LibraryRepository>(*db_);
        queue_   = std::make_unique<QueueManager>();
        libSvc_  = std::make_unique<LibraryService>(repo_.get());
        toasts_  = std::make_unique<ToastService>();
        nav_     = std::make_unique<NavigationService>();
        vm_      = std::make_unique<HomeViewModel>(
            repo_.get(), queue_.get(), libSvc_.get(),
            toasts_.get(), nav_.get());
    }

    MusicFile makeTrack(const QString& filePath,
                        const QString& title,
                        const QString& artist,
                        const QString& album,
                        qint64         lastPlayed = 0) {
        MusicFile m;
        m.setId(QUuid::createUuid().toString(QUuid::WithoutBraces));
        m.setFilePath(filePath);
        m.setTitle(title);
        m.setArtist(artist);
        m.setAlbum(album);
        m.setSourceType(QStringLiteral("local"));
        m.setDuration(std::chrono::seconds{ 180 });
        if (lastPlayed > 0) {
            m.setLastPlayed(QDateTime::fromSecsSinceEpoch(lastPlayed));
        }
        return m;
    }

    PlaylistInfo makePlaylist(const QString& name, int trackCount) {
        PlaylistInfo p;
        p.setId(QUuid::createUuid().toString(QUuid::WithoutBraces));
        p.setName(name);
        p.setTrackCount(trackCount);
        p.setSourceType(QStringLiteral("local"));
        p.setCreatedAt(QDateTime::currentDateTime());
        return p;
    }

private slots:
    void initTestCase() {
        QCoreApplication::setOrganizationName(QStringLiteral("MusicefyTest"));
        QCoreApplication::setApplicationName(QStringLiteral("homeviewmodel"));
        QSettings().clear();
        tmpRoot_ = std::make_unique<QTemporaryDir>();
        QVERIFY(tmpRoot_->isValid());
    }
    void cleanupTestCase() {
        vm_.reset();
        libSvc_.reset();
        nav_.reset();
        toasts_.reset();
        queue_.reset();
        repo_.reset();
        db_.reset();
        tmpRoot_.reset();
        QSettings().clear();
    }
    void init() { rebuild(); }
    void cleanup() { QSettings().clear(); }

    // ── Empty state ──────────────────────────────────────────────────

    void emptyLibraryIsReported() {
        QCOMPARE(vm_->recentlyPlayedCount(), 0);
        QCOMPARE(vm_->favouritesCount(), 0);
        QCOMPARE(vm_->playlistsCount(), 0);
        QVERIFY(vm_->libraryIsEmpty());
    }

    void emptyAfterFoldersButNoTracks() {
        // Folders without any tracks are also "empty" from the user's
        // perspective.
        QVERIFY(libSvc_->addFolder(tmpRoot_->path() + QStringLiteral("/empty")));
        QVERIFY(vm_->libraryIsEmpty());
    }

    // ── Section data ─────────────────────────────────────────────────

    void recentlyPlayedSortedByLastPlayedDesc() {
        repo_->upsertTrack(makeTrack("/a.mp3", "A", "X", "Y", 100));
        repo_->upsertTrack(makeTrack("/b.mp3", "B", "X", "Y", 300));
        repo_->upsertTrack(makeTrack("/c.mp3", "C", "X", "Y", 200));
        // d is unplayed, must not appear.
        repo_->upsertTrack(makeTrack("/d.mp3", "D", "X", "Y", 0));

        vm_->refresh();
        const auto r = vm_->recentlyPlayed();
        QCOMPARE(r.size(), 3);
        QCOMPARE(r[0].title(), QStringLiteral("B"));
        QCOMPARE(r[1].title(), QStringLiteral("C"));
        QCOMPARE(r[2].title(), QStringLiteral("A"));
    }

    void quickAccessCappedAtFour() {
        for (int i = 0; i < 8; ++i) {
            repo_->upsertTrack(makeTrack(
                QStringLiteral("/t%1.mp3").arg(i),
                QStringLiteral("T%1").arg(i),
                QStringLiteral("A"),
                QStringLiteral("Alb"),
                1000 - i));
        }
        vm_->refresh();
        QCOMPARE(vm_->quickAccess().size(), HomeViewModel::kQuickAccessMax);
        // Most recent first.
        QCOMPARE(vm_->quickAccess().first().title(), QStringLiteral("T0"));
    }

    void favouritesIncludeAllStarredTracks() {
        repo_->upsertTrack(makeTrack("/a.mp3", "A", "X", "Y"));
        repo_->upsertTrack(makeTrack("/b.mp3", "B", "X", "Y"));
        repo_->upsertTrack(makeTrack("/c.mp3", "C", "X", "Y"));
        repo_->toggleFavourite("/a.mp3");
        repo_->toggleFavourite("/c.mp3");

        vm_->refresh();
        QCOMPARE(vm_->favouritesCount(), 2);
        QList<QString> names;
        for (const auto& t : vm_->favourites()) names << t.title();
        QVERIFY(names.contains(QStringLiteral("A")));
        QVERIFY(names.contains(QStringLiteral("C")));
        QVERIFY(!names.contains(QStringLiteral("B")));
    }

    void favouritesAreCappedAtTwelve() {
        for (int i = 0; i < 20; ++i) {
            auto t = makeTrack(
                QStringLiteral("/f%1.mp3").arg(i),
                QStringLiteral("F%1").arg(i),
                QStringLiteral("A"),
                QStringLiteral("Alb"));
            repo_->upsertTrack(t);
            repo_->toggleFavourite(t.filePath());
        }
        vm_->refresh();
        QCOMPARE(vm_->favouritesCount(), HomeViewModel::kFavouritesMax);
    }

    void playlistsAreListed() {
        repo_->upsertPlaylist(makePlaylist(QStringLiteral("Mix A"), 5));
        repo_->upsertPlaylist(makePlaylist(QStringLiteral("Mix B"), 9));
        repo_->upsertPlaylist(makePlaylist(QStringLiteral("Workout"), 12));
        vm_->refresh();
        QCOMPARE(vm_->playlistsCount(), 3);
        QList<QString> names;
        for (const auto& p : vm_->playlists()) names << p.name();
        QVERIFY(names.contains(QStringLiteral("Mix A")));
        QVERIFY(names.contains(QStringLiteral("Mix B")));
        QVERIFY(names.contains(QStringLiteral("Workout")));
    }

    // ── Auto-refresh on tracksChanged ────────────────────────────────

    void contentRefreshesWhenServiceEmitsTracksChanged() {
        QSignalSpy spy(vm_.get(), &HomeViewModel::contentChanged);
        repo_->upsertTrack(makeTrack("/x.mp3", "X", "Y", "Z", 999));
        // Bypass the service — emit tracksChanged ourselves as if
        // a scan had just completed.
        emit libSvc_->tracksChanged();
        QVERIFY(spy.count() >= 1);
        QCOMPARE(vm_->recentlyPlayedCount(), 1);
    }

    void toggleFavouriteReloads() {
        repo_->upsertTrack(makeTrack("/z.mp3", "Z", "Y", "X"));
        vm_->refresh();
        QCOMPARE(vm_->favouritesCount(), 0);

        QSignalSpy spy(vm_.get(), &HomeViewModel::contentChanged);
        vm_->toggleFavourite(QStringLiteral("/z.mp3"));
        QCOMPARE(vm_->favouritesCount(), 1);
        QCOMPARE(spy.count(), 1);
    }

    // ── Commands ─────────────────────────────────────────────────────

    void playTrackEnqueuesAndStartsAtIndex() {
        repo_->upsertTrack(makeTrack("/a.mp3", "A", "X", "Y", 100));
        repo_->upsertTrack(makeTrack("/b.mp3", "B", "X", "Y", 200));
        repo_->upsertTrack(makeTrack("/c.mp3", "C", "X", "Y", 300));
        vm_->playTrack(QStringLiteral("/b.mp3"));

        QCOMPARE(queue_->count(), 3);
        QCOMPARE(queue_->currentIndex(), 1);
        QCOMPARE(queue_->currentTrack().filePath(), QStringLiteral("/b.mp3"));
    }

    void playTrackUnknownPathDoesNothing() {
        repo_->upsertTrack(makeTrack("/a.mp3", "A", "X", "Y", 100));
        vm_->playTrack(QStringLiteral("/nope.mp3"));
        QCOMPARE(queue_->count(), 0);
    }

    void playAllFromQuickAccessClearsAndEnqueues() {
        for (int i = 0; i < 3; ++i) {
            repo_->upsertTrack(makeTrack(
                QStringLiteral("/q%1.mp3").arg(i),
                QStringLiteral("Q%1").arg(i),
                QStringLiteral("A"),
                QStringLiteral("X"),
                100 + i));
        }
        vm_->refresh();
        vm_->playAllFromQuickAccess();
        QCOMPARE(queue_->count(), 3);
        QCOMPARE(queue_->currentIndex(), 0);
    }

    void playAllFromFavouritesClearsAndEnqueues() {
        for (int i = 0; i < 3; ++i) {
            auto t = makeTrack(
                QStringLiteral("/f%1.mp3").arg(i),
                QStringLiteral("F%1").arg(i),
                QStringLiteral("A"),
                QStringLiteral("X"));
            repo_->upsertTrack(t);
            repo_->toggleFavourite(t.filePath());
        }
        vm_->refresh();
        vm_->playAllFromFavourites();
        QCOMPARE(queue_->count(), 3);
        QCOMPARE(queue_->currentIndex(), 0);
    }

    // ── Navigation ───────────────────────────────────────────────────

    void openPlaylistAtFiresNavigationRequest() {
        auto p = makePlaylist(QStringLiteral("My Mix"), 4);
        repo_->upsertPlaylist(p);
        vm_->refresh();
        QSignalSpy spy(nav_.get(), &NavigationService::playlistNavigationRequested);

        vm_->openPlaylistAt(0);
        QCOMPARE(spy.count(), 1);
        auto args = spy.takeFirst();
        QCOMPARE(args.at(0).value<PlaylistInfo>().name(), QStringLiteral("My Mix"));
    }

    void openPlaylistAtOutOfRangeDoesNothing() {
        auto p = makePlaylist(QStringLiteral("One"), 1);
        repo_->upsertPlaylist(p);
        vm_->refresh();
        QSignalSpy spy(nav_.get(), &NavigationService::playlistNavigationRequested);
        vm_->openPlaylistAt(99);
        QCOMPARE(spy.count(), 0);
    }

    // ── Greeting ─────────────────────────────────────────────────────

    void greetingIsNotEmpty() {
        QVERIFY(!vm_->greeting().isEmpty());
        // Any of the four valid greetings.
        const QString g = vm_->greeting();
        QVERIFY(g == QStringLiteral("Good morning")
             || g == QStringLiteral("Good afternoon")
             || g == QStringLiteral("Good evening")
             || g == QStringLiteral("Good night"));
    }
};

QTEST_GUILESS_MAIN(TestHomeViewModel)
#include "testhomeviewmodel.moc"

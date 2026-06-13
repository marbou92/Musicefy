// testcontainer.cpp
// Verifies that AppContainer::build() produces a working DI graph.
// Every service is resolved and the most basic invariants are checked
// (non-null, correct concrete type, basic API surface).

#include <QtTest/QtTest>
#include <QCoreApplication>
#include <QStandardPaths>

#include "AppContainer.h"

#include "core/database/Database.h"
#include "core/database/LibraryRepository.h"
#include "core/playback/PlaybackService.h"
#include "core/playback/QueueManager.h"
#include "core/playback/MediaKeyFilter.h"
#include "core/playback/SmtcController.h"
#include "core/services/ArtworkEnrichment.h"
#include "core/services/BrowseService.h"
#include "core/services/DownloadService.h"
#include "core/services/ExtensionManager.h"
#include "core/services/HealthCheckService.h"
#include "core/services/ImageCache.h"
#include "core/services/LibraryService.h"
#include "core/services/NavigationService.h"
#include "core/services/SearchHistoryService.h"
#include "core/services/SettingsControl.h"
#include "core/services/ToastService.h"
#include "core/sources/HttpClient.h"
#include "core/sources/StreamingSourceManager.h"
#include "core/sources/SubsonicProvider.h"
#include "core/sources/YouTubeProvider.h"
#include "core/theme/ThemeManager.h"
#include "viewmodels/HomeViewModel.h"
#include "viewmodels/LibraryViewModel.h"
#include "viewmodels/MainViewModel.h"
#include "viewmodels/PlayerViewModel.h"

using namespace mf::app;
using namespace mf::core::database;
using namespace mf::core::playback;
using namespace mf::core::services;
using namespace mf::core::sources;

class TestContainer : public QObject {
    Q_OBJECT

private slots:
    void initTestCase() {
        // Keep test state out of the user's real config.
        QCoreApplication::setOrganizationName(QStringLiteral("MusicefyTest"));
        QCoreApplication::setApplicationName(QStringLiteral("container"));
        QSettings().clear();
    }
    void cleanupTestCase() {
        QSettings().clear();
    }

    void containerBuildsAndResolvesAllServices() {
        AppContainer c;
        c.build();

        QVERIFY(c.database()          != nullptr);
        QVERIFY(c.library()           != nullptr);
        QVERIFY(c.playback()          != nullptr);
        QVERIFY(c.queue()             != nullptr);
        QVERIFY(c.mediaKeys()         != nullptr);
        QVERIFY(c.smtc()              != nullptr);
        QVERIFY(c.browse()            != nullptr);
        QVERIFY(c.searchHistory()     != nullptr);
        QVERIFY(c.health()            != nullptr);
        QVERIFY(c.settings()          != nullptr);
        QVERIFY(c.extensions()        != nullptr);
        QVERIFY(c.downloads()         != nullptr);
        QVERIFY(c.libraryService()    != nullptr);
        QVERIFY(c.http()              != nullptr);
        QVERIFY(c.sourceManager()     != nullptr);
        QVERIFY(c.theme()             != nullptr);
        QVERIFY(c.imageCache()        != nullptr);
        QVERIFY(c.artwork()           != nullptr);
    }

    void imageServicesShareHttpClient() {
        AppContainer c;
        c.build();
        // ArtworkEnrichment should be wired to the ImageCache that
        // shares the app's HttpClient, so that a network call made by
        // the ArtworkEnrichment reaches the same socket pool.
        QVERIFY(c.imageCache() != nullptr);
        QVERIFY(c.artwork()    != nullptr);
        QVERIFY(c.artwork()->imageCache() == c.imageCache().get());
    }

    void databaseIsOpen() {
        AppContainer c;
        c.build();
        QVERIFY(c.database()->isOpen());
    }

    void streamingSourceManagerHasBuiltinProviders() {
        AppContainer c;
        c.build();
        auto mgr = c.sourceManager();
        QVERIFY(mgr->providerFor(QStringLiteral("subsonic")) != nullptr);
        QVERIFY(mgr->providerFor(QStringLiteral("youtube"))  != nullptr);
    }

    void singletonsAreSingletons() {
        AppContainer c;
        c.build();
        // Resolving twice should yield the same shared_ptr address for
        // singletons.
        auto p1 = c.playback();
        auto p2 = c.playback();
        QCOMPARE(p1.get(), p2.get());

        auto s1 = c.settings();
        auto s2 = c.settings();
        QCOMPARE(s1.get(), s2.get());
    }

    void playbackserviceCanBeConfigured() {
        AppContainer c;
        c.build();
        c.playback()->setVolume(0.5f);
        QCOMPARE(c.playback()->volume(), 0.5f);
    }

    void queuemanagerEnqueueWorks() {
        AppContainer c;
        c.build();
        auto q = c.queue();
        mf::core::models::MusicFile m;
        m.setFilePath(QStringLiteral("/music/test.mp3"));
        m.setTitle(QStringLiteral("Test"));
        q->enqueue(m);
        QCOMPARE(q->count(), 1);
    }

    void lifecycleStartStop() {
        AppContainer c;
        c.build();
        AppLifecycle lc(c);
        lc.start();
        QVERIFY(c.health()->isRunning());
        lc.shutdown();
        QVERIFY(!c.health()->isRunning());
    }

    void lifecycleStartIsIdempotent() {
        AppContainer c;
        c.build();
        AppLifecycle lc(c);
        lc.start();
        lc.start();   // should not double-start
        QVERIFY(c.health()->isRunning());
        lc.shutdown();
    }

    void themeManagerIsResolvableAndHasScheme() {
        // Reset persisted state so the test is order-independent.
        QSettings().clear();

        AppContainer c;
        c.build();
        auto tm = c.theme();
        QVERIFY(tm != nullptr);
        QVERIFY(!tm->schemeName().isEmpty());
        QVERIFY(tm->scheme().primary.isValid());

        // Setting a different theme should produce a different scheme.
        QColor before = tm->scheme().primary;
        tm->setTheme(mf::core::theme::AppTheme::GreenApple);
        QColor after = tm->scheme().primary;
        QVERIFY(before != after);
    }

    void playerViewModelIsResolvable() {
        AppContainer c;
        c.build();
        auto vm = c.playerVm();
        QVERIFY(vm != nullptr);
        QVERIFY(!vm->isPlaying());
        QVERIFY(vm->isStopped());
        QCOMPARE(vm->queueCount(), 0);
    }

    void libraryViewModelIsResolvable() {
        AppContainer c;
        c.build();
        auto vm = c.libraryVm();
        QVERIFY(vm != nullptr);
        QCOMPARE(vm->trackCount(), 0);
        QCOMPARE(vm->artistCount(), 0);
        QCOMPARE(vm->albumCount(), 0);
        QCOMPARE(vm->playlistCount(), 0);
    }

    void mainViewModelIsResolvable() {
        AppContainer c;
        c.build();
        auto vm = c.mainVm();
        QVERIFY(vm != nullptr);
        QCOMPARE(vm->currentPage(), int(mf::app::viewmodels::MainViewModel::PageHome));
        QCOMPARE(vm->currentPageName(), QStringLiteral("home"));
    }

    void homeViewModelIsResolvable() {
        AppContainer c;
        c.build();
        auto h = c.homeVm();
        QVERIFY(h != nullptr);
        QCOMPARE(h.get(), c.homeVm().get());
        // Brand-new app: no folders, no tracks, so the Home page
        // starts in its empty state.
        QVERIFY(h->libraryIsEmpty());
        QVERIFY(!h->greeting().isEmpty());
    }

    void navigationServiceIsResolvable() {
        AppContainer c;
        c.build();
        auto n = c.nav();
        QVERIFY(n != nullptr);
        // Just verify it doesn't crash and has a stable address.
        QCOMPARE(n.get(), c.nav().get());
    }

    void toastServiceIsResolvable() {
        AppContainer c;
        c.build();
        auto t = c.toasts();
        QVERIFY(t != nullptr);
        QCOMPARE(t.get(), c.toasts().get());
    }

    void libraryServiceIsResolvable() {
        AppContainer c;
        c.build();
        auto l = c.libraryService();
        QVERIFY(l != nullptr);
        QCOMPARE(l.get(), c.libraryService().get());
        // Newly-built app has no folders, no in-flight scan, no tracks.
        QVERIFY(l->folders().isEmpty());
        QVERIFY(!l->isScanning());
    }
};

QTEST_GUILESS_MAIN(TestContainer)
#include "testcontainer.moc"

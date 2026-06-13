// testservices.cpp
// Unit tests for the service layer.
// SettingsControl, ExtensionManager, BrowseService: pure logic, fully
// testable.
// SearchHistoryService, HealthCheckService, DownloadService: need a
// streaming source manager and/or a database, so we use the in-memory
// SQLite pattern from testdatabase.cpp and a lightweight mock source.

#include <QtTest/QtTest>
#include <QCoreApplication>
#include <QStandardPaths>
#include <QDir>
#include <QFile>
#include <QFileInfo>
#include <QSettings>
#include <QTemporaryDir>

#include "services/SettingsControl.h"
#include "services/ExtensionManager.h"
#include "services/BrowseService.h"
#include "services/SearchHistoryService.h"
#include "services/HealthCheckService.h"
#include "services/DownloadService.h"

#include "interfaces/IStreamingSourceManager.h"
#include "interfaces/IMusicSourceProvider.h"
#include "interfaces/IMusicSourceSession.h"

#include "models/StreamingSource.h"
#include "models/ExtensionManifest.h"
#include "models/SourceHealthState.h"

#include "database/Database.h"
#include "database/DatabaseConfig.h"
#include "database/LibraryRepository.h"

#include "sources/StreamingSourceManager.h"

using namespace mf::core::services;
using namespace mf::core::models;
using namespace mf::core::database;
using namespace mf::core::interfaces;
using namespace mf::core::sources;

namespace {

// A test provider/session that doesn't do any network I/O.
class FakeSession : public QObject, public IMusicSourceSession {
    Q_OBJECT
public:
    FakeSession(QString id, bool healthy, QObject* parent = nullptr)
        : QObject(parent), id_(id), healthy_(healthy) {}

    QString sourceType() const override { return QStringLiteral("fake"); }
    QString sourceId()   const override { return id_; }
    bool    isHealthy()  const override { return healthy_; }

    void searchTracks(QString, int, ResultCallback cb, StringCallback) override { if (cb) cb({}); }
    void fetchStreamUrl(QString, StringCallback ok, StringCallback) override { if (ok) ok(QStringLiteral("https://x/y.mp3")); }
    void fetchLyrics(QString, StringCallback ok, StringCallback) override { if (ok) ok(QString()); }
    void fetchCover(QString, BytesCallback, StringCallback) override {}
    void ping(BoolCallback cb) override { if (cb) cb(healthy_, healthy_ ? QString() : QStringLiteral("nope")); }

private:
    QString id_;
    bool    healthy_;
};

class FakeProvider : public QObject, public IMusicSourceProvider {
    Q_OBJECT
public:
    FakeProvider(bool healthy) : healthy_(healthy) {}

    QString sourceType() const override { return QStringLiteral("fake"); }
    QString displayName() const override { return QStringLiteral("Fake"); }
    QList<SourceConfigField> configFields() const override { return {}; }

    std::unique_ptr<IMusicSourceSession> createSession(
        const StreamingSource& source) const override {
        return std::make_unique<FakeSession>(source.id(), healthy_);
    }

private:
    bool healthy_;
};

} // namespace

class TestServices : public QObject {
    Q_OBJECT
private:
    QTemporaryDir tempDir_;
    DatabaseConfig makeDbConfig() {
        DatabaseConfig c;
        c.setFilePath(tempDir_.path() + QStringLiteral("/test.db"));
        QDir().mkpath(tempDir_.path() + QStringLiteral("/migrations"));
        c.setMigrationFiles({ tempDir_.path() + QStringLiteral("/migrations") });
        return c;
    }
    void copyMigration(const Database& /*unused*/) {
        QString src = QCoreApplication::applicationDirPath() + QStringLiteral("/migrations/0001_initial_schema.sql");
        QString dst = tempDir_.path() + QStringLiteral("/migrations/0001_initial_schema.sql");
        if (QFile::exists(dst)) return;
        if (!QFile::exists(src)) {
            qFatal("Could not find migration at %s", qUtf8Printable(src));
        }
        QFile::remove(dst);
        if (!QFile::copy(src, dst)) {
            qFatal("Could not copy migration from %s to %s", qUtf8Printable(src), qUtf8Printable(dst));
        }
    }

private slots:
    void init() {
        QVERIFY(tempDir_.isValid());
    }

    // ── SettingsControl ────────────────────────────────────────────────
    void settingsSetGet() {
        SettingsControl s(QStringLiteral("MusicefyTest"),
                          QStringLiteral("settingsSetGet") + QString::number(QDateTime::currentMSecsSinceEpoch()));
        s.set(QStringLiteral("foo"), QStringLiteral("bar"));
        s.sync();
        QCOMPARE(s.get(QStringLiteral("foo")).toString(), QStringLiteral("bar"));
        QVERIFY(s.contains(QStringLiteral("foo")));
        s.remove(QStringLiteral("foo"));
        QVERIFY(!s.contains(QStringLiteral("foo")));
    }

    void settingsGetAs() {
        SettingsControl s(QStringLiteral("MusicefyTest"),
                          QStringLiteral("getAs") + QString::number(QDateTime::currentMSecsSinceEpoch()));
        s.set(QStringLiteral("volume"), 0.75);
        s.set(QStringLiteral("enabled"), true);
        s.set(QStringLiteral("name"),    QStringLiteral("alice"));
        s.sync();
        QCOMPARE(*s.getAs<double>(QStringLiteral("volume")), 0.75);
        QCOMPARE(*s.getAs<bool>(QStringLiteral("enabled")), true);
        QCOMPARE(*s.getAs<QString>(QStringLiteral("name")), QStringLiteral("alice"));
        QVERIFY(!s.getAs<int>(QStringLiteral("missing")).has_value());
    }

    void settingsGetOrDefault() {
        SettingsControl s(QStringLiteral("MusicefyTest"),
                          QStringLiteral("getOrDefault") + QString::number(QDateTime::currentMSecsSinceEpoch()));
        QCOMPARE(s.getOrDefault<int>(QStringLiteral("missing"), 42), 42);
        s.set(QStringLiteral("present"), 7);
        QCOMPARE(s.getOrDefault<int>(QStringLiteral("present"), 42), 7);
    }

    // ── ExtensionManager ───────────────────────────────────────────────
    void extensionManagerEmpty() {
        ExtensionManager mgr;
        QCOMPARE(mgr.allExtensions().size(), 0);
        QCOMPARE(mgr.enabledExtensions().size(), 0);
        QVERIFY(!mgr.isLoaded(QStringLiteral("any")));
        QVERIFY(mgr.resolveEntrypoint(QStringLiteral("any")) == nullptr);
    }

    void extensionManagerLoadFromEmptyDir() {
        ExtensionManager mgr;
        int doneCount = 0;
        QList<ExtensionManifest> result;
        mgr.loadExtensions(tempDir_.path(),
                           [&](QList<ExtensionManifest> manifests) {
            ++doneCount;
            result = manifests;
        });
        QCOMPARE(doneCount, 1);
        QCOMPARE(result.size(), 0);
    }

    void extensionManagerLoadFromNonexistentDir() {
        ExtensionManager mgr;
        int doneCount = 0;
        mgr.loadExtensions(tempDir_.path() + QStringLiteral("/no-such-dir"),
                           [&](QList<ExtensionManifest>) { ++doneCount; });
        QCOMPARE(doneCount, 1);
    }

    // ── BrowseService ──────────────────────────────────────────────────
    void browseServiceEmptyHome() {
        BrowseService svc;
        int doneCount = 0;
        svc.loadHome([&](QList<HomeSection> secs) {
            ++doneCount;
            QCOMPARE(secs.size(), 0);
        });
        QCOMPARE(doneCount, 1);
    }

    void browseServiceAllCallbacksFire() {
        BrowseService svc;
        int charts = 0, moods = 0, releases = 0, playlists = 0;
        svc.loadCharts(QStringLiteral("local"),
                       [&](QList<BrowseSection>) { ++charts; });
        svc.loadMoodsAndGenres(QStringLiteral("local"),
                               [&](QList<BrowseSection>) { ++moods; });
        svc.loadNewReleases(QStringLiteral("local"),
                            [&](QList<BrowseSection>) { ++releases; });
        svc.loadPlaylists(QStringLiteral("local"),
                          [&](QList<BrowseSection>) { ++playlists; });
        QCOMPARE(charts, 1);
        QCOMPARE(moods, 1);
        QCOMPARE(releases, 1);
        QCOMPARE(playlists, 1);
    }

    // ── SearchHistoryService ───────────────────────────────────────────
    void searchHistoryRoundtrip() {
        DatabaseConfig cfg = makeDbConfig();
        copyMigration(Database{cfg});
        Database db(cfg);
        QVERIFY(db.open());

        LibraryRepository repo(db);
        SearchHistoryService svc(&repo);

        svc.recordSearch(QStringLiteral("foo"), QStringLiteral("local"), 10);
        svc.recordSearch(QStringLiteral("bar"), QStringLiteral("local"), 5);
        svc.recordSearch(QStringLiteral("foobar"), QStringLiteral("local"), 3);

        auto recent = svc.recent(10);
        QCOMPARE(recent.size(), 3);
        // Most recent first.
        QCOMPARE(recent.first().query(), QStringLiteral("foobar"));

        auto sugg = svc.suggestions(QStringLiteral("foo"), 10);
        QCOMPARE(sugg.size(), 2); // "foo" and "foobar"
    }

    void searchHistoryClickBumps() {
        DatabaseConfig cfg = makeDbConfig();
        copyMigration(Database{cfg});
        Database db(cfg);
        QVERIFY(db.open());
        LibraryRepository repo(db);
        SearchHistoryService svc(&repo);
        svc.recordSearch(QStringLiteral("q"), QStringLiteral("local"), 1);
        svc.recordClick(QStringLiteral("q"), QStringLiteral("local"));
        svc.recordClick(QStringLiteral("q"), QStringLiteral("local"));
        auto recent = svc.recent(10);
        QCOMPARE(recent.size(), 1);
        QCOMPARE(recent.first().clickCount(), 2);
    }

    void searchHistoryClear() {
        DatabaseConfig cfg = makeDbConfig();
        copyMigration(Database{cfg});
        Database db(cfg);
        QVERIFY(db.open());
        LibraryRepository repo(db);
        SearchHistoryService svc(&repo);

        svc.recordSearch(QStringLiteral("a"), QStringLiteral("local"), 1);
        svc.recordSearch(QStringLiteral("b"), QStringLiteral("youtube"), 1);
        QCOMPARE(svc.recent(10).size(), 2);

        svc.clearForSource(QStringLiteral("local"));
        QCOMPARE(svc.recent(10).size(), 1);
        QCOMPARE(svc.recent(10).first().sourceType(), QStringLiteral("youtube"));

        svc.clearAll();
        QCOMPARE(svc.recent(10).size(), 0);
    }

    void searchHistoryNotifies() {
        DatabaseConfig cfg = makeDbConfig();
        copyMigration(Database{cfg});
        Database db(cfg);
        QVERIFY(db.open());
        LibraryRepository repo(db);
        SearchHistoryService svc(&repo);

        int changes = 0;
        svc.setOnHistoryChanged([&](QList<SearchHistory>) { ++changes; });
        svc.recordSearch(QStringLiteral("x"), QStringLiteral("local"), 1);
        svc.recordClick(QStringLiteral("x"), QStringLiteral("local"));
        svc.clearAll();
        QCOMPARE(changes, 3);
    }

    // ── HealthCheckService ─────────────────────────────────────────────
    void healthCheckServiceStartsAndStops() {
        HealthCheckService svc;
        QVERIFY(!svc.isRunning());
        svc.start();
        QVERIFY(svc.isRunning());
        svc.stop();
        QVERIFY(!svc.isRunning());
    }

    void healthCheckServiceCheckAll() {
        StreamingSourceManager mgr;
        mgr.registerProvider(std::make_shared<FakeProvider>(true));

        StreamingSource s;
        s.setId(QStringLiteral("src-1"));
        s.setName(QStringLiteral("Fake"));
        s.setType(QStringLiteral("fake"));
        mgr.addSource(s);

        HealthCheckService hc(&mgr);
        int doneCount = 0;
        QHash<QString, SourceHealthState> result;
        hc.checkAll([&](QHash<QString, SourceHealthState> map) {
            ++doneCount;
            result = map;
        });
        QCOMPARE(doneCount, 1);
        QCOMPARE(result.size(), 1);
        QVERIFY(result.contains(QStringLiteral("src-1")));
        QCOMPARE(result.value(QStringLiteral("src-1")).status(),
                 SourceHealthStatus::Healthy);
    }

    void healthCheckServiceUnhealthySource() {
        StreamingSourceManager mgr;
        mgr.registerProvider(std::make_shared<FakeProvider>(false));

        StreamingSource s;
        s.setId(QStringLiteral("src-bad"));
        s.setName(QStringLiteral("Bad"));
        s.setType(QStringLiteral("fake"));
        mgr.addSource(s);

        HealthCheckService hc(&mgr);
        int doneCount = 0;
        hc.checkSource(QStringLiteral("src-bad"),
                       [&](SourceHealthState state) {
            ++doneCount;
            QCOMPARE(state.status(), SourceHealthStatus::Degraded);
        });
        QCOMPARE(doneCount, 1);
    }

    void healthCheckServiceStateFor() {
        StreamingSourceManager mgr;
        mgr.registerProvider(std::make_shared<FakeProvider>(true));

        StreamingSource s;
        s.setId(QStringLiteral("src-x"));
        s.setName(QStringLiteral("X"));
        s.setType(QStringLiteral("fake"));
        mgr.addSource(s);

        HealthCheckService hc(&mgr);
        hc.checkSource(QStringLiteral("src-x"), nullptr);
        auto st = hc.stateFor(QStringLiteral("src-x"));
        QCOMPARE(st.sourceId(), QStringLiteral("src-x"));
    }

    // ── DownloadService ────────────────────────────────────────────────
    void downloadServiceLocalPathForUnknown() {
        DownloadService svc;
        QVERIFY(svc.localPathFor(QStringLiteral("nonexistent")).isEmpty());
        QVERIFY(!svc.isDownloading(QStringLiteral("nonexistent")));
        QVERIFY(!svc.isDownloaded(QStringLiteral("nonexistent")));
    }

    void downloadServiceRemoveDownloadNoOpIfUnknown() {
        DownloadService svc;
        svc.removeDownload(QStringLiteral("nonexistent")); // no crash
    }

    void downloadServiceCancelNoOpIfUnknown() {
        DownloadService svc;
        svc.cancel(QStringLiteral("nonexistent")); // no crash
    }
};

QTEST_GUILESS_MAIN(TestServices)
#include "testservices.moc"

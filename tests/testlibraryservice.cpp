// testlibraryservice.cpp
// Verifies LibraryService: folder CRUD, persistence, scan progress
// signals, track cleanup on folder removal, and the ILibraryService
// pass-through API.

#include <QtTest/QtTest>
#include <QCoreApplication>
#include <QDir>
#include <QFile>
#include <QSettings>
#include <QSignalSpy>
#include <QStandardPaths>
#include <QTemporaryDir>

#include "database/Database.h"
#include "database/DatabaseConfig.h"
#include "database/LibraryRepository.h"
#include "services/LibraryService.h"

#include "models/MusicFile.h"

using namespace mf::core::database;
using namespace mf::core::services;
using namespace mf::core::models;

class TestLibraryService : public QObject {
    Q_OBJECT

private:
    std::unique_ptr<QTemporaryDir>    tmpRoot_;
    std::unique_ptr<Database>         db_;
    std::unique_ptr<LibraryRepository> repo_;
    std::unique_ptr<LibraryService>   svc_;
    QString                            dbPath_;
    QString                            migrationsDir_;
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

    void writeFakeAudio(const QString& path) {
        QFile f(path);
        if (!f.open(QIODevice::WriteOnly)) return;
        f.write(QByteArray(2048, '\0'));
        f.close();
    }

    QString freshFolder(const QString& tag) {
        QString p = tmpRoot_->path() + QStringLiteral("/") + tag;
        QDir().mkpath(p);
        return p;
    }

    void rebuildService() {
        // Re-create db + repo + service after a settings wipe so each
        // test starts from a clean folder list AND a clean DB file.
        // The unique counter avoids cross-test contamination of the
        // on-disk SQLite database.
        ++testCounter_;
        QSettings().clear();
        svc_.reset();
        repo_.reset();
        db_.reset();
        DatabaseConfig cfg = makeConfig(QStringLiteral("svc_%1").arg(testCounter_));
        db_   = std::make_unique<Database>(cfg);
        QVERIFY(db_->open());
        repo_ = std::make_unique<LibraryRepository>(*db_);
        svc_  = std::make_unique<LibraryService>(repo_.get());
    }

private slots:
    void initTestCase() {
        QCoreApplication::setOrganizationName(QStringLiteral("MusicefyTest"));
        QCoreApplication::setApplicationName(QStringLiteral("libraryservice"));
        QSettings().clear();
        tmpRoot_ = std::make_unique<QTemporaryDir>();
        QVERIFY(tmpRoot_->isValid());
    }
    void cleanupTestCase() {
        svc_.reset();
        repo_.reset();
        db_.reset();
        tmpRoot_.reset();
        QSettings().clear();
    }

    void init() {
        rebuildService();
    }
    void cleanup() {
        QSettings().clear();
    }

    // ── Folder CRUD ─────────────────────────────────────────────────

    void initialStateIsEmpty() {
        QVERIFY(svc_->folders().isEmpty());
        QVERIFY(!svc_->isScanning());
        QCOMPARE(svc_->totalAdded(), 0);
        QCOMPARE(svc_->totalUpdated(), 0);
    }

    void addFolderAcceptsValidDirectory() {
        const QString p = freshFolder(QStringLiteral("musicA"));
        QSignalSpy addedSpy(svc_.get(), &LibraryService::folderAdded);
        QSignalSpy foldersSpy(svc_.get(), &LibraryService::foldersChanged);

        QVERIFY(svc_->addFolder(p));
        QCOMPARE(svc_->folders().size(), 1);
        QCOMPARE(svc_->folders().first(), QDir::cleanPath(p));
        QCOMPARE(addedSpy.count(), 1);
        QCOMPARE(foldersSpy.count(), 1);
        QCOMPARE(addedSpy.first().first().toString(), QDir::cleanPath(p));
    }

    void addFolderRejectsNonExistent() {
        QVERIFY(!svc_->addFolder(QStringLiteral("C:/this/path/is/not/real/anywhere")));
        QVERIFY(svc_->folders().isEmpty());
    }

    void addFolderRejectsRegularFile() {
        const QString p = tmpRoot_->path() + QStringLiteral("/not_a_dir.mp3");
        writeFakeAudio(p);
        QVERIFY(QFile::exists(p));
        QVERIFY(!svc_->addFolder(p));
        QVERIFY(svc_->folders().isEmpty());
    }

    void addFolderRejectsDuplicate() {
        const QString p = freshFolder(QStringLiteral("dup"));
        QVERIFY(svc_->addFolder(p));
        QVERIFY(!svc_->addFolder(p));
        QCOMPARE(svc_->folders().size(), 1);
    }

    void addFolderNormalisesPath() {
        const QString p = freshFolder(QStringLiteral("norm"));
        // Pass a path with redundant separators and "./" parts.
        const QString messy = p + QStringLiteral("/./sub/../");
        QVERIFY(svc_->addFolder(messy));
        QCOMPARE(svc_->folders().size(), 1);
        QCOMPARE(svc_->folders().first(), QDir::cleanPath(p));
    }

    void removeFolderRemovesFromList() {
        const QString p = freshFolder(QStringLiteral("rem"));
        QVERIFY(svc_->addFolder(p));
        QVERIFY(svc_->removeFolder(p));
        QVERIFY(svc_->folders().isEmpty());
    }

    void removeFolderEmitsSignals() {
        const QString p = freshFolder(QStringLiteral("remsig"));
        svc_->addFolder(p);
        QSignalSpy removedSpy(svc_.get(), &LibraryService::folderRemoved);
        QSignalSpy foldersSpy(svc_.get(), &LibraryService::foldersChanged);
        QVERIFY(svc_->removeFolder(p));
        QCOMPARE(removedSpy.count(), 1);
        QCOMPARE(foldersSpy.count(), 2); // one for add, one for remove
    }

    void removeFolderOnUnknownIsNoOp() {
        QVERIFY(!svc_->removeFolder(QStringLiteral("C:/never/added")));
        QVERIFY(svc_->folders().isEmpty());
    }

    // ── Persistence ─────────────────────────────────────────────────

    void folderListPersistsAcrossInstances() {
        const QString p = freshFolder(QStringLiteral("persist"));
        QVERIFY(svc_->addFolder(p));
        QCOMPARE(svc_->folders().size(), 1);

        // Drop the service and bring it back. It must reload the list
        // from QSettings during construction.
        svc_.reset();
        repo_.reset();
        db_.reset();
        db_   = std::make_unique<Database>(makeConfig(QStringLiteral("svc")));
        QVERIFY(db_->open());
        repo_ = std::make_unique<LibraryRepository>(*db_);
        svc_  = std::make_unique<LibraryService>(repo_.get());
        QCOMPARE(svc_->folders().size(), 1);
        QCOMPARE(svc_->folders().first(), QDir::cleanPath(p));
    }

    void staleFoldersArePrunedOnConstruction() {
        // Persist a folder that doesn't exist on disk.
        QSettings s;
        QStringList stale = {QStringLiteral("C:/totally/fake/path/12345")};
        s.setValue(QStringLiteral("library/folders"), stale);
        s.sync();

        // New service should drop the stale entry.
        svc_.reset();
        repo_.reset();
        db_.reset();
        db_   = std::make_unique<Database>(makeConfig(QStringLiteral("svc")));
        QVERIFY(db_->open());
        repo_ = std::make_unique<LibraryRepository>(*db_);
        svc_  = std::make_unique<LibraryService>(repo_.get());
        QVERIFY(svc_->folders().isEmpty());

        // And the pruned list should be written back to QSettings.
        QStringList reread = s.value(QStringLiteral("library/folders")).toStringList();
        QVERIFY(reread.isEmpty());
    }

    // ── Scan lifecycle ──────────────────────────────────────────────

    void scanFiresProgressAndFinishedSignals() {
        const QString p = freshFolder(QStringLiteral("scan1"));
        writeFakeAudio(p + QStringLiteral("/a.mp3"));
        writeFakeAudio(p + QStringLiteral("/b.flac"));
        writeFakeAudio(p + QStringLiteral("/c.ogg"));

        QSignalSpy startedSpy(svc_.get(),  &LibraryService::scanStarted);
        QSignalSpy progressSpy(svc_.get(), &LibraryService::scanProgress);
        QSignalSpy finishedSpy(svc_.get(), &LibraryService::scanFinished);
        QSignalSpy tracksSpy(svc_.get(),   &LibraryService::tracksChanged);

        QVERIFY(svc_->addFolder(p));

        QCOMPARE(startedSpy.count(), 1);
        QVERIFY(progressSpy.count() >= 3);
        QCOMPARE(finishedSpy.count(), 1);
        // The scan is synchronous, so by the time addFolder returns
        // the finished signal has already been delivered.
        QVERIFY(svc_->allTracks().size() >= 3);
        QVERIFY(tracksSpy.count() >= 1);
    }

    void emptyFolderListSkipsScan() {
        QSignalSpy startedSpy(svc_.get(), &LibraryService::scanStarted);
        svc_->rescan();
        QCOMPARE(startedSpy.count(), 0);
    }

    void consecutiveScansBothFire() {
        const QString p = freshFolder(QStringLiteral("scan2"));
        writeFakeAudio(p + QStringLiteral("/a.mp3"));

        // Each scan is synchronous, so by the time the first returns
        // scanning_ is back to false and the next call can run. Two
        // started signals should fire (one per call).
        QSignalSpy startedSpy(svc_.get(), &LibraryService::scanStarted);
        svc_->addFolder(p);                  // scan #1
        svc_->rescan();                      // scan #2
        QCOMPARE(startedSpy.count(), 2);
    }

    // ── removeFolder → track cleanup ────────────────────────────────

    void removeFolderDeletesTracksInThatFolder() {
        const QString p1 = freshFolder(QStringLiteral("tracksA"));
        const QString p2 = freshFolder(QStringLiteral("tracksB"));

        // Pre-seed via the repository so we control the filePath
        // format exactly (forward slashes, predictable). This also
        // avoids running the scanner — we're testing folder-removal
        // cleanup, not the scan itself.
        auto seed = [&](const QString& folder, const QString& name) {
            MusicFile m;
            m.setFilePath(folder + QStringLiteral("/") + name);
            m.setTitle(name);
            m.setSourceType(QStringLiteral("local"));
            repo_->upsertTrack(m);
        };
        seed(p1, QStringLiteral("a.mp3"));
        seed(p1, QStringLiteral("b.mp3"));
        seed(p2, QStringLiteral("c.mp3"));

        QCOMPARE(svc_->allTracks().size(), 3);

        QVERIFY(svc_->removeFolder(p1));
        QList<MusicFile> remaining = svc_->allTracks();
        QCOMPARE(remaining.size(), 1);
        // The remaining track must be from p2, not p1.
        const QString cleanP1 = QDir::cleanPath(p1);
        const QString cleanP2 = QDir::cleanPath(p2);
        const QString cleanRemaining = QDir::cleanPath(remaining.first().filePath());
        QVERIFY2(!cleanRemaining.startsWith(cleanP1),
                 qPrintable(QStringLiteral("Survivor %1 starts with %2")
                            .arg(cleanRemaining, cleanP1)));
        QVERIFY2(cleanRemaining.startsWith(cleanP2),
                 qPrintable(QStringLiteral("Survivor %1 should start with %2")
                            .arg(cleanRemaining, cleanP2)));
    }

    void deleteTracksByPathPrefixOnlyMatchesPath() {
        // Regression test for the prefix-matching bug: a folder named
        // "C:/Music" must NOT also delete "C:/Musical/track.mp3".
        const QString p1 = tmpRoot_->path() + QStringLiteral("/Music");
        const QString p2 = tmpRoot_->path() + QStringLiteral("/Musical");
        QDir().mkpath(p1);
        QDir().mkpath(p2);
        const QString t1 = p1 + QStringLiteral("/a.mp3");
        const QString t2 = p2 + QStringLiteral("/b.mp3");
        writeFakeAudio(t1);
        writeFakeAudio(t2);

        MusicFile m1; m1.setFilePath(t1); m1.setTitle(QStringLiteral("T1"));
        MusicFile m2; m2.setFilePath(t2); m2.setTitle(QStringLiteral("T2"));
        repo_->upsertTrack(m1);
        repo_->upsertTrack(m2);

        const int removed = repo_->deleteTracksByPathPrefix(p1);
        QCOMPARE(removed, 1);
        QVERIFY(!repo_->trackByPath(t1).has_value());
        QVERIFY( repo_->trackByPath(t2).has_value());
    }

    // ── ILibraryService pass-throughs ───────────────────────────────

    void passThroughReturnsFromRepository() {
        MusicFile m;
        m.setFilePath(QStringLiteral("/lib/pass.mp3"));
        m.setTitle(QStringLiteral("Pass"));
        m.setArtist(QStringLiteral("X"));
        m.setAlbum(QStringLiteral("Y"));
        m.setSourceType(QStringLiteral("local"));
        repo_->upsertTrack(m);

        QList<MusicFile> all = svc_->allTracks();
        QVERIFY(!all.isEmpty());
        bool found = false;
        for (const auto& t : all) {
            if (t.filePath() == QStringLiteral("/lib/pass.mp3")) {
                found = true;
                break;
            }
        }
        QVERIFY(found);
    }

    void removeTrackPassesThrough() {
        MusicFile m;
        m.setFilePath(QStringLiteral("/lib/rm.mp3"));
        m.setTitle(QStringLiteral("RM"));
        m.setSourceType(QStringLiteral("local"));
        repo_->upsertTrack(m);

        QSignalSpy tracksSpy(svc_.get(), &LibraryService::tracksChanged);
        svc_->removeTrack(QStringLiteral("/lib/rm.mp3"));
        QVERIFY(!repo_->trackByPath(QStringLiteral("/lib/rm.mp3")).has_value());
        QCOMPARE(tracksSpy.count(), 1);
    }

    void toggleFavouritePassesThrough() {
        MusicFile m;
        m.setFilePath(QStringLiteral("/lib/fav.mp3"));
        m.setTitle(QStringLiteral("Fav"));
        m.setSourceType(QStringLiteral("local"));
        repo_->upsertTrack(m);

        QVERIFY(!repo_->trackByPath(QStringLiteral("/lib/fav.mp3"))->isFavourite());
        svc_->toggleFavourite(QStringLiteral("/lib/fav.mp3"));
        QVERIFY( repo_->trackByPath(QStringLiteral("/lib/fav.mp3"))->isFavourite());
        svc_->toggleFavourite(QStringLiteral("/lib/fav.mp3"));
        QVERIFY(!repo_->trackByPath(QStringLiteral("/lib/fav.mp3"))->isFavourite());
    }

    // ── Interface callback setters ──────────────────────────────────

    void scanProgressCallbackIsInvoked() {
        const QString p = freshFolder(QStringLiteral("cb"));
        writeFakeAudio(p + QStringLiteral("/a.mp3"));

        int callbackCalls = 0;
        int lastCurrent = -1;
        svc_->setOnScanProgress([&](int current, int /*total*/, QString /*file*/) {
            ++callbackCalls;
            lastCurrent = current;
        });

        svc_->addFolder(p);
        QVERIFY(callbackCalls >= 1);
        QCOMPARE(lastCurrent, callbackCalls); // current is monotonically increasing
    }
};

QTEST_GUILESS_MAIN(TestLibraryService)
#include "testlibraryservice.moc"

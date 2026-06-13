// testlibraryviewmodel.cpp
// Verifies that LibraryViewModel correctly mirrors LibraryRepository
// state, emits the right signals on changes, and forwards play/
// favourite/delete commands.

#include <QtTest/QtTest>
#include <QCoreApplication>
#include <QSettings>
#include <QTemporaryDir>
#include <QFile>
#include <QDir>
#include <QStandardPaths>

#include "core/database/Database.h"
#include "core/database/DatabaseConfig.h"
#include "core/database/LibraryRepository.h"
#include "core/database/LibraryScanner.h"
#include "core/playback/QueueManager.h"
#include "core/models/MusicFile.h"
#include "core/models/PlaylistInfo.h"

#include "viewmodels/LibraryViewModel.h"

using namespace mf::core::database;
using namespace mf::core::playback;
using namespace mf::core::models;
using namespace mf::app::viewmodels;

namespace {

MusicFile makeTrack(const QString& path, const QString& title,
                    bool favourite = false) {
    MusicFile m;
    m.setFilePath(path);
    m.setTitle(title);
    m.setArtist(QStringLiteral("Artist"));
    m.setAlbum(QStringLiteral("Album"));
    m.setTrackNumber(1);
    m.setDuration(std::chrono::seconds{180});
    m.setSourceType(QStringLiteral("local"));
    m.setIsFavourite(favourite);
    return m;
}

struct Fixture {
    QTemporaryDir tempDir;
    Database db;
    LibraryRepository repo;
    QueueManager queue;
    std::unique_ptr<LibraryViewModel> vm;

    Fixture()
        : repo(db)
        , queue(&repo)
    {
        DatabaseConfig c;
        QString migDir = tempDir.path() + QStringLiteral("/migrations");
        QDir().mkpath(migDir);
        QString src = QCoreApplication::applicationDirPath()
                      + QStringLiteral("/migrations/0001_initial_schema.sql");
        QString dst = migDir + QStringLiteral("/0001_initial_schema.sql");
        if (!QFile::exists(src)) {
            qFatal("Missing migration at %s", qUtf8Printable(src));
        }
        if (QFile::exists(dst)) QFile::remove(dst);
        if (!QFile::copy(src, dst)) {
            qFatal("Could not copy migration");
        }
        c.setFilePath(tempDir.path() + QStringLiteral("/lib.db"));
        c.setMigrationFiles({migDir});
        db = Database(c);
        QVERIFY(db.open());
        vm = std::make_unique<LibraryViewModel>(&repo, &queue);
    }
};

} // namespace

class TestLibraryViewModel : public QObject {
    Q_OBJECT

private slots:
    void initTestCase() {
        QCoreApplication::setOrganizationName(QStringLiteral("MusicefyTest"));
        QCoreApplication::setApplicationName(QStringLiteral("libraryviewmodel"));
    }
    void cleanupTestCase() { QSettings().clear(); }

    void initialStateIsEmpty() {
        Fixture f;
        QCOMPARE(f.vm->trackCount(), 0);
        QCOMPARE(f.vm->artistCount(), 0);
        QCOMPARE(f.vm->albumCount(), 0);
        QCOMPARE(f.vm->playlistCount(), 0);
        QVERIFY(f.vm->tracks().isEmpty());
    }

    void refreshReloadsFromRepo() {
        Fixture f;
        f.repo.upsertTrack(makeTrack(QStringLiteral("/x/a.mp3"), QStringLiteral("A")));
        f.repo.upsertTrack(makeTrack(QStringLiteral("/x/b.mp3"), QStringLiteral("B")));
        // vm was constructed before the inserts, so it sees 0.
        QCOMPARE(f.vm->trackCount(), 0);
        f.vm->refresh();
        QCOMPARE(f.vm->trackCount(), 2);
        QVERIFY(!f.vm->tracks().isEmpty());
    }

    void toggleFavouriteEmitsAndUpdatesFavList() {
        Fixture f;
        f.repo.upsertTrack(makeTrack(QStringLiteral("/x/a.mp3"), QStringLiteral("A")));
        f.vm->refresh();
        QCOMPARE(f.vm->favouriteTracks().size(), 0);

        int n = 0;
        connect(f.vm.get(), &LibraryViewModel::tracksChanged,
                [&]() { ++n; });

        f.vm->toggleFavourite(QStringLiteral("/x/a.mp3"));
        QCOMPARE(n, 1);
        // Favourites list now contains the track.
        f.vm->refresh();
        QCOMPARE(f.vm->favouriteTracks().size(), 1);

        // Toggle off again.
        f.vm->toggleFavourite(QStringLiteral("/x/a.mp3"));
        f.vm->refresh();
        QCOMPARE(f.vm->favouriteTracks().size(), 0);
    }

    void deleteTrackRemovesAndEmitsLibraryChanged() {
        Fixture f;
        f.repo.upsertTrack(makeTrack(QStringLiteral("/x/a.mp3"), QStringLiteral("A")));
        f.repo.upsertTrack(makeTrack(QStringLiteral("/x/b.mp3"), QStringLiteral("B")));
        f.vm->refresh();
        QCOMPARE(f.vm->trackCount(), 2);

        f.vm->deleteTrack(QStringLiteral("/x/a.mp3"));
        QCOMPARE(f.vm->trackCount(), 1);
        QCOMPARE(f.vm->trackAt(0).filePath(), QStringLiteral("/x/b.mp3"));
    }

    void createPlaylistAppendsToList() {
        Fixture f;
        QCOMPARE(f.vm->playlistCount(), 0);
        f.vm->createPlaylist(QStringLiteral("My Mix"));
        QCOMPARE(f.vm->playlistCount(), 1);
        QCOMPARE(f.vm->playlists().first().name(), QStringLiteral("My Mix"));
        QVERIFY(!f.vm->playlists().first().id().isEmpty());
    }

    void playAllEnqueuesEverything() {
        Fixture f;
        f.repo.upsertTrack(makeTrack(QStringLiteral("/x/a.mp3"), QStringLiteral("A")));
        f.repo.upsertTrack(makeTrack(QStringLiteral("/x/b.mp3"), QStringLiteral("B")));
        f.vm->refresh();

        f.vm->playAll();
        QCOMPARE(f.queue.count(), 2);
    }

    void playTrackClearsAndEnqueuesAllThenJumpsToMatch() {
        Fixture f;
        f.repo.upsertTrack(makeTrack(QStringLiteral("/x/a.mp3"), QStringLiteral("A")));
        f.repo.upsertTrack(makeTrack(QStringLiteral("/x/b.mp3"), QStringLiteral("B")));
        f.repo.upsertTrack(makeTrack(QStringLiteral("/x/c.mp3"), QStringLiteral("C")));
        f.vm->refresh();

        f.vm->playTrack(QStringLiteral("/x/b.mp3"));
        QCOMPARE(f.queue.count(), 3);
        QCOMPARE(f.queue.currentIndex(), 1);  // B is at index 1
        QCOMPARE(f.queue.currentTrack().filePath(), QStringLiteral("/x/b.mp3"));
    }

    void playTrackOnUnknownPathIsNoOp() {
        Fixture f;
        f.repo.upsertTrack(makeTrack(QStringLiteral("/x/a.mp3"), QStringLiteral("A")));
        f.vm->refresh();

        f.vm->playTrack(QStringLiteral("/x/does-not-exist.mp3"));
        QCOMPARE(f.queue.count(), 0);
    }

    void rowForTrackReturnsCorrectIndex() {
        Fixture f;
        f.repo.upsertTrack(makeTrack(QStringLiteral("/x/a.mp3"), QStringLiteral("A")));
        f.repo.upsertTrack(makeTrack(QStringLiteral("/x/b.mp3"), QStringLiteral("B")));
        f.vm->refresh();

        QCOMPARE(f.vm->rowForTrack(QStringLiteral("/x/a.mp3")), 0);
        QCOMPARE(f.vm->rowForTrack(QStringLiteral("/x/b.mp3")), 1);
        QCOMPARE(f.vm->rowForTrack(QStringLiteral("/x/missing.mp3")), -1);
    }
};

QTEST_MAIN(TestLibraryViewModel)
#include "testlibraryviewmodel.moc"

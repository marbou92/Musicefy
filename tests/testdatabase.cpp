// testdatabase.cpp
// Roundtrip tests for Database + LibraryRepository + LibraryScanner.
// All operations use a temporary on-disk SQLite file in QStandardPaths::TempLocation.

#include <QtTest/QtTest>
#include <QCoreApplication>
#include <QStandardPaths>
#include <QDir>
#include <QFile>
#include <QFileInfo>

#include "database/Database.h"
#include "database/DatabaseConfig.h"
#include "database/LibraryRepository.h"
#include "database/LibraryScanner.h"

#include "models/MusicFile.h"
#include "models/ArtistInfo.h"
#include "models/AlbumInfo.h"
#include "models/PlaylistInfo.h"

using namespace mf::core::database;
using namespace mf::core::models;

class TestDatabase : public QObject {
    Q_OBJECT

private:
    QString tmpRoot_;

    QString repoMigrationDir_;

    DatabaseConfig makeConfig(const QString& tag) {
        DatabaseConfig c;
        QString dir = tmpRoot_ + QStringLiteral("/") + tag;
        QDir().mkpath(dir);
        QString migrationDir = dir + QStringLiteral("/migrations");
        QDir().mkpath(migrationDir);
        // Copy the project migration into a per-test dir so the test owns it.
        QString src = QCoreApplication::applicationDirPath() + QStringLiteral("/migrations/0001_initial_schema.sql");
        QString dst = migrationDir + QStringLiteral("/0001_initial_schema.sql");
        if (!QFile::exists(dst)) {
            QFile::remove(dst);
            if (!QFile::copy(src, dst)) {
                qFatal("Could not copy migration from %s to %s", qUtf8Printable(src), qUtf8Printable(dst));
            }
        }
        c.setFilePath(dir + QStringLiteral("/musicefy.db"));
        c.setMigrationFiles({migrationDir});
        return c;
    }

    static MusicFile makeTrack(const QString& filePath,
                               const QString& title,
                               const QString& artist,
                               const QString& album) {
        MusicFile m;
        m.setFilePath(filePath);
        m.setTitle(title);
        m.setArtist(artist);
        m.setAlbum(album);
        m.setYear(2024);
        m.setTrackNumber(1);
        m.setDuration(std::chrono::seconds{ 180 });
        m.setBitrate(320);
        m.setFileSize(7'000'000);
        m.setSourceType(QStringLiteral("local"));
        return m;
    }

private slots:
    void init() {
        tmpRoot_ = QStandardPaths::writableLocation(QStandardPaths::TempLocation)
                 + QStringLiteral("/musicefy_test_") + QString::number(QDateTime::currentMSecsSinceEpoch());
        QDir().mkpath(tmpRoot_);
    }

    void openClose() {
        DatabaseConfig cfg = makeConfig(QStringLiteral("open"));
        Database db(cfg);
        QVERIFY(db.open());
        QVERIFY(db.isOpen());
        QCOMPARE(db.schemaVersion(), 1);
        db.close();
        QVERIFY(!db.isOpen());
    }

    void migrationsAreIdempotent() {
        DatabaseConfig cfg = makeConfig(QStringLiteral("idem"));
        Database db(cfg);
        QVERIFY(db.open());
        db.close();

        // Re-open: migrations should be no-ops because user_version=1.
        Database db2(cfg);
        QVERIFY(db2.open());
        QCOMPARE(db2.schemaVersion(), 1);
    }

    void trackUpsertAndFetch() {
        DatabaseConfig cfg = makeConfig(QStringLiteral("track"));
        Database db(cfg);
        QVERIFY(db.open());
        LibraryRepository repo(db);

        MusicFile t = makeTrack(QStringLiteral("/music/a.mp3"),
                                QStringLiteral("Song A"),
                                QStringLiteral("Artist X"),
                                QStringLiteral("Album Y"));
        repo.upsertTrack(t);

        auto found = repo.trackByPath(QStringLiteral("/music/a.mp3"));
        QVERIFY(found.has_value());
        QCOMPARE(found->title(), QStringLiteral("Song A"));
        QCOMPARE(found->artist(), QStringLiteral("Artist X"));
        QCOMPARE(found->bitrate(), 320);
        QCOMPARE(found->playCount(), 0);
    }

    void upsertUpdatesExistingRow() {
        DatabaseConfig cfg = makeConfig(QStringLiteral("upd"));
        Database db(cfg);
        QVERIFY(db.open());
        LibraryRepository repo(db);

        MusicFile t = makeTrack(QStringLiteral("/music/b.mp3"),
                                QStringLiteral("Old"),
                                QStringLiteral("A"),
                                QStringLiteral("B"));
        repo.upsertTrack(t);

        t.setTitle(QStringLiteral("New"));
        t.setPlayCount(5);
        repo.upsertTrack(t);

        auto found = repo.trackByPath(QStringLiteral("/music/b.mp3"));
        QVERIFY(found.has_value());
        QCOMPARE(found->title(), QStringLiteral("New"));
        QCOMPARE(found->playCount(), 5);
        QCOMPARE(repo.trackCount(), 1); // Still one row.
    }

    void incrementPlayCount() {
        DatabaseConfig cfg = makeConfig(QStringLiteral("play"));
        Database db(cfg);
        QVERIFY(db.open());
        LibraryRepository repo(db);

        MusicFile t = makeTrack(QStringLiteral("/music/c.mp3"),
                                QStringLiteral("X"), QStringLiteral("A"), QStringLiteral("B"));
        repo.upsertTrack(t);
        repo.incrementPlayCount(QStringLiteral("/music/c.mp3"));
        repo.incrementPlayCount(QStringLiteral("/music/c.mp3"));
        repo.incrementPlayCount(QStringLiteral("/music/c.mp3"));

        auto found = repo.trackByPath(QStringLiteral("/music/c.mp3"));
        QVERIFY(found.has_value());
        QCOMPARE(found->playCount(), 3);
        QVERIFY(found->lastPlayed().isValid());
    }

    void toggleFavourite() {
        DatabaseConfig cfg = makeConfig(QStringLiteral("fav"));
        Database db(cfg);
        QVERIFY(db.open());
        LibraryRepository repo(db);

        MusicFile t = makeTrack(QStringLiteral("/music/d.mp3"),
                                QStringLiteral("X"), QStringLiteral("A"), QStringLiteral("B"));
        repo.upsertTrack(t);
        QVERIFY(!repo.trackByPath(QStringLiteral("/music/d.mp3"))->isFavourite());

        repo.toggleFavourite(QStringLiteral("/music/d.mp3"));
        QVERIFY(repo.trackByPath(QStringLiteral("/music/d.mp3"))->isFavourite());

        repo.toggleFavourite(QStringLiteral("/music/d.mp3"));
        QVERIFY(!repo.trackByPath(QStringLiteral("/music/d.mp3"))->isFavourite());
    }

    void deleteTrack() {
        DatabaseConfig cfg = makeConfig(QStringLiteral("del"));
        Database db(cfg);
        QVERIFY(db.open());
        LibraryRepository repo(db);

        MusicFile t = makeTrack(QStringLiteral("/music/e.mp3"),
                                QStringLiteral("X"), QStringLiteral("A"), QStringLiteral("B"));
        repo.upsertTrack(t);
        QCOMPARE(repo.trackCount(), 1);

        repo.deleteTrack(QStringLiteral("/music/e.mp3"));
        QCOMPARE(repo.trackCount(), 0);
        QVERIFY(!repo.trackByPath(QStringLiteral("/music/e.mp3")).has_value());
    }

    void artistAndAlbumCrud() {
        DatabaseConfig cfg = makeConfig(QStringLiteral("artalb"));
        Database db(cfg);
        QVERIFY(db.open());
        LibraryRepository repo(db);

        ArtistInfo artist;
        artist.setId(QStringLiteral("ar-1"));
        artist.setName(QStringLiteral("Test Artist"));
        artist.setIsFollowed(true);
        artist.setSourceType(QStringLiteral("local"));
        repo.upsertArtist(artist);

        AlbumInfo album;
        album.setId(QStringLiteral("al-1"));
        album.setName(QStringLiteral("Test Album"));
        album.setArtistId(QStringLiteral("ar-1"));
        album.setArtist(QStringLiteral("Test Artist"));
        album.setYear(2020);
        album.setTrackCount(12);
        repo.upsertAlbum(album);

        QList<ArtistInfo> artists = repo.allArtists();
        QCOMPARE(artists.size(), 1);
        QCOMPARE(artists.first().name(), QStringLiteral("Test Artist"));
        QVERIFY(artists.first().isFollowed());

        QList<AlbumInfo> albums = repo.albumsForArtist(QStringLiteral("ar-1"));
        QCOMPARE(albums.size(), 1);
        QCOMPARE(albums.first().name(), QStringLiteral("Test Album"));
        QCOMPARE(albums.first().trackCount(), 12);
    }

    void playlistCrud() {
        DatabaseConfig cfg = makeConfig(QStringLiteral("plist"));
        Database db(cfg);
        QVERIFY(db.open());
        LibraryRepository repo(db);

        PlaylistInfo pl;
        pl.setId(QStringLiteral("pl-1"));
        pl.setName(QStringLiteral("My Mix"));
        pl.setSourceType(QStringLiteral("local"));
        pl.setCreatedAt(QDateTime::currentDateTime());
        repo.upsertPlaylist(pl);

        QCOMPARE(repo.allPlaylists().size(), 1);

        MusicFile t = makeTrack(QStringLiteral("/music/f.mp3"),
                                QStringLiteral("X"), QStringLiteral("A"), QStringLiteral("B"));
        t.setId(QStringLiteral("tr-f"));
        repo.upsertTrack(t);

        repo.addTrackToPlaylist(QStringLiteral("pl-1"), QStringLiteral("tr-f"), 0);
        repo.addTrackToPlaylist(QStringLiteral("pl-1"), QStringLiteral("tr-f"), 1);

        repo.removeTrackFromPlaylist(QStringLiteral("pl-1"), 0);
        repo.deletePlaylist(QStringLiteral("pl-1"));

        QCOMPARE(repo.allPlaylists().size(), 0);
    }

    void searchHistoryRoundtrip() {
        DatabaseConfig cfg = makeConfig(QStringLiteral("search"));
        Database db(cfg);
        QVERIFY(db.open());
        LibraryRepository repo(db);

        repo.recordSearch(QStringLiteral("foo"), QStringLiteral("local"), 10);
        repo.recordSearch(QStringLiteral("bar"), QStringLiteral("youtube"), 25);
        repo.recordSearch(QStringLiteral("foo"), QStringLiteral("local"), 12);

        auto recent = repo.recentSearchQueries(10);
        QCOMPARE(recent.size(), 2);
        // Most recent first.
        QCOMPARE(recent.first(), QStringLiteral("foo"));
    }

    void appStateRoundtrip() {
        DatabaseConfig cfg = makeConfig(QStringLiteral("state"));
        Database db(cfg);
        QVERIFY(db.open());
        LibraryRepository repo(db);

        QVERIFY(!repo.appState(QStringLiteral("theme")).has_value());
        repo.setAppState(QStringLiteral("theme"), QStringLiteral("MidnightBlues"));
        QCOMPARE(repo.appState(QStringLiteral("theme"))->toString(), QStringLiteral("MidnightBlues"));

        repo.setAppState(QStringLiteral("theme"), QStringLiteral("Forest"));
        QCOMPARE(repo.appState(QStringLiteral("theme"))->toString(), QStringLiteral("Forest"));
    }

    void favouriteTracksQuery() {
        DatabaseConfig cfg = makeConfig(QStringLiteral("favq"));
        Database db(cfg);
        QVERIFY(db.open());
        LibraryRepository repo(db);

        MusicFile a = makeTrack(QStringLiteral("/music/g1.mp3"),
                                QStringLiteral("A"), QStringLiteral("X"), QStringLiteral("Y"));
        MusicFile b = makeTrack(QStringLiteral("/music/g2.mp3"),
                                QStringLiteral("B"), QStringLiteral("X"), QStringLiteral("Y"));
        MusicFile c = makeTrack(QStringLiteral("/music/g3.mp3"),
                                QStringLiteral("C"), QStringLiteral("X"), QStringLiteral("Y"));
        repo.upsertTrack(a);
        repo.upsertTrack(b);
        repo.upsertTrack(c);

        repo.toggleFavourite(QStringLiteral("/music/g1.mp3"));
        repo.toggleFavourite(QStringLiteral("/music/g3.mp3"));

        auto favs = repo.favouriteTracks();
        QCOMPARE(favs.size(), 2);
    }

    void transactionRollsBackOnException() {
        DatabaseConfig cfg = makeConfig(QStringLiteral("txn"));
        Database db(cfg);
        QVERIFY(db.open());
        LibraryRepository repo(db);

        MusicFile t = makeTrack(QStringLiteral("/music/h.mp3"),
                                QStringLiteral("X"), QStringLiteral("A"), QStringLiteral("B"));
        repo.upsertTrack(t);
        QCOMPARE(repo.trackCount(), 1);

        bool rolledBack = false;
        try {
            db.inTransaction([&](QSqlDatabase&) {
                MusicFile t2 = makeTrack(QStringLiteral("/music/i.mp3"),
                                         QStringLiteral("Y"), QStringLiteral("A"), QStringLiteral("B"));
                repo.upsertTrack(t2);
                QCOMPARE(repo.trackCount(), 2);
                throw std::runtime_error("nope");
            });
        } catch (const std::exception&) {
            rolledBack = true;
        }
        QVERIFY(rolledBack);
        QCOMPARE(repo.trackCount(), 1);
    }
};

QTEST_GUILESS_MAIN(TestDatabase)
#include "testdatabase.moc"

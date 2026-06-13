// testsubsonic.cpp
// End-to-end tests for SubsonicProvider + SubsonicSession.
// Uses MockHttpClient (tests/mock/MockHttpClient.h) to enqueue canned
// Subsonic JSON responses by URL substring. Covers:
//
//   • Auth URL shape: token = md5(password + salt), salt is 16 hex,
//     includes u/t/s/v/c/f query params, and legacy useTokenAuth=false
//     uses p= and skips t/s.
//   • Health check + healthChanged signal transitions.
//   • searchTracks field-by-field mapping; onError propagation on
//     server failure envelopes.
//   • fetchStreamUrl, fetchLyrics, fetchCover.
//   • Higher-level browse methods: listArtists (multi-index flatten),
//     getArtist, getAlbum, getAlbumList.
//   • Playlist CRUD: getPlaylists, createPlaylist, updatePlaylist,
//     deletePlaylist. 18 slots total.

#include <QtTest>
#include <QBuffer>
#include <QCryptographicHash>
#include <QCoreApplication>
#include <QEventLoop>
#include <QJsonArray>
#include <QJsonDocument>
#include <QJsonObject>
#include <QSignalSpy>
#include <QString>
#include <QStringList>
#include <QTimer>
#include <QUrl>
#include <QUrlQuery>

#include <chrono>

#include "mock/MockHttpClient.h"
#include "models/MusicFile.h"
#include "models/Playlist.h"
#include "models/StreamingSource.h"
#include "sources/HttpClient.h"
#include "sources/SubsonicProvider.h"

using mf::core::models::MusicFile;
using mf::core::models::Playlist;
using mf::core::models::StreamingSource;
using mf::core::sources::HttpResponse;
using mf::core::sources::SubsonicConfig;
using mf::core::sources::SubsonicProvider;
using mf::core::sources::SubsonicSession;
using mf::core::test::MockHttpClient;

class TestSubsonic : public QObject {
    Q_OBJECT
private:
    // ── Fixture helpers ────────────────────────────────────────────────

    static SubsonicConfig makeConfig(bool tokenAuth = true) {
        SubsonicConfig c;
        c.serverUrl    = QStringLiteral("http://navidrome.test:4533");
        c.username     = QStringLiteral("alice");
        c.password     = QStringLiteral("hunter2");
        c.useTokenAuth = tokenAuth;
        c.apiVersion   = QStringLiteral("1.16.1");
        c.clientName   = QStringLiteral("musicefy");
        return c;
    }

    static StreamingSource makeSource(SubsonicConfig cfg = makeConfig()) {
        StreamingSource s;
        s.setId(QStringLiteral("sub-test"));
        s.setName(QStringLiteral("Test Subsonic"));
        s.setType(QStringLiteral("subsonic"));
        QJsonObject o;
        o.insert(QStringLiteral("serverUrl"),    cfg.serverUrl);
        o.insert(QStringLiteral("username"),     cfg.username);
        o.insert(QStringLiteral("password"),     cfg.password);
        o.insert(QStringLiteral("useTokenAuth"), cfg.useTokenAuth);
        o.insert(QStringLiteral("apiVersion"),   cfg.apiVersion);
        o.insert(QStringLiteral("clientName"),   cfg.clientName);
        QJsonDocument doc(o);
        s.setConfigurationJson(QString::fromUtf8(doc.toJson(QJsonDocument::Compact)));
        return s;
    }

    static QJsonObject okEnvelope(QJsonObject body) {
        QJsonObject resp;
        resp.insert(QStringLiteral("status"), QStringLiteral("ok"));
        for (auto it = body.constBegin(); it != body.constEnd(); ++it) {
            resp.insert(it.key(), it.value());
        }
        QJsonObject envelope;
        envelope.insert(QStringLiteral("subsonic-response"), resp);
        return envelope;
    }

    static QByteArray jsonBytes(const QJsonObject& obj) {
        return QJsonDocument(obj).toJson(QJsonDocument::Compact);
    }

    static QJsonObject songObject(const QString& id,
                                  const QString& title = QStringLiteral("Song"),
                                  const QString& artist = QStringLiteral("Artist"),
                                  const QString& album = QStringLiteral("Album"),
                                  int year = 2020,
                                  const QString& genre = QStringLiteral("Rock"),
                                  int track = 1,
                                  int durationSec = 180,
                                  int bitRate = 320,
                                  qint64 size = 5'000'000,
                                  const QString& coverArt = QStringLiteral("ar-1")) {
        QJsonObject s;
        s.insert(QStringLiteral("id"),          id);
        s.insert(QStringLiteral("title"),       title);
        s.insert(QStringLiteral("artist"),      artist);
        s.insert(QStringLiteral("album"),       album);
        s.insert(QStringLiteral("year"),        year);
        s.insert(QStringLiteral("genre"),       genre);
        s.insert(QStringLiteral("track"),       track);
        s.insert(QStringLiteral("duration"),    durationSec);
        s.insert(QStringLiteral("bitRate"),     bitRate);
        s.insert(QStringLiteral("size"),        static_cast<double>(size));
        s.insert(QStringLiteral("coverArt"),    coverArt);
        s.insert(QStringLiteral("streamUrl"),   QStringLiteral("/rest/stream?id=") + id);
        s.insert(QStringLiteral("albumArtist"), QStringLiteral("Album Artist"));
        return s;
    }

    static int drainMock(MockHttpClient& m, int timeoutMs = 2000) {
        return m.drain(timeoutMs);
    }

    static QString md5Hex(const QByteArray& input) {
        return QString::fromLatin1(
            QCryptographicHash::hash(input, QCryptographicHash::Md5).toHex());
    }

    MockHttpClient& mock() { return mock_; }

private slots:
    // ── 1. ping + healthChanged ────────────────────────────────────────
    void subsonic_ping_succeedsOnOkResponse() {
        MockHttpClient mock;
        mock.enqueueResponse(QStringLiteral("rest/ping"),
                             HttpResponse{200, jsonBytes(okEnvelope({})), {}, {}});

        SubsonicSession s(makeConfig(), QStringLiteral("src-1"), &mock);
        QSignalSpy spy(&s, &SubsonicSession::healthChanged);

        bool okCalled = false;
        bool okValue = false;
        QString errValue;
        s.ping([&](bool ok, QString err) {
            okCalled = true; okValue = ok; errValue = err;
        });
        drainMock(mock);

        QVERIFY(okCalled);
        QVERIFY(okValue);
        QVERIFY(errValue.isEmpty());
        QCOMPARE(spy.count(), 1);
        QCOMPARE(spy.first().first().toBool(), true);
        QVERIFY(s.isHealthy());
    }

    // ── 2. ping transitions false→true→false ──────────────────────────
    void subsonic_ping_emitsHealthChangedOnTransition() {
        MockHttpClient mock;
        mock.enqueueResponse(QStringLiteral("rest/ping"),
                             HttpResponse{200, jsonBytes(okEnvelope({})), {}, {}});
        mock.enqueueResponse(QStringLiteral("rest/ping"),
                             HttpResponse{0, {}, {}, QStringLiteral("network down")});

        SubsonicSession s(makeConfig(), QStringLiteral("src-1"), &mock);
        QSignalSpy spy(&s, &SubsonicSession::healthChanged);

        s.ping([](bool, QString) {});
        drainMock(mock);
        QCOMPARE(spy.count(), 1);
        QCOMPARE(spy.first().first().toBool(), true);

        s.ping([](bool, QString) {});
        drainMock(mock);
        QCOMPARE(spy.count(), 2);
        QCOMPARE(spy.last().first().toBool(), false);
        QVERIFY(!s.isHealthy());
    }

    // ── 3. searchTracks field mapping ─────────────────────────────────
    void subsonic_searchTracks_parsesAllFields() {
        QJsonArray songs;
        songs.append(songObject(QStringLiteral("song-1"),
                                QStringLiteral("Hit"),
                                QStringLiteral("Band"),
                                QStringLiteral("LP"),
                                2024, QStringLiteral("Indie"),
                                3, 240, 256, 4'000'000,
                                QStringLiteral("ar-2")));
        QJsonObject sr;
        sr.insert(QStringLiteral("song"), songs);
        mock().enqueueResponse(QStringLiteral("rest/search3"),
            HttpResponse{200, jsonBytes(okEnvelope({{"searchResult3", sr}})), {}, {}});

        SubsonicSession s(makeConfig(), QStringLiteral("src-1"), &mock());
        QList<MusicFile> result;
        bool done = false;
        s.searchTracks(QStringLiteral("hit"), 5,
            [&](QList<MusicFile> tracks) { result = tracks; done = true; },
            [](QString) {});
        drainMock(mock());

        QVERIFY(done);
        QCOMPARE(result.size(), 1);
        const MusicFile& m = result.first();
        QCOMPARE(m.id(),          QStringLiteral("song-1"));
        QCOMPARE(m.title(),       QStringLiteral("Hit"));
        QCOMPARE(m.artist(),      QStringLiteral("Band"));
        QCOMPARE(m.album(),       QStringLiteral("LP"));
        QCOMPARE(m.year(),        2024);
        QCOMPARE(m.genre(),       QStringLiteral("Indie"));
        QCOMPARE(m.trackNumber(), 3);
        QCOMPARE(m.duration(),    std::chrono::seconds(240));
        QCOMPARE(m.bitrate(),     256);
        QCOMPARE(m.fileSize(),    qint64(4'000'000));
        QCOMPARE(m.coverPath(),   QStringLiteral("ar-2"));
        QCOMPARE(m.sourceType(),  QStringLiteral("subsonic"));
        QCOMPARE(m.albumArtist(), QStringLiteral("Album Artist"));
    }

    // ── 4. searchTracks empty array ───────────────────────────────────
    void subsonic_searchTracks_emptyArrayReturnsEmpty() {
        QJsonObject sr;
        sr.insert(QStringLiteral("song"), QJsonArray{});
        mock().enqueueResponse(QStringLiteral("rest/search3"),
            HttpResponse{200, jsonBytes(okEnvelope({{"searchResult3", sr}})), {}, {}});

        SubsonicSession s(makeConfig(), QStringLiteral("src-1"), &mock());
        QList<MusicFile> result;
        bool done = false;
        s.searchTracks(QStringLiteral("nothing"), 5,
            [&](QList<MusicFile> t) { result = t; done = true; },
            [](QString) {});
        drainMock(mock());
        QVERIFY(done);
        QVERIFY(result.isEmpty());
    }

    // ── 5. searchTracks error propagation ─────────────────────────────
    void subsonic_searchTracks_serverErrorPropagatesToOnError() {
        QJsonObject errObj;
        errObj.insert(QStringLiteral("code"),    10);
        errObj.insert(QStringLiteral("message"), QStringLiteral("data not found"));
        QJsonObject resp;
        resp.insert(QStringLiteral("status"), QStringLiteral("failed"));
        resp.insert(QStringLiteral("error"),  errObj);
        QJsonObject envelope;
        envelope.insert(QStringLiteral("subsonic-response"), resp);
        mock().enqueueResponse(QStringLiteral("rest/search3"),
            HttpResponse{200, jsonBytes(envelope), {}, {}});

        SubsonicSession s(makeConfig(), QStringLiteral("src-1"), &mock());
        QList<MusicFile> result;
        bool done = false;
        QString errorMsg;
        bool errCalled = false;
        s.searchTracks(QStringLiteral("nope"), 5,
            [&](QList<MusicFile> t) { result = t; done = true; },
            [&](QString err) { errCalled = true; errorMsg = err; });
        drainMock(mock());
        QVERIFY(done);
        QVERIFY(errCalled);
        QVERIFY(result.isEmpty());
        QVERIFY(errorMsg.contains(QStringLiteral("data not found")));
    }

    // ── 6. fetchStreamUrl auth signature ──────────────────────────────
    void subsonic_fetchStreamUrl_buildsSignedUrl_andTokenMatchesMd5() {
        SubsonicSession s(makeConfig(true), QStringLiteral("src-1"), &mock());
        QString url;
        QString err;
        s.fetchStreamUrl(QStringLiteral("track-42"),
                         [&](QString u) { url = u; },
                         [&](QString e) { err = e; });
        QVERIFY(err.isEmpty());
        QVERIFY(url.contains(QStringLiteral("/rest/stream")));

        QUrl parsed(url);
        QUrlQuery q(parsed.query());
        QCOMPARE(q.queryItemValue(QStringLiteral("u")), QStringLiteral("alice"));
        QCOMPARE(q.queryItemValue(QStringLiteral("v")), QStringLiteral("1.16.1"));
        QCOMPARE(q.queryItemValue(QStringLiteral("c")), QStringLiteral("musicefy"));
        QCOMPARE(q.queryItemValue(QStringLiteral("f")), QStringLiteral("json"));
        QCOMPARE(q.queryItemValue(QStringLiteral("id")), QStringLiteral("track-42"));
        const QString t = q.queryItemValue(QStringLiteral("t"));
        const QString s_ = q.queryItemValue(QStringLiteral("s"));
        QVERIFY(!s_.isEmpty());
        QCOMPARE(s_.size(), 16); // 64-bit hex
        QCOMPARE(t, md5Hex((QStringLiteral("hunter2") + s_).toUtf8()));
    }

    // ── 7. fetchStreamUrl legacy auth ─────────────────────────────────
    void subsonic_fetchStreamUrl_legacyAuthUsesPlainPassword() {
        SubsonicSession s(makeConfig(false), QStringLiteral("src-1"), &mock());
        QString url;
        s.fetchStreamUrl(QStringLiteral("track-99"),
                         [&](QString u) { url = u; },
                         [](QString) {});
        QUrl parsed(url);
        QUrlQuery q(parsed.query());
        QCOMPARE(q.queryItemValue(QStringLiteral("p")), QStringLiteral("hunter2"));
        QVERIFY(q.queryItemValue(QStringLiteral("t")).isEmpty());
        QVERIFY(q.queryItemValue(QStringLiteral("s")).isEmpty());
    }

    // ── 8. fetchLyrics happy path ──────────────────────────────────────
    void subsonic_fetchLyrics_returnsText() {
        QJsonObject lr;
        lr.insert(QStringLiteral("value"), QStringLiteral("la la la"));
        mock().enqueueResponse(QStringLiteral("rest/getLyrics"),
            HttpResponse{200, jsonBytes(okEnvelope({{"lyrics", lr}})), {}, {}});

        SubsonicSession s(makeConfig(), QStringLiteral("src-1"), &mock());
        QString lyrics;
        bool done = false;
        s.fetchLyrics(QStringLiteral("track-1"),
                      [&](QString v) { lyrics = v; done = true; },
                      [](QString) {});
        drainMock(mock());
        QVERIFY(done);
        QCOMPARE(lyrics, QStringLiteral("la la la"));
    }

    // ── 9. fetchLyrics missing field ──────────────────────────────────
    void subsonic_fetchLyrics_missingLyricsReturnsEmpty() {
        mock().enqueueResponse(QStringLiteral("rest/getLyrics"),
            HttpResponse{200, jsonBytes(okEnvelope({})), {}, {}});

        SubsonicSession s(makeConfig(), QStringLiteral("src-1"), &mock());
        QString lyrics;
        bool done = false;
        s.fetchLyrics(QStringLiteral("track-1"),
                      [&](QString v) { lyrics = v; done = true; },
                      [](QString) {});
        drainMock(mock());
        QVERIFY(done);
        QVERIFY(lyrics.isEmpty());
    }

    // ── 10. fetchCover binary ─────────────────────────────────────────
    void subsonic_fetchCover_returnsBytes() {
        QByteArray jpeg;
        for (int i = 0; i < 16; ++i) jpeg.append(char(0xAA));
        mock().enqueueResponse(QStringLiteral("rest/getCoverArt"),
            HttpResponse{200, jpeg, {}, {}});

        SubsonicSession s(makeConfig(), QStringLiteral("src-1"), &mock());
        QByteArray bytes;
        bool done = false;
        s.fetchCover(QStringLiteral("ar-1"),
                     [&](QByteArray b) { bytes = b; done = true; },
                     [](QString) {});
        drainMock(mock());
        QVERIFY(done);
        QCOMPARE(bytes.size(), 16);
        QCOMPARE(bytes[0], char(0xAA));
    }

    // ── 11. listArtists multi-index flatten ───────────────────────────
    void subsonic_listArtists_flattensAllIndexes() {
        QJsonObject idxA;
        idxA.insert(QStringLiteral("name"), QStringLiteral("A"));
        QJsonArray aArts;
        QJsonObject ar1; ar1.insert(QStringLiteral("id"), QStringLiteral("a-1"));
        ar1.insert(QStringLiteral("name"), QStringLiteral("AC/DC"));
        ar1.insert(QStringLiteral("coverArt"), QStringLiteral("ar-a1"));
        aArts.append(ar1);
        idxA.insert(QStringLiteral("artist"), aArts);

        QJsonObject idxB;
        idxB.insert(QStringLiteral("name"), QStringLiteral("B"));
        QJsonArray bArts;
        QJsonObject ar2; ar2.insert(QStringLiteral("id"), QStringLiteral("b-1"));
        ar2.insert(QStringLiteral("name"), QStringLiteral("Beatles"));
        ar2.insert(QStringLiteral("coverArt"), QStringLiteral("ar-b1"));
        bArts.append(ar2);
        idxB.insert(QStringLiteral("artist"), bArts);

        QJsonObject artists;
        artists.insert(QStringLiteral("index"), QJsonArray{idxA, idxB});
        mock().enqueueResponse(QStringLiteral("rest/getArtists"),
            HttpResponse{200, jsonBytes(okEnvelope({{"artists", artists}})), {}, {}});

        SubsonicSession s(makeConfig(), QStringLiteral("src-1"), &mock());
        QList<MusicFile> out;
        bool done = false;
        s.listArtists([&](QList<MusicFile> a, QString) { out = a; done = true; });
        drainMock(mock());
        QVERIFY(done);
        QCOMPARE(out.size(), 2);
        QCOMPARE(out[0].id(),        QStringLiteral("a-1"));
        QCOMPARE(out[0].artist(),    QStringLiteral("AC/DC"));
        QCOMPARE(out[0].coverPath(), QStringLiteral("ar-a1"));
        QCOMPARE(out[0].sourceType(), QStringLiteral("subsonic"));
        QCOMPARE(out[1].id(),        QStringLiteral("b-1"));
        QCOMPARE(out[1].artist(),    QStringLiteral("Beatles"));
    }

    // ── 12. getArtist returns albums (songs shape, mapped) ────────────
    void subsonic_getArtist_returnsAlbums() {
        QJsonArray albums;
        QJsonObject al;
        al.insert(QStringLiteral("id"),        QStringLiteral("al-1"));
        al.insert(QStringLiteral("title"),     QStringLiteral("Greatest Hits"));
        al.insert(QStringLiteral("artist"),    QStringLiteral("AC/DC"));
        al.insert(QStringLiteral("coverArt"),  QStringLiteral("ar-al1"));
        albums.append(al);
        QJsonObject artist;
        artist.insert(QStringLiteral("album"), albums);
        mock().enqueueResponse(QStringLiteral("rest/getArtist"),
            HttpResponse{200, jsonBytes(okEnvelope({{"artist", artist}})), {}, {}});

        SubsonicSession s(makeConfig(), QStringLiteral("src-1"), &mock());
        QList<MusicFile> out;
        bool done = false;
        s.getArtist(QStringLiteral("a-1"),
                    [&](QList<MusicFile> v, QString) { out = v; done = true; });
        drainMock(mock());
        QVERIFY(done);
        QCOMPARE(out.size(), 1);
        QCOMPARE(out[0].id(),        QStringLiteral("al-1"));
        QCOMPARE(out[0].title(),     QStringLiteral("Greatest Hits"));
        QCOMPARE(out[0].coverPath(), QStringLiteral("ar-al1"));
    }

    // ── 13. getAlbum returns songs ────────────────────────────────────
    void subsonic_getAlbum_returnsSongs() {
        QJsonArray songs;
        songs.append(songObject(QStringLiteral("sg-1"),
                                QStringLiteral("Track 1"),
                                QStringLiteral("AC/DC"),
                                QStringLiteral("Back in Black"),
                                1980, QStringLiteral("Rock"),
                                1, 250, 320, 6'000'000));
        songs.append(songObject(QStringLiteral("sg-2"),
                                QStringLiteral("Track 2"),
                                QStringLiteral("AC/DC"),
                                QStringLiteral("Back in Black"),
                                1980, QStringLiteral("Rock"),
                                2, 200, 320, 5'000'000));
        QJsonObject album;
        album.insert(QStringLiteral("song"), songs);
        mock().enqueueResponse(QStringLiteral("rest/getAlbum"),
            HttpResponse{200, jsonBytes(okEnvelope({{"album", album}})), {}, {}});

        SubsonicSession s(makeConfig(), QStringLiteral("src-1"), &mock());
        QList<MusicFile> out;
        bool done = false;
        s.getAlbum(QStringLiteral("al-1"),
                   [&](QList<MusicFile> v, QString) { out = v; done = true; });
        drainMock(mock());
        QVERIFY(done);
        QCOMPARE(out.size(), 2);
        QCOMPARE(out[0].id(),    QStringLiteral("sg-1"));
        QCOMPARE(out[1].id(),    QStringLiteral("sg-2"));
        QCOMPARE(out[0].album(), QStringLiteral("Back in Black"));
        QCOMPARE(out[1].trackNumber(), 2);
    }

    // ── 14. getAlbumList passes type + size; returns albums ───────────
    void subsonic_getAlbumList_passesType() {
        mock().enqueueResponse(QStringLiteral("rest/getAlbumList2"),
            HttpResponse{200, jsonBytes(okEnvelope({})), {}, {}});
        SubsonicSession s(makeConfig(), QStringLiteral("src-1"), &mock());
        QList<MusicFile> out;
        bool done = false;
        s.getAlbumList(QStringLiteral("newest"), 20,
                       [&](QList<MusicFile> v, QString) { out = v; done = true; });
        drainMock(mock());
        QVERIFY(done);
        QVERIFY(out.isEmpty());

        // Verify the URL carries the type= & size= params.
        auto rec = mock().requestsMatching(QStringLiteral("getAlbumList2")).value(0);
        QVERIFY(rec.url.contains(QStringLiteral("type=newest")));
        QVERIFY(rec.url.contains(QStringLiteral("size=20")));
    }

    // ── 15. getPlaylists returns playlists with track IDs ─────────────
    void subsonic_getPlaylists_returnsPlaylistsWithTrackIds() {
        QJsonArray entriesA;
        QJsonObject e1; e1.insert(QStringLiteral("id"), QStringLiteral("tr-1"));
        entriesA.append(e1);
        QJsonObject e2; e2.insert(QStringLiteral("id"), QStringLiteral("tr-2"));
        entriesA.append(e2);

        QJsonObject plA;
        plA.insert(QStringLiteral("id"),        QStringLiteral("pl-1"));
        plA.insert(QStringLiteral("name"),      QStringLiteral("My Mix"));
        plA.insert(QStringLiteral("songCount"), 2);
        plA.insert(QStringLiteral("duration"),  360);
        plA.insert(QStringLiteral("public"),    true);
        plA.insert(QStringLiteral("owner"),     QStringLiteral("alice"));
        plA.insert(QStringLiteral("coverArt"),  QStringLiteral("ar-pl1"));
        plA.insert(QStringLiteral("entry"),     entriesA);

        QJsonObject plB;
        plB.insert(QStringLiteral("id"),        QStringLiteral("pl-2"));
        plB.insert(QStringLiteral("name"),      QStringLiteral("Road Trip"));
        plB.insert(QStringLiteral("songCount"), 0);
        plB.insert(QStringLiteral("duration"),  0);
        plB.insert(QStringLiteral("public"),    false);
        plB.insert(QStringLiteral("owner"),     QStringLiteral("alice"));
        plB.insert(QStringLiteral("coverArt"),  QString());
        plB.insert(QStringLiteral("entry"),     QJsonArray{});

        QJsonObject playlists;
        playlists.insert(QStringLiteral("playlist"), QJsonArray{plA, plB});
        mock().enqueueResponse(QStringLiteral("rest/getPlaylists"),
            HttpResponse{200, jsonBytes(okEnvelope({{"playlists", playlists}})), {}, {}});

        SubsonicSession s(makeConfig(), QStringLiteral("src-1"), &mock());
        QList<Playlist> out;
        bool done = false;
        s.getPlaylists([&](QList<Playlist> p, QString) { out = p; done = true; });
        drainMock(mock());
        QVERIFY(done);
        QCOMPARE(out.size(), 2);

        QCOMPARE(out[0].id(),        QStringLiteral("pl-1"));
        QCOMPARE(out[0].name(),      QStringLiteral("My Mix"));
        QCOMPARE(out[0].songCount(), 2);
        QCOMPARE(out[0].duration(),  std::chrono::seconds(360));
        QVERIFY(out[0].isPublic());
        QCOMPARE(out[0].owner(),     QStringLiteral("alice"));
        QCOMPARE(out[0].coverArt(),  QStringLiteral("ar-pl1"));
        QCOMPARE(out[0].trackIds(),  QStringList{QStringLiteral("tr-1"),
                                                 QStringLiteral("tr-2")});

        QCOMPARE(out[1].id(),        QStringLiteral("pl-2"));
        QCOMPARE(out[1].name(),      QStringLiteral("Road Trip"));
        QVERIFY(!out[1].isPublic());
        QVERIFY(out[1].trackIds().isEmpty());
    }

    // ── 16. createPlaylist sends name + songIds; returns new playlist ──
    void subsonic_createPlaylist_sendsNameAndSongIds() {
        QJsonArray entries;
        QJsonObject e1; e1.insert(QStringLiteral("id"), QStringLiteral("tr-1"));
        entries.append(e1);
        QJsonObject newPl;
        newPl.insert(QStringLiteral("id"),        QStringLiteral("pl-99"));
        newPl.insert(QStringLiteral("name"),      QStringLiteral("New Mix"));
        newPl.insert(QStringLiteral("songCount"), 1);
        newPl.insert(QStringLiteral("duration"),  180);
        newPl.insert(QStringLiteral("public"),    true);
        newPl.insert(QStringLiteral("owner"),     QStringLiteral("alice"));
        newPl.insert(QStringLiteral("coverArt"),  QString());
        newPl.insert(QStringLiteral("entry"),     entries);

        mock().enqueueResponse(QStringLiteral("rest/createPlaylist"),
            HttpResponse{200, jsonBytes(okEnvelope({{"playlist", newPl}})), {}, {}});

        SubsonicSession s(makeConfig(), QStringLiteral("src-1"), &mock());
        QList<Playlist> out;
        bool done = false;
        s.createPlaylist(QStringLiteral("New Mix"),
                         QStringList{QStringLiteral("tr-1")},
                         true,
                         [&](QList<Playlist> p, QString) { out = p; done = true; });
        drainMock(mock());
        QVERIFY(done);
        QCOMPARE(out.size(), 1);
        QCOMPARE(out[0].id(),   QStringLiteral("pl-99"));
        QCOMPARE(out[0].name(), QStringLiteral("New Mix"));
        QCOMPARE(out[0].trackIds(), QStringList{QStringLiteral("tr-1")});

        // Verify the URL carries name= and songIds=.
        auto rec = mock().requestsMatching(QStringLiteral("createPlaylist")).value(0);
        QVERIFY(rec.url.contains(QStringLiteral("name=New+Mix")));
        QVERIFY(rec.url.contains(QStringLiteral("songIds=tr-1")));
        QVERIFY(rec.url.contains(QStringLiteral("public=true")));
    }

    // ── 17. updatePlaylist sends playlistId ───────────────────────────
    void subsonic_updatePlaylist_sendsPlaylistId() {
        mock().enqueueResponse(QStringLiteral("rest/updatePlaylist"),
            HttpResponse{200, jsonBytes(okEnvelope({})), {}, {}});

        SubsonicSession s(makeConfig(), QStringLiteral("src-1"), &mock());
        QList<Playlist> out;
        bool done = false;
        s.updatePlaylist(QStringLiteral("pl-42"),
                         QStringLiteral("Renamed"),
                         QStringList{QStringLiteral("a"), QStringLiteral("b")},
                         false,
                         [&](QList<Playlist> p, QString) { out = p; done = true; });
        drainMock(mock());
        QVERIFY(done);
        QVERIFY(out.isEmpty());

        auto rec = mock().requestsMatching(QStringLiteral("updatePlaylist")).value(0);
        QVERIFY(rec.url.contains(QStringLiteral("playlistId=pl-42")));
        QVERIFY(rec.url.contains(QStringLiteral("name=Renamed")));
        QVERIFY(rec.url.contains(QStringLiteral("songIds=a,b")));
        QVERIFY(rec.url.contains(QStringLiteral("public=false")));
    }

    // ── 18. deletePlaylist calls onDone with true on success ──────────
    void subsonic_deletePlaylist_callsOnDoneWithTrue() {
        mock().enqueueResponse(QStringLiteral("rest/deletePlaylist"),
            HttpResponse{200, jsonBytes(okEnvelope({})), {}, {}});

        SubsonicSession s(makeConfig(), QStringLiteral("src-1"), &mock());
        bool ok = false;
        QString err;
        bool done = false;
        s.deletePlaylist(QStringLiteral("pl-99"),
                         [&](bool b, QString e) { ok = b; err = e; done = true; });
        drainMock(mock());
        QVERIFY(done);
        QVERIFY(ok);
        QVERIFY(err.isEmpty());

        auto rec = mock().requestsMatching(QStringLiteral("deletePlaylist")).value(0);
        QVERIFY(rec.url.contains(QStringLiteral("id=pl-99")));
    }

private:
    // Shared mock for slots that need it (slots 8-14 use it directly
    // via the member).
    MockHttpClient mock_;
};

QTEST_GUILESS_MAIN(TestSubsonic)
#include "testsubsonic.moc"

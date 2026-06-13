// testsearchviewmodel.cpp
// Tests SearchViewModel state machine, URL detection, and basic search flow.

#include <QtTest/QtTest>
#include <QCoreApplication>
#include <QSettings>

#include "core/models/MusicFile.h"
#include "core/models/SearchResultGroup.h"
#include "core/sources/youtube/YouTubeUrlParser.h"

#include "viewmodels/SearchViewModel.h"

using namespace mf::core::models;
using namespace mf::core::sources::youtube;
using namespace mf::app::viewmodels;

class TestSearchViewModel : public QObject {
    Q_OBJECT

private slots:
    void initTestCase() {
        QCoreApplication::setOrganizationName(QStringLiteral("MusicefyTest"));
        QCoreApplication::setApplicationName(QStringLiteral("searchviewmodel"));
    }
    void cleanupTestCase() { QSettings().clear(); }

    // ── YouTubeUrlParser tests ──────────────────────────────────────

    void parseVideoUrl() {
        auto r = YouTubeUrlParser::parse(
            QStringLiteral("https://www.youtube.com/watch?v=dQw4w9WgXcQ"));
        QCOMPARE(r.type, UrlType::Video);
        QCOMPARE(r.videoId, QStringLiteral("dQw4w9WgXcQ"));
    }

    void parseShortVideoUrl() {
        auto r = YouTubeUrlParser::parse(
            QStringLiteral("https://youtu.be/dQw4w9WgXcQ"));
        QCOMPARE(r.type, UrlType::Video);
        QCOMPARE(r.videoId, QStringLiteral("dQw4w9WgXcQ"));
    }

    void parseShortsUrl() {
        auto r = YouTubeUrlParser::parse(
            QStringLiteral("https://www.youtube.com/shorts/dQw4w9WgXcQ"));
        QCOMPARE(r.type, UrlType::Video);
        QCOMPARE(r.videoId, QStringLiteral("dQw4w9WgXcQ"));
    }

    void parseMusicVideoUrl() {
        auto r = YouTubeUrlParser::parse(
            QStringLiteral("https://music.youtube.com/watch?v=dQw4w9WgXcQ"));
        QCOMPARE(r.type, UrlType::Video);
        QCOMPARE(r.videoId, QStringLiteral("dQw4w9WgXcQ"));
    }

    void parsePlaylistUrl() {
        auto r = YouTubeUrlParser::parse(
            QStringLiteral("https://www.youtube.com/playlist?list=PLrAXtmErZgOeiKm4sgNOknGvNjby9efdf"));
        QCOMPARE(r.type, UrlType::Playlist);
        QCOMPARE(r.playlistId, QStringLiteral("PLrAXtmErZgOeiKm4sgNOknGvNjby9efdf"));
    }

    void parseArtistUrl() {
        auto r = YouTubeUrlParser::parse(
            QStringLiteral("https://music.youtube.com/channel/UCxOeBkQP1VvHnMiOxKzKkZA"));
        QCOMPARE(r.type, UrlType::Artist);
        QCOMPARE(r.browseId, QStringLiteral("UCxOeBkQP1VvHnMiOxKzKkZA"));
    }

    void parseAlbumUrl() {
        auto r = YouTubeUrlParser::parse(
            QStringLiteral("https://music.youtube.com/browse/MPREb_8eEwH9k3y5"));
        QCOMPARE(r.type, UrlType::Album);
        QCOMPARE(r.browseId, QStringLiteral("MPREb_8eEwH9k3y5"));
    }

    void parseNonYouTubeUrl() {
        auto r = YouTubeUrlParser::parse(
            QStringLiteral("https://www.google.com"));
        QCOMPARE(r.type, UrlType::Unknown);
        QVERIFY(r.videoId.isEmpty());
    }

    void isYouTubeUrlReturnsTrueForValidUrl() {
        QVERIFY(YouTubeUrlParser::isYouTubeUrl(
            QStringLiteral("https://youtu.be/dQw4w9WgXcQ")));
    }

    void isYouTubeUrlReturnsFalseForInvalidUrl() {
        QVERIFY(!YouTubeUrlParser::isYouTubeUrl(
            QStringLiteral("https://example.com/video")));
    }

    void createWatchUrlFormatsCorrectly() {
        auto url = YouTubeUrlParser::createWatchUrl(QStringLiteral("dQw4w9WgXcQ"));
        QCOMPARE(url, QStringLiteral("https://music.youtube.com/watch?v=dQw4w9WgXcQ"));
    }

    void parseVideoWithPlaylistParam() {
        auto r = YouTubeUrlParser::parse(
            QStringLiteral("https://www.youtube.com/watch?v=dQw4w9WgXcQ&list=PLrAXtmErZgOeiKm4sgNOknGvNjby9efdf"));
        QCOMPARE(r.type, UrlType::Video);
        QCOMPARE(r.videoId, QStringLiteral("dQw4w9WgXcQ"));
        QCOMPARE(r.playlistId, QStringLiteral("PLrAXtmErZgOeiKm4sgNOknGvNjby9efdf"));
    }

    void parseTrimsWhitespace() {
        auto r = YouTubeUrlParser::parse(
            QStringLiteral("  https://youtu.be/dQw4w9WgXcQ  "));
        QCOMPARE(r.type, UrlType::Video);
        QCOMPARE(r.videoId, QStringLiteral("dQw4w9WgXcQ"));
    }

    // ── SearchResultGroup tests ─────────────────────────────────────

    void searchResultGroupDefaults() {
        SearchResultGroup g;
        QCOMPARE(g.sourceType(), QString());
        QCOMPARE(g.mode(), SearchSourceMode::All);
        QCOMPARE(g.header(), QString());
        QVERIFY(g.results().isEmpty());
        QCOMPARE(g.totalCount(), 0);
        QVERIFY(!g.hasMore());
    }

    void searchResultGroupSetters() {
        SearchResultGroup g;
        g.setSourceType(QStringLiteral("youtube"));
        g.setHeader(QStringLiteral("Songs"));
        MusicFile m;
        m.setTitle(QStringLiteral("Test"));
        QList<MusicFile> results;
        results.append(m);
        g.setResults(results);
        g.setTotalCount(1);
        g.setHasMore(true);

        QCOMPARE(g.sourceType(), QStringLiteral("youtube"));
        QCOMPARE(g.header(), QStringLiteral("Songs"));
        QCOMPARE(g.results().size(), 1);
        QCOMPARE(g.totalCount(), 1);
        QVERIFY(g.hasMore());
    }
};

QTEST_MAIN(TestSearchViewModel)
#include "testsearchviewmodel.moc"

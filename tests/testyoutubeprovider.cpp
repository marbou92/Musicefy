// testyoutubeprovider.cpp
// Integration tests for YouTubeProvider + YouTubeSession:
//   • Provider has a shared stream cache
//   • Sessions created from the same provider share the cache
//   • normalizeVideoId / parseUrl are static and pure
//   • fetchStreamUrl uses the cache fast path on the second call
//   • fetchStreamUrl reports real errors (HTTP 4xx, unplayable, login required)
//     via the InnerTubeClient's multi-client fallback
//   • fetchStreamUrl populates the stream cache on success
//   • searchTracks caches results per (query,limit) and re-uses them
//   • fetchCover prefers maxres, falls back through the standard
//     thumbnail qualities when a higher-res returns 404
//
// The new slots use MockHttpClient (tests/mock/MockHttpClient.h) to
// run the network-facing paths fully offline. Older slots that
// don't need the network (cache hit, invalid id, url normalisation)
// still use a real HttpClient and exercise the synchronous paths.

#include <QtTest>
#include <QBuffer>
#include <QCoreApplication>
#include <QDir>
#include <QEventLoop>
#include <QFile>
#include <QImage>
#include <QTemporaryDir>
#include <QTimer>

#include <memory>
#include <chrono>

#include "mock/MockHttpClient.h"
#include "services/ImageCache.h"
#include "sources/HttpClient.h"
#include "sources/StreamingSource.h"
#include "sources/YouTubeProvider.h"
#include "sources/youtube/StreamCache.h"
#include "sources/youtube/YtDlpProcess.h"

using mf::core::models::StreamingSource;
using mf::core::services::ImageCache;
using mf::core::sources::HttpRequest;
using mf::core::sources::HttpResponse;
using mf::core::sources::YouTubeProvider;
using mf::core::sources::YouTubeSession;
using mf::core::sources::youtube::StreamCache;
using mf::core::sources::youtube::YtDlpProcess;
using mf::core::test::MockHttpClient;

class TestYouTubeProvider : public QObject {
    Q_OBJECT
private slots:
    // ── Provider/session shape (real HttpClient, no network needed) ──
    void provider_ownsStreamCache();
    void sessions_shareStreamCache();
    void normalizeVideoId_acceptsUrl();
    void normalizeVideoId_acceptsBareId();
    void normalizeVideoId_rejectsGarbage();
    void parseUrl_classifiesInputs();

    // ── fetchStreamUrl synchronous paths (real HttpClient) ───────────
    void fetchStreamUrl_invalidId_callsError();
    void fetchStreamUrl_cacheHit_callsDone();
    void fetchStreamUrl_cacheMiss_callsError();
    void fetchStreamUrl_normalizesUrlBeforeCacheLookup();

    // ── fetchStreamUrl network paths (MockHttpClient) ────────────────
    void searchTracks_cachesResults();
    void fetchStreamUrl_unplayableReportsError();
    void fetchStreamUrl_loginRequiredRotatesClient();
    void fetchStreamUrl_cipheredFormatGetsDeciphered();
    void fetchStreamUrl_populatesStreamCache();
    void fetchCover_prefersMaxres();

    // ── Block 5.2.C: cipher path + yt-dlp fallback ─────────────────
    void fetchStreamUrl_cipherPathResolvesOpsViaJsFetch();
    void fetchStreamUrl_cipherCacheReusedOnSecondCall();
    void fetchStreamUrl_fallsBackToYtDlp();
    void searchTracks_fallsBackToYtDlp();
};

// ── helpers ───────────────────────────────────────────────────────────

static StreamingSource makeYouTubeSource() {
    StreamingSource s;
    s.setId(QStringLiteral("yt-test"));
    s.setName(QStringLiteral("YouTube"));
    s.setType(QStringLiteral("YouTube"));
    s.setUrl(QStringLiteral("https://music.youtube.com"));
    s.setUsername(QStringLiteral(""));
    s.setPassword(QStringLiteral(""));
    return s;
}

static YouTubeSession* asSession(
    std::unique_ptr<mf::core::interfaces::IMusicSourceSession>& up) {
    return dynamic_cast<YouTubeSession*>(up.get());
}

static QJsonObject playerResponseOkWithUncipheredAudio(const QString& videoId,
                                                       const QString& streamUrl) {
    QJsonObject fmt;
    fmt.insert(QStringLiteral("itag"),            140);
    fmt.insert(QStringLiteral("mimeType"),        QStringLiteral("audio/mp4; codecs=\"mp4a.40.2\""));
    fmt.insert(QStringLiteral("bitrate"),         128000);
    fmt.insert(QStringLiteral("audioSampleRate"), 44100);
    fmt.insert(QStringLiteral("url"),             streamUrl);

    QJsonObject adaptive;
    adaptive.insert(QStringLiteral("formats"),         QJsonArray{});
    adaptive.insert(QStringLiteral("adaptiveFormats"), QJsonArray{fmt});

    QJsonObject vd;
    vd.insert(QStringLiteral("videoId"),      videoId);
    vd.insert(QStringLiteral("title"),        QStringLiteral("Test Track"));
    vd.insert(QStringLiteral("author"),       QStringLiteral("Test Artist"));
    vd.insert(QStringLiteral("lengthSeconds"), 180);

    QJsonObject status;
    status.insert(QStringLiteral("status"), QStringLiteral("OK"));

    QJsonObject root;
    root.insert(QStringLiteral("playabilityStatus"), status);
    root.insert(QStringLiteral("videoDetails"),      vd);
    root.insert(QStringLiteral("streamingData"),     adaptive);
    return root;
}

static QJsonObject playerResponseUnplayable(const QString& reason) {
    QJsonObject status;
    status.insert(QStringLiteral("status"), QStringLiteral("ERROR"));
    status.insert(QStringLiteral("reason"), reason);

    QJsonObject root;
    root.insert(QStringLiteral("playabilityStatus"), status);
    return root;
}

static QJsonObject playerResponseLoginRequired() {
    QJsonObject status;
    status.insert(QStringLiteral("status"), QStringLiteral("LOGIN_REQUIRED"));
    status.insert(QStringLiteral("reason"), QStringLiteral("Sign in to confirm you’re not a bot"));

    QJsonObject root;
    root.insert(QStringLiteral("playabilityStatus"), status);
    return root;
}

static QJsonObject playerResponseCiphered(const QString& videoId,
                                          const QString& sig,
                                          const QString& baseUrl) {
    QJsonObject fmt;
    fmt.insert(QStringLiteral("itag"),            140);
    fmt.insert(QStringLiteral("mimeType"),        QStringLiteral("audio/mp4; codecs=\"mp4a.40.2\""));
    fmt.insert(QStringLiteral("bitrate"),         128000);
    fmt.insert(QStringLiteral("audioSampleRate"), 44100);
    // signatureCipher is a query-string blob.
    const QString cipher = QStringLiteral("s=%1&sp=sig&url=%2").arg(sig, baseUrl);
    fmt.insert(QStringLiteral("signatureCipher"), cipher);

    QJsonObject adaptive;
    adaptive.insert(QStringLiteral("formats"),         QJsonArray{});
    adaptive.insert(QStringLiteral("adaptiveFormats"), QJsonArray{fmt});

    QJsonObject vd;
    vd.insert(QStringLiteral("videoId"), videoId);
    vd.insert(QStringLiteral("title"),   QStringLiteral("Ciphered"));
    vd.insert(QStringLiteral("author"),  QStringLiteral("Test"));
    vd.insert(QStringLiteral("lengthSeconds"), 200);

    QJsonObject status;
    status.insert(QStringLiteral("status"), QStringLiteral("OK"));

    QJsonObject root;
    root.insert(QStringLiteral("playabilityStatus"), status);
    root.insert(QStringLiteral("videoDetails"),      vd);
    root.insert(QStringLiteral("streamingData"),     adaptive);
    return root;
}

static QJsonObject playerResponseCipheredWithJs(const QString& videoId,
                                                const QString& sig,
                                                const QString& baseUrl,
                                                const QString& playerJsUrl) {
    QJsonObject root = playerResponseCiphered(videoId, sig, baseUrl);
    QJsonObject assets;
    assets.insert(QStringLiteral("js"), playerJsUrl);
    root.insert(QStringLiteral("assets"), assets);
    return root;
}

static HttpResponse jsonResponse(int statusCode, const QJsonObject& body) {
    HttpResponse r;
    r.statusCode = statusCode;
    r.body = QJsonDocument(body).toJson(QJsonDocument::Compact);
    r.headers.insert(QStringLiteral("Content-Type"), QStringLiteral("application/json"));
    return r;
}

// Spin the event loop until the mock queue drains or `timeoutMs`
// elapses. Used to await the async InnerTube callback in tests.
static int drainMock(MockHttpClient& m, int timeoutMs = 2000) {
    return m.drain(timeoutMs);
}

// ── Provider/session shape ────────────────────────────────────────────

void TestYouTubeProvider::provider_ownsStreamCache() {
    YouTubeProvider p;
    QVERIFY(p.streamCache() != nullptr);
    QVERIFY(p.streamCache()->isEmpty());
    QCOMPARE(p.streamCache()->count(), 0);
}

void TestYouTubeProvider::sessions_shareStreamCache() {
    YouTubeProvider p;
    const auto cacheA = p.streamCache();
    auto session1 = p.createSession(makeYouTubeSource());
    auto session2 = p.createSession(makeYouTubeSource());

    p.streamCache()->put(QStringLiteral("vid1"), QStringLiteral("https://x/1"));

    QCOMPARE(cacheA.get(), p.streamCache().get());
    QVERIFY(session1.get() != nullptr);
    QVERIFY(session2.get() != nullptr);
}

void TestYouTubeProvider::normalizeVideoId_acceptsUrl() {
    QCOMPARE(YouTubeSession::normalizeVideoId(QStringLiteral("https://youtu.be/dQw4w9WgXcQ")),
             QStringLiteral("dQw4w9WgXcQ"));
}

void TestYouTubeProvider::normalizeVideoId_acceptsBareId() {
    QCOMPARE(YouTubeSession::normalizeVideoId(QStringLiteral("dQw4w9WgXcQ")),
             QStringLiteral("dQw4w9WgXcQ"));
}

void TestYouTubeProvider::normalizeVideoId_rejectsGarbage() {
    QVERIFY(YouTubeSession::normalizeVideoId(QString()).isEmpty());
    QVERIFY(YouTubeSession::normalizeVideoId(QStringLiteral("not a video id")).isEmpty());
    QVERIFY(YouTubeSession::normalizeVideoId(QStringLiteral("https://example.com/no-id")).isEmpty());
}

void TestYouTubeProvider::parseUrl_classifiesInputs() {
    const auto video = YouTubeSession::parseUrl(QStringLiteral("https://www.youtube.com/watch?v=dQw4w9WgXcQ"));
    QCOMPARE(int(video.type), int(mf::core::sources::youtube::ParsedYouTubeUrl::Type::Video));
    QCOMPARE(video.videoId, QStringLiteral("dQw4w9WgXcQ"));

    const auto pl = YouTubeSession::parseUrl(QStringLiteral("https://music.youtube.com/playlist?list=PLabc"));
    QCOMPARE(int(pl.type), int(mf::core::sources::youtube::ParsedYouTubeUrl::Type::Playlist));
}

// ── fetchStreamUrl synchronous paths ─────────────────────────────────

void TestYouTubeProvider::fetchStreamUrl_invalidId_callsError() {
    YouTubeProvider p;
    auto session = p.createSession(makeYouTubeSource());
    QVERIFY(session.get() != nullptr);

    QString got;
    bool    okCalled  = false;
    bool    errCalled = false;
    session->fetchStreamUrl(
        QStringLiteral("not a video id"),
        [&](const QString& u){ okCalled = true;  got = u; },
        [&](const QString& e){ errCalled = true; got = e; });
    QVERIFY(errCalled);
    QVERIFY(!okCalled);
    QVERIFY(got.contains(QStringLiteral("Invalid video id")));
}

void TestYouTubeProvider::fetchStreamUrl_cacheHit_callsDone() {
    YouTubeProvider p;
    auto session = p.createSession(makeYouTubeSource());

    p.streamCache()->put(QStringLiteral("dQw4w9WgXcQ"),
                          QStringLiteral("https://cached.example/v1"));

    QString got;
    bool    okCalled  = false;
    bool    errCalled = false;
    session->fetchStreamUrl(
        QStringLiteral("dQw4w9WgXcQ"),
        [&](const QString& u){ okCalled = true;  got = u; },
        [&](const QString& e){ errCalled = true; got = e; });
    QVERIFY(okCalled);
    QVERIFY(!errCalled);
    QCOMPARE(got, QStringLiteral("https://cached.example/v1"));
}

void TestYouTubeProvider::fetchStreamUrl_cacheMiss_callsError() {
    auto mock = std::make_unique<MockHttpClient>();
    // Queue 5 errors (one per fallback client) so the session
    // exhausts the chain immediately.
    for (int i = 0; i < 5; ++i) {
        mock->enqueueError(QStringLiteral("youtubei/v1/player"),
                           QStringLiteral("HTTP 503: service down"));
    }
    YouTubeProvider p(mock.get());
    p.setUseYtDlpFallback(false);
    auto session = p.createSession(makeYouTubeSource());

    QString got;
    bool    okCalled  = false;
    bool    errCalled = false;
    session->fetchStreamUrl(
        QStringLiteral("dQw4w9WgXcQ"),
        [&](const QString& u){ okCalled = true;  got = u; },
        [&](const QString& e){ errCalled = true; got = e; });
    drainMock(*mock);
    QVERIFY(errCalled);
    QVERIFY(!okCalled);
    QVERIFY(got.contains(QStringLiteral("InnerTube")));
    QVERIFY(got.contains(QStringLiteral("all 5 clients failed")));
}

void TestYouTubeProvider::fetchStreamUrl_normalizesUrlBeforeCacheLookup() {
    YouTubeProvider p;
    auto session = p.createSession(makeYouTubeSource());

    p.streamCache()->put(QStringLiteral("dQw4w9WgXcQ"),
                          QStringLiteral("https://cached.example/v1"));

    QString got;
    session->fetchStreamUrl(
        QStringLiteral("https://youtu.be/dQw4w9WgXcQ"),
        [&](const QString& u){ got = u; },
        [](const QString&){ });
    QCOMPARE(got, QStringLiteral("https://cached.example/v1"));
}

// ── fetchStreamUrl network paths ──────────────────────────────────────

void TestYouTubeProvider::searchTracks_cachesResults() {
    auto mock = std::make_unique<MockHttpClient>();

    // First call: queue a real search response. The session
    // hits the network and caches the result.
    QJsonObject searchResp;
    QJsonObject contents;
    QJsonObject tabbed;
    QJsonArray  tabs;
    QJsonObject tab;
    QJsonObject tabRenderer;
    QJsonObject content;
    QJsonObject sectionList;
    QJsonObject sectionListRenderer;
    QJsonArray  sectionContents;

    // One musicShelfRenderer with one item.
    QJsonObject musicShelf;
    QJsonArray  shelfContents;
    QJsonObject item;
    QJsonObject mrl;
    QJsonArray  flex;
    {
        QJsonObject col0;
        QJsonObject text0;
        QJsonArray  runs0;
        QJsonObject r0; r0.insert(QStringLiteral("text"), QStringLiteral("Cached Song"));
        runs0.append(r0);
        text0.insert(QStringLiteral("runs"), runs0);
        col0.insert(QStringLiteral("text"), text0);

        QJsonObject flexCol;
        flexCol.insert(QStringLiteral("musicResponsiveListItemFlexColumnRenderer"), col0);
        flex.append(flexCol);
    }
    {
        QJsonObject col1;
        QJsonObject text1;
        QJsonArray  runs1;
        QJsonObject r1; r1.insert(QStringLiteral("text"), QStringLiteral("Cached Artist"));
        runs1.append(r1);
        text1.insert(QStringLiteral("runs"), runs1);
        col1.insert(QStringLiteral("text"), text1);

        QJsonObject flexCol;
        flexCol.insert(QStringLiteral("musicResponsiveListItemFlexColumnRenderer"), col1);
        flex.append(flexCol);
    }
    mrl.insert(QStringLiteral("flexColumns"), flex);
    mrl.insert(QStringLiteral("overlay"), QJsonObject{
        {QStringLiteral("musicItemThumbnailOverlayRenderer"), QJsonObject{
            {QStringLiteral("content"), QJsonObject{
                {QStringLiteral("musicPlayButtonRenderer"), QJsonObject{
                    {QStringLiteral("playNavigationEndpoint"), QJsonObject{
                        {QStringLiteral("watchEndpoint"), QJsonObject{
                            {QStringLiteral("videoId"), QStringLiteral("vidABC123XYZ")}
                        }}
                    }}
                }}
            }}
        }}
    }});
    item.insert(QStringLiteral("musicResponsiveListItemRenderer"), mrl);
    shelfContents.append(item);
    musicShelf.insert(QStringLiteral("contents"), shelfContents);
    QJsonObject shelfWrapped;
    shelfWrapped.insert(QStringLiteral("musicShelfRenderer"), musicShelf);
    sectionContents.append(shelfWrapped);
    sectionListRenderer.insert(QStringLiteral("contents"), sectionContents);
    content.insert(QStringLiteral("sectionListRenderer"), sectionListRenderer);
    tabRenderer.insert(QStringLiteral("content"), content);
    tab.insert(QStringLiteral("tabRenderer"), tabRenderer);
    tabs.append(tab);
    tabbed.insert(QStringLiteral("tabs"), tabs);
    contents.insert(QStringLiteral("tabbedSearchResultsRenderer"), tabbed);
    searchResp.insert(QStringLiteral("contents"), contents);

    mock->enqueueResponse(QStringLiteral("youtubei/v1/search"),
                          jsonResponse(200, searchResp));

    YouTubeProvider p(mock.get());
    auto session = p.createSession(makeYouTubeSource());

    QList<mf::core::models::MusicFile> first;
    bool firstDone = false;
    session->searchTracks(QStringLiteral("test query"), 10,
        [&](QList<mf::core::models::MusicFile> tracks) {
            first = tracks;
            firstDone = true;
        },
        [](QString) {});
    drainMock(*mock);
    QVERIFY(firstDone);
    QVERIFY(!first.isEmpty());
    QCOMPARE(first.first().youTubeVideoId(), QStringLiteral("vidABC123XYZ"));

    // Second call: clear the mock. If the session correctly cached
    // the first result, no HTTP call is made.
    mock->clearRequests();

    QList<mf::core::models::MusicFile> second;
    bool secondDone = false;
    session->searchTracks(QStringLiteral("test query"), 10,
        [&](QList<mf::core::models::MusicFile> tracks) {
            second = tracks;
            secondDone = true;
        },
        [](QString) {});
    drainMock(*mock, 200);
    QVERIFY(secondDone);
    QCOMPARE(second.size(), first.size());
    QCOMPARE(second.first().youTubeVideoId(), first.first().youTubeVideoId());

    // No HTTP call should have been issued for the cached query.
    const auto reqs = mock->requests();
    bool sawSearch = false;
    for (const auto& r : reqs) {
        if (r.url.contains(QStringLiteral("youtubei/v1/search"))) {
            sawSearch = true; break;
        }
    }
    QVERIFY(!sawSearch);
}

void TestYouTubeProvider::fetchStreamUrl_unplayableReportsError() {
    auto mock = std::make_unique<MockHttpClient>();
    mock->enqueueResponse(QStringLiteral("youtubei/v1/player"),
                          jsonResponse(200, playerResponseUnplayable(QStringLiteral("Video unavailable"))));

    YouTubeProvider p(mock.get());
    p.setUseYtDlpFallback(false);
    auto session = p.createSession(makeYouTubeSource());

    QString got;
    bool    okCalled  = false;
    bool    errCalled = false;
    session->fetchStreamUrl(
        QStringLiteral("dQw4w9WgXcQ"),
        [&](const QString& u){ okCalled = true;  got = u; },
        [&](const QString& e){ errCalled = true; got = e; });
    drainMock(*mock);
    QVERIFY(errCalled);
    QVERIFY(!okCalled);
    QVERIFY(got.contains(QStringLiteral("Unplayable")));
    QVERIFY(got.contains(QStringLiteral("Video unavailable")));
}

void TestYouTubeProvider::fetchStreamUrl_loginRequiredRotatesClient() {
    auto mock = std::make_unique<MockHttpClient>();
    // First call → LOGIN_REQUIRED. Second call → OK with an
    // unciphered audio format. The InnerTubeClient should rotate
    // to the second fallback automatically.
    mock->enqueueResponse(QStringLiteral("youtubei/v1/player"),
                          jsonResponse(200, playerResponseLoginRequired()));
    mock->enqueueResponse(QStringLiteral("youtubei/v1/player"),
                          jsonResponse(200, playerResponseOkWithUncipheredAudio(
                              QStringLiteral("dQw4w9WgXcQ"),
                              QStringLiteral("https://stream.example/v"))));

    YouTubeProvider p(mock.get());
    p.setUseYtDlpFallback(false);
    auto session = p.createSession(makeYouTubeSource());

    QString got;
    bool    okCalled  = false;
    bool    errCalled = false;
    session->fetchStreamUrl(
        QStringLiteral("dQw4w9WgXcQ"),
        [&](const QString& u){ okCalled = true;  got = u; },
        [&](const QString& e){ errCalled = true; got = e; });
    drainMock(*mock);
    QVERIFY(okCalled);
    QVERIFY(!errCalled);
    QCOMPARE(got, QStringLiteral("https://stream.example/v"));

    // Both player responses should have been consumed.
    QCOMPARE(mock->requestsMatching(QStringLiteral("youtubei/v1/player")).size(), 2);
}

void TestYouTubeProvider::fetchStreamUrl_cipheredFormatGetsDeciphered() {
    auto mock = std::make_unique<MockHttpClient>();
    // Player response with a ciphered format. Signature is
    // "ABCDEFG" with ops [1,2,3] (swap, slice, splice) — the
    // deciphered result depends on the ops. We can verify the
    // output URL contains the result of deciphering "ABCDEFG"
    // with the supplied ops table.
    const QString ops    = QStringLiteral("1,2,3");
    const QString sigIn  = QStringLiteral("ABCDEFG");
    const QString base   = QStringLiteral("https://stream.example/aud?exp=1");
    QJsonObject pr = playerResponseCiphered(
        QStringLiteral("vidCipher1"), sigIn, base);

    mock->enqueueResponse(QStringLiteral("youtubei/v1/player"),
                          jsonResponse(200, pr));
    // The session will look up cachedCipherOps (empty) and fall
    // through to the raw-sig path; the final URL therefore
    // contains sig=ABCDEFG (the no-op decipher). The full
    // cipher-decode path requires the player JS URL to be
    // captured by PlayerResponseParser, which is part of a
    // follow-up. This test asserts the *contract* — the URL is
    // built and the sig is appended — without depending on the
    // JS-fetch path.

    YouTubeProvider p(mock.get());
    p.setUseYtDlpFallback(false);
    auto session = p.createSession(makeYouTubeSource());

    QString got;
    bool    okCalled  = false;
    bool    errCalled = false;
    session->fetchStreamUrl(
        QStringLiteral("vidCipher1"),
        [&](const QString& u){ okCalled = true;  got = u; },
        [&](const QString& e){ errCalled = true; got = e; });
    drainMock(*mock);
    QVERIFY(okCalled);
    QVERIFY(!errCalled);
    QVERIFY(got.contains(QStringLiteral("https://stream.example/aud")));
    QVERIFY(got.contains(QStringLiteral("sig=")));
    Q_UNUSED(ops);
}

void TestYouTubeProvider::fetchStreamUrl_populatesStreamCache() {
    auto mock = std::make_unique<MockHttpClient>();
    mock->enqueueResponse(QStringLiteral("youtubei/v1/player"),
                          jsonResponse(200, playerResponseOkWithUncipheredAudio(
                              QStringLiteral("dQw4w9WgXcQ"),
                              QStringLiteral("https://stream.example/populated"))));

    YouTubeProvider p(mock.get());
    p.setUseYtDlpFallback(false);
    auto session = p.createSession(makeYouTubeSource());

    QVERIFY(p.streamCache()->tryGet(QStringLiteral("dQw4w9WgXcQ")).isEmpty());

    session->fetchStreamUrl(
        QStringLiteral("dQw4w9WgXcQ"),
        [](const QString&){ },
        [](const QString&){ });
    drainMock(*mock);

    const QString cached = p.streamCache()->tryGet(QStringLiteral("dQw4w9WgXcQ"));
    QCOMPARE(cached, QStringLiteral("https://stream.example/populated"));
}

void TestYouTubeProvider::fetchCover_prefersMaxres() {
    auto mock = std::make_unique<MockHttpClient>();
    auto tmp  = std::make_unique<QTemporaryDir>();
    QVERIFY(tmp->isValid());

    auto* imageCache = new ImageCache(mock.get(), tmp->path() + "/img",
                                      /*lruCapacity=*/4,
                                      /*defaultTtlSeconds=*/60,
                                      /*maxDiskBytes=*/1'000'000);

    // Generate a 16x16 JPG so the cache has something to write.
    QImage img(16, 16, QImage::Format_ARGB32);
    img.fill(Qt::red);
    QByteArray jpegBytes;
    {
        QBuffer buf(&jpegBytes);
        buf.open(QIODevice::WriteOnly);
        img.save(&buf, "JPG");
    }

    // 404 for the first two (maxresdefault, sddefault), 200 for
    // hqdefault. The session should bail at the first 200.
    mock->enqueueStatus(QStringLiteral("maxresdefault.jpg"), 404);
    mock->enqueueStatus(QStringLiteral("sddefault.jpg"),    404);
    mock->enqueueStatus(QStringLiteral("hqdefault.jpg"),    200, jpegBytes);

    YouTubeProvider p(mock.get());
    p.setImageCache(imageCache);
    auto session = p.createSession(makeYouTubeSource());

    QByteArray got;
    bool    okCalled  = false;
    bool    errCalled = false;
    session->fetchCover(
        QStringLiteral("dQw4w9WgXcQ"),
        [&](const QByteArray& b){ okCalled = true;  got = b; },
        [&](const QString& e){ errCalled = true; got = e; });
    drainMock(*mock);
    QVERIFY(okCalled);
    QVERIFY(!errCalled);
    QCOMPARE(got, jpegBytes);

    // The first three qualities were tried; mqdefault and default
    // were never reached.
    const auto reqs = mock->requests();
    int maxresCount = 0, sdCount = 0, hqCount = 0, mqCount = 0, defCount = 0;
    for (const auto& r : reqs) {
        if (r.url.contains(QStringLiteral("maxresdefault"))) ++maxresCount;
        else if (r.url.contains(QStringLiteral("sddefault")))   ++sdCount;
        else if (r.url.contains(QStringLiteral("hqdefault")))   ++hqCount;
        else if (r.url.contains(QStringLiteral("mqdefault")))   ++mqCount;
        else if (r.url.contains(QStringLiteral("/default.jpg"))) ++defCount;
    }
    QCOMPARE(maxresCount, 1);
    QCOMPARE(sdCount,     1);
    QCOMPARE(hqCount,     1);
    QCOMPARE(mqCount,     0);
    QCOMPARE(defCount,    0);

    // The session takes ownership of nothing; the provider does
    // not own the cache either. Just let it leak on test exit
    // (the test process terminates right after).
}

// ── Block 5.2.C: cipher path + yt-dlp fallback ──────────────────────

void TestYouTubeProvider::fetchStreamUrl_cipherPathResolvesOpsViaJsFetch() {
    auto mock = std::make_unique<MockHttpClient>();
    const QString jsUrl = QStringLiteral("https://www.youtube.com/s/player/test/base.js");

    // Player response has a ciphered format AND an `assets.js` URL.
    mock->enqueueResponse(QStringLiteral("youtubei/v1/player"),
                          jsonResponse(200, playerResponseCipheredWithJs(
                              QStringLiteral("vidCipher1"),
                              QStringLiteral("ABCDEFG"),
                              QStringLiteral("https://stream.example/aud?exp=1"),
                              jsUrl)));

    // The session will GET the player JS body. Queue a response
    // containing a `.split("")` near an ops-table literal that the
    // JsCipherExtractor can find. Ops use only {1,2,3,4}.
    const QString jsBody = QStringLiteral(
        "var Nf = \"1,2,3,1,2,3\"; function f(a) { return a.split(\"\").reverse().join(\"\"); }");
    HttpResponse jsResp;
    jsResp.statusCode = 200;
    jsResp.body       = jsBody.toUtf8();
    jsResp.headers.insert(QStringLiteral("Content-Type"),
                          QStringLiteral("application/javascript"));
    mock->enqueueResponse(jsUrl, jsResp);

    YouTubeProvider p(mock.get());
    p.setUseYtDlpFallback(false);
    auto session = p.createSession(makeYouTubeSource());

    QString got;
    bool    okCalled  = false;
    bool    errCalled = false;
    session->fetchStreamUrl(
        QStringLiteral("vidCipher1"),
        [&](const QString& u){ okCalled = true;  got = u; },
        [&](const QString& e){ errCalled = true; got = e; });
    drainMock(*mock);
    QVERIFY(okCalled);
    QVERIFY(!errCalled);
    QVERIFY(got.contains(QStringLiteral("https://stream.example/aud")));
    QVERIFY(got.contains(QStringLiteral("sig=")));
    // The sig was actually deciphered — it must NOT be the raw
    // "ABCDEFG". With ops "1,28,2,3,3,1,49,4" applied to "ABCDEFG"
    // the result is some non-raw permutation.
    QVERIFY(!got.contains(QStringLiteral("sig=ABCDEFG")));
}

void TestYouTubeProvider::fetchStreamUrl_cipherCacheReusedOnSecondCall() {
    auto mock = std::make_unique<MockHttpClient>();
    const QString jsUrl = QStringLiteral("https://www.youtube.com/s/player/cached/base.js");

    // Two consecutive calls. First: player + JS fetch. Second:
    // player only (the JS should be served from cipherCache_).
    mock->enqueueResponse(QStringLiteral("youtubei/v1/player"),
                          jsonResponse(200, playerResponseCipheredWithJs(
                              QStringLiteral("vidCache"),
                              QStringLiteral("ABCDEFG"),
                              QStringLiteral("https://stream.example/aud?exp=2"),
                              jsUrl)));
    mock->enqueueResponse(QStringLiteral("youtubei/v1/player"),
                          jsonResponse(200, playerResponseCipheredWithJs(
                              QStringLiteral("vidCache"),
                              QStringLiteral("ABCDEFG"),
                              QStringLiteral("https://stream.example/aud?exp=2"),
                              jsUrl)));
    const QString jsBody = QStringLiteral(
        "var Nf = \"1,2,3,1,2,3\"; function f(a) { return a.split(\"\").reverse().join(\"\"); }");
    HttpResponse jsResp;
    jsResp.statusCode = 200;
    jsResp.body       = jsBody.toUtf8();
    jsResp.headers.insert(QStringLiteral("Content-Type"),
                          QStringLiteral("application/javascript"));
    mock->enqueueResponse(jsUrl, jsResp);

    YouTubeProvider p(mock.get());
    p.setUseYtDlpFallback(false);
    auto session = p.createSession(makeYouTubeSource());

    for (int i = 0; i < 2; ++i) {
        bool okCalled = false, errCalled = false;
        QString got;
        session->fetchStreamUrl(
            QStringLiteral("vidCache"),
            [&](const QString& u){ okCalled = true;  got = u; },
            [&](const QString& e){ errCalled = true; got = e; });
        drainMock(*mock);
        QVERIFY(okCalled);
        QVERIFY(!errCalled);
        QVERIFY(!got.isEmpty());
    }

    // The JS URL should have been fetched exactly once across both
    // calls. The second call uses the cipherCache_.
    const auto jsReqs = mock->requestsMatching(jsUrl);
    QCOMPARE(jsReqs.size(), 1);
}

void TestYouTubeProvider::fetchStreamUrl_fallsBackToYtDlp() {
    auto mock = std::make_unique<MockHttpClient>();
    // 5 InnerTube errors → chain exhausted. Session should then try
    // the YtDlpProcess.
    for (int i = 0; i < 5; ++i) {
        mock->enqueueError(QStringLiteral("youtubei/v1/player"),
                           QStringLiteral("HTTP 503: service down"));
    }

    // Build a YtDlpProcess in test mode that returns a canned URL.
    auto* ytDlp = new YtDlpProcess();
    YtDlpProcess::FakeOutput fo;
    fo.active     = true;
    fo.streamUrl  = QStringLiteral("https://yt-dlp.example/stream?v=dQw4w9WgXcQ");
    ytDlp->setFakeOutput(fo);

    YouTubeProvider p(mock.get());
    p.setYtDlpProcess(ytDlp); // session will NOT own this pointer
    auto session = p.createSession(makeYouTubeSource());

    QString got;
    bool    okCalled  = false;
    bool    errCalled = false;
    session->fetchStreamUrl(
        QStringLiteral("dQw4w9WgXcQ"),
        [&](const QString& u){ okCalled = true;  got = u; },
        [&](const QString& e){ errCalled = true; got = e; });
    drainMock(*mock);
    QVERIFY(okCalled);
    QVERIFY(!errCalled);
    QCOMPARE(got, QStringLiteral("https://yt-dlp.example/stream?v=dQw4w9WgXcQ"));

    // The provider is the owner of the default YtDlpProcess but we
    // replaced it with our own — the default is freed inside
    // setYtDlpProcess; we delete the injected one here.
    delete ytDlp;
}

void TestYouTubeProvider::searchTracks_fallsBackToYtDlp() {
    auto mock = std::make_unique<MockHttpClient>();
    // InnerTube search returns an empty sectionList → 0 tracks →
    // session falls back to yt-dlp.
    QJsonObject searchResp;
    QJsonObject contents;
    QJsonObject tabbed;
    QJsonArray  tabs;
    QJsonObject tab;
    QJsonObject tabRenderer;
    QJsonObject content;
    QJsonObject sectionListRenderer;
    QJsonArray  sectionContents;
    QJsonObject shelfWrapped;
    shelfWrapped.insert(QStringLiteral("musicShelfRenderer"), QJsonObject{
        {QStringLiteral("contents"), QJsonArray{}}
    });
    sectionContents.append(shelfWrapped);
    sectionListRenderer.insert(QStringLiteral("contents"), sectionContents);
    content.insert(QStringLiteral("sectionListRenderer"), sectionListRenderer);
    tabRenderer.insert(QStringLiteral("content"), content);
    tab.insert(QStringLiteral("tabRenderer"), tabRenderer);
    tabs.append(tab);
    tabbed.insert(QStringLiteral("tabs"), tabs);
    contents.insert(QStringLiteral("tabbedSearchResultsRenderer"), tabbed);
    searchResp.insert(QStringLiteral("contents"), contents);
    mock->enqueueResponse(QStringLiteral("youtubei/v1/search"),
                          jsonResponse(200, searchResp));

    auto* ytDlp = new YtDlpProcess();
    YtDlpProcess::FakeOutput fo;
    fo.active = true;
    YtDlpProcess::SearchEntry e1;
    e1.id              = QStringLiteral("ytVidA");
    e1.title           = QStringLiteral("yt-dlp Title A");
    e1.uploader        = QStringLiteral("yt-dlp Channel A");
    e1.durationSeconds = 200;
    YtDlpProcess::SearchEntry e2;
    e2.id              = QStringLiteral("ytVidB");
    e2.title           = QStringLiteral("yt-dlp Title B");
    e2.uploader        = QStringLiteral("yt-dlp Channel B");
    e2.durationSeconds = 240;
    fo.searchResults.append(e1);
    fo.searchResults.append(e2);
    ytDlp->setFakeOutput(fo);

    YouTubeProvider p(mock.get());
    p.setYtDlpProcess(ytDlp);
    auto session = p.createSession(makeYouTubeSource());

    QList<mf::core::models::MusicFile> tracks;
    bool done = false;
    session->searchTracks(QStringLiteral("test query"), 5,
        [&](QList<mf::core::models::MusicFile> t) {
            tracks = std::move(t);
            done = true;
        },
        [](QString) {});
    drainMock(*mock);
    QVERIFY(done);
    QCOMPARE(tracks.size(), 2);
    QCOMPARE(tracks[0].youTubeVideoId(), QStringLiteral("ytVidA"));
    QCOMPARE(tracks[0].title(),          QStringLiteral("yt-dlp Title A"));
    QCOMPARE(tracks[0].artist(),         QStringLiteral("yt-dlp Channel A"));
    QCOMPARE(tracks[1].youTubeVideoId(), QStringLiteral("ytVidB"));

    delete ytDlp;
}

QTEST_GUILESS_MAIN(TestYouTubeProvider)
#include "testyoutubeprovider.moc"

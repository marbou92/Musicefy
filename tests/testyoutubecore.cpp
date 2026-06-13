// testyoutubecore.cpp
// Unit tests for the deterministic YouTube primitives:
//   • YouTubeUrlParser — 5 video patterns + playlist/artist/album
//   • Cipher — parseOperations + apply + decipher
//   • StreamCache — TTL, get/put, expiry, purge
//   • PlayerResponseParser — DTO + classifyItag
//   • YouTubeExtractor — pickBestAudio + buildUrl
//
// All tests are pure (no network, no GUI). The QTEST_GUILESS_MAIN
// harness is sufficient.

#include <QtTest>
#include <QCoreApplication>

#include <thread>

#include "sources/youtube/YouTubeUrlParser.h"
#include "sources/youtube/Cipher.h"
#include "sources/youtube/StreamCache.h"
#include "sources/youtube/PlayerResponse.h"
#include "sources/youtube/YouTubeExtractor.h"

using mf::core::sources::youtube::YouTubeUrlParser;
using mf::core::sources::youtube::ParsedYouTubeUrl;
using mf::core::sources::youtube::Cipher;
using mf::core::sources::youtube::StreamCache;
using mf::core::sources::youtube::PlayerResponse;
using mf::core::sources::youtube::PlayerResponseParser;
using mf::core::sources::youtube::YouTubeExtractor;

class TestYouTubeCore : public QObject {
    Q_OBJECT
private slots:
    // ── UrlParser ────────────────────────────────────────────────────
    void parseWatchUrl();
    void parseShortUrl();
    void parseShortsUrl();
    void parseEmbedUrl();
    void parseMusicWatchUrl();
    void parsePlaylistUrl();
    void parseChannelUrl();
    void parseBrowseAlbumUrl();
    void parseUnknownUrl();
    void extractVideoId_acceptsBareId();
    void extractVideoId_rejectsShortId();
    void isValidVideoId_boundaries();
    void createWatchUrl_roundTrip();

    // ── Cipher ───────────────────────────────────────────────────────
    void cipher_parse_empty();
    void cipher_parse_swap();
    void cipher_parse_slice();
    void cipher_parse_splice();
    void cipher_parse_reverse();
    void cipher_parse_mixed();
    void cipher_parse_invalidOp_returnsEmpty();
    void cipher_parse_truncatedSlice_returnsEmpty();
    void cipher_apply_swap();
    void cipher_apply_reverse();
    void cipher_apply_slice();
    void cipher_apply_splice();
    void cipher_decipher_integration();
    void cipher_decipher_emptyInputs_returnEmpty();
    void cipher_nParamDecode_passthrough();

    // ── StreamCache ──────────────────────────────────────────────────
    void streamCache_putAndGet();
    void streamCache_missReturnsEmpty();
    void streamCache_respectsTtl();
    void streamCache_clampsToMaxExpiry();
    void streamCache_remove();
    void streamCache_purgeExpired();
    void streamCache_emptyInputsIgnored();
    void streamCache_threadSafe_basic();

    // ── PlayerResponseParser ─────────────────────────────────────────
    void playerResponse_classifyItag_audioOnly();
    void playerResponse_classifyItag_progressive();
    void playerResponse_classifyItag_videoOnly();
    void playerResponse_parse_unplayableStatus();
    void playerResponse_parse_signaturedCipher();
    void playerResponse_parse_combinesFormats();
    void playerResponse_parse_capturesAssetsJsAtRoot();
    void playerResponse_parse_capturesAssetsJsNested();

    // ── YouTubeExtractor ─────────────────────────────────────────────
    void extractor_picksAudioOnlyOverProgressive();
    void extractor_returnsUncipheredUrlAsIs();
    void extractor_deciphersCipheredFormat();
    void extractor_noAudioFormats_reportsError();
    void extractor_unplayable_reportsError();
    void extractor_cipheredNoCallback_reportsError();
    void extractor_cipheredCallbackFails_reportsError();
    void extractor_buildUrl_appendsSig();
    void extractor_buildUrl_usesCustomSigParam();

    // ── YouTubeExtractor::bestThumbnailUrl ──────────────────────────
    void extractor_bestThumb_returnsFirstForEmptyArray();
    void extractor_bestThumb_picksExactMatch();
    void extractor_bestThumb_picksClosestOver();
    void extractor_bestThumb_fallsBackToLargestUnder();
    void extractor_bestThumb_usesFirstAsLastResort();
};

// ── UrlParser ────────────────────────────────────────────────────────

void TestYouTubeCore::parseWatchUrl() {
    const auto p = YouTubeUrlParser::parse(QStringLiteral("https://www.youtube.com/watch?v=dQw4w9WgXcQ"));
    QCOMPARE(int(p.type), int(ParsedYouTubeUrl::Type::Video));
    QCOMPARE(p.videoId, QStringLiteral("dQw4w9WgXcQ"));
}

void TestYouTubeCore::parseShortUrl() {
    const auto p = YouTubeUrlParser::parse(QStringLiteral("https://youtu.be/dQw4w9WgXcQ"));
    QCOMPARE(int(p.type), int(ParsedYouTubeUrl::Type::Video));
    QCOMPARE(p.videoId, QStringLiteral("dQw4w9WgXcQ"));
}

void TestYouTubeCore::parseShortsUrl() {
    const auto p = YouTubeUrlParser::parse(QStringLiteral("https://www.youtube.com/shorts/abcdef01234"));
    QCOMPARE(int(p.type), int(ParsedYouTubeUrl::Type::Video));
    QCOMPARE(p.videoId, QStringLiteral("abcdef01234"));
}

void TestYouTubeCore::parseEmbedUrl() {
    const auto p = YouTubeUrlParser::parse(QStringLiteral("https://www.youtube.com/embed/dQw4w9WgXcQ"));
    QCOMPARE(int(p.type), int(ParsedYouTubeUrl::Type::Video));
    QCOMPARE(p.videoId, QStringLiteral("dQw4w9WgXcQ"));
}

void TestYouTubeCore::parseMusicWatchUrl() {
    const auto p = YouTubeUrlParser::parse(QStringLiteral("https://music.youtube.com/watch?v=dQw4w9WgXcQ"));
    QCOMPARE(int(p.type), int(ParsedYouTubeUrl::Type::Video));
    QCOMPARE(p.videoId, QStringLiteral("dQw4w9WgXcQ"));
}

void TestYouTubeCore::parsePlaylistUrl() {
    const auto p = YouTubeUrlParser::parse(QStringLiteral("https://www.youtube.com/playlist?list=PLrAXtmRdnEQy6nuLMHjMZOz59Oq"));
    QCOMPARE(int(p.type), int(ParsedYouTubeUrl::Type::Playlist));
    QVERIFY(p.playlistId.startsWith(QStringLiteral("PL")));
}

void TestYouTubeCore::parseChannelUrl() {
    const auto p = YouTubeUrlParser::parse(QStringLiteral("https://music.youtube.com/channel/UCabcdefghijklmnopqrstuv"));
    QCOMPARE(int(p.type), int(ParsedYouTubeUrl::Type::Artist));
    QVERIFY(p.browseId.startsWith(QStringLiteral("UC")));
}

void TestYouTubeCore::parseBrowseAlbumUrl() {
    const auto p = YouTubeUrlParser::parse(QStringLiteral("https://music.youtube.com/browse/MPREb_AbCdEfGhIjK"));
    QCOMPARE(int(p.type), int(ParsedYouTubeUrl::Type::Album));
    QVERIFY(p.browseId.startsWith(QStringLiteral("MPRE")));
}

void TestYouTubeCore::parseUnknownUrl() {
    const auto p = YouTubeUrlParser::parse(QStringLiteral("https://example.com/something-else"));
    QCOMPARE(int(p.type), int(ParsedYouTubeUrl::Type::Unknown));
    QVERIFY(!p.isValid());
}

void TestYouTubeCore::extractVideoId_acceptsBareId() {
    QCOMPARE(YouTubeUrlParser::extractVideoId(QStringLiteral("dQw4w9WgXcQ")),
             QStringLiteral("dQw4w9WgXcQ"));
    QCOMPARE(YouTubeUrlParser::extractVideoId(QStringLiteral("https://youtu.be/dQw4w9WgXcQ")),
             QStringLiteral("dQw4w9WgXcQ"));
}

void TestYouTubeCore::extractVideoId_rejectsShortId() {
    // 10 chars — invalid.
    QCOMPARE(YouTubeUrlParser::extractVideoId(QStringLiteral("dQw4w9WgXc")),
             QString());
}

void TestYouTubeCore::isValidVideoId_boundaries() {
    QVERIFY(YouTubeUrlParser::isValidVideoId(QStringLiteral("dQw4w9WgXcQ")));
    QVERIFY(YouTubeUrlParser::isValidVideoId(QStringLiteral("_-_-_-_-_-a")));
    QVERIFY(!YouTubeUrlParser::isValidVideoId(QStringLiteral("")));
    QVERIFY(!YouTubeUrlParser::isValidVideoId(QStringLiteral("too_short")));
    QVERIFY(!YouTubeUrlParser::isValidVideoId(QStringLiteral("contains spaces!")));
    QVERIFY(!YouTubeUrlParser::isValidVideoId(QStringLiteral("a/b\\c.d")));
}

void TestYouTubeCore::createWatchUrl_roundTrip() {
    const QString url = YouTubeUrlParser::createWatchUrl(QStringLiteral("dQw4w9WgXcQ"));
    QCOMPARE(url, QStringLiteral("https://music.youtube.com/watch?v=dQw4w9WgXcQ"));
    const auto p = YouTubeUrlParser::parse(url);
    QCOMPARE(p.videoId, QStringLiteral("dQw4w9WgXcQ"));
}

// ── Cipher ───────────────────────────────────────────────────────────

void TestYouTubeCore::cipher_parse_empty() {
    const auto ops = Cipher::parseOperations(QString());
    QVERIFY(ops.empty());
}

void TestYouTubeCore::cipher_parse_swap() {
    // op 1 = Swap(45)
    const auto ops = Cipher::parseOperations(QStringLiteral("1,45"));
    QCOMPARE(ops.size(), std::size_t{1});
    QCOMPARE(int(ops[0].kind), int(Cipher::Op::Swap));
    QCOMPARE(ops[0].a, 45);
}

void TestYouTubeCore::cipher_parse_slice() {
    // op 2 = Slice(28)
    const auto ops = Cipher::parseOperations(QStringLiteral("2,28"));
    QCOMPARE(ops.size(), std::size_t{1});
    QCOMPARE(int(ops[0].kind), int(Cipher::Op::Slice));
    QCOMPARE(ops[0].a, 28);
}

void TestYouTubeCore::cipher_parse_splice() {
    // op 3 = Splice(3, 2)
    const auto ops = Cipher::parseOperations(QStringLiteral("3,3,2"));
    QCOMPARE(ops.size(), std::size_t{1});
    QCOMPARE(int(ops[0].kind), int(Cipher::Op::Splice));
    QCOMPARE(ops[0].a, 3);
    QCOMPARE(ops[0].b, 2);
}

void TestYouTubeCore::cipher_parse_reverse() {
    // op 4 = Reverse
    const auto ops = Cipher::parseOperations(QStringLiteral("4"));
    QCOMPARE(ops.size(), std::size_t{1});
    QCOMPARE(int(ops[0].kind), int(Cipher::Op::Reverse));
}

void TestYouTubeCore::cipher_parse_mixed() {
    // swap 28, slice 3, splice 1 49, reverse
    const auto ops = Cipher::parseOperations(QStringLiteral("1,28,2,3,3,1,49,4"));
    QCOMPARE(ops.size(), std::size_t{4});
    QCOMPARE(int(ops[0].kind), int(Cipher::Op::Swap));
    QCOMPARE(ops[0].a, 28);
    QCOMPARE(int(ops[1].kind), int(Cipher::Op::Slice));
    QCOMPARE(ops[1].a, 3);
    QCOMPARE(int(ops[2].kind), int(Cipher::Op::Splice));
    QCOMPARE(ops[2].a, 1);
    QCOMPARE(ops[2].b, 49);
    QCOMPARE(int(ops[3].kind), int(Cipher::Op::Reverse));
}

void TestYouTubeCore::cipher_parse_invalidOp_returnsEmpty() {
    // 99 is not a recognised op
    QCOMPARE(Cipher::parseOperations(QStringLiteral("99,42")).size(), std::size_t{0});
}

void TestYouTubeCore::cipher_parse_truncatedSlice_returnsEmpty() {
    // Op 2 needs an argument; missing → bail.
    QCOMPARE(Cipher::parseOperations(QStringLiteral("2")).size(), std::size_t{0});
}

void TestYouTubeCore::cipher_apply_swap() {
    const auto ops = Cipher::parseOperations(QStringLiteral("1,3"));
    QCOMPARE(Cipher::apply(QStringLiteral("ABCD"), ops),
             QStringLiteral("DBCA"));
}

void TestYouTubeCore::cipher_apply_reverse() {
    const auto ops = Cipher::parseOperations(QStringLiteral("4"));
    QCOMPARE(Cipher::apply(QStringLiteral("ABCDEF"), ops),
             QStringLiteral("FEDCBA"));
}

void TestYouTubeCore::cipher_apply_slice() {
    const auto ops = Cipher::parseOperations(QStringLiteral("2,3"));
    QCOMPARE(Cipher::apply(QStringLiteral("ABCDEFG"), ops),
             QStringLiteral("ABC"));
}

void TestYouTubeCore::cipher_apply_splice() {
    // remove 2 chars starting at pos 1 → "ADEFG"
    const auto ops = Cipher::parseOperations(QStringLiteral("3,1,2"));
    QCOMPARE(Cipher::apply(QStringLiteral("ABCDEFG"), ops),
             QStringLiteral("ADEFG"));
}

void TestYouTubeCore::cipher_decipher_integration() {
    // Take a string, swap 0 with 1, reverse, slice to 4.
    // Input "ABCDEFG" → "BACDEFG" → "GFEDCBA" → "GFED"
    const QString sig = QStringLiteral("ABCDEFG");
    const QString ops = QStringLiteral("1,1,4,2,4");
    QCOMPARE(Cipher::decipher(sig, ops), QStringLiteral("GFED"));
}

void TestYouTubeCore::cipher_decipher_emptyInputs_returnEmpty() {
    QVERIFY(Cipher::decipher(QString(), QStringLiteral("1,1")).isEmpty());
    QVERIFY(Cipher::decipher(QStringLiteral("ABCD"), QString()).isEmpty());
    QVERIFY(Cipher::decipher(QStringLiteral("ABCD"), QStringLiteral("99,1")).isEmpty());
}

void TestYouTubeCore::cipher_nParamDecode_passthrough() {
    // Empty table → input passes through.
    QCOMPARE(Cipher::nParamDecode(QStringLiteral("abc"), {}),
             QStringLiteral("abc"));
    // Empty input → empty.
    QCOMPARE(Cipher::nParamDecode(QString(), {}), QString());
}

// ── StreamCache ──────────────────────────────────────────────────────

void TestYouTubeCore::streamCache_putAndGet() {
    StreamCache c(/*ttlMs=*/60'000);
    c.put(QStringLiteral("vid1"), QStringLiteral("https://x.example/stream?v=1"),
          QStringLiteral("audio/webm"), 128'000, 21'540);
    QCOMPARE(c.tryGet(QStringLiteral("vid1")),
             QStringLiteral("https://x.example/stream?v=1"));
    QCOMPARE(c.count(), 1);
}

void TestYouTubeCore::streamCache_missReturnsEmpty() {
    StreamCache c;
    QVERIFY(c.tryGet(QStringLiteral("missing")).isEmpty());
    QVERIFY(c.isEmpty());
}

void TestYouTubeCore::streamCache_respectsTtl() {
    // 50ms TTL — entry should be gone after we wait it out.
    StreamCache c(/*ttlMs=*/50);
    c.put(QStringLiteral("vid1"), QStringLiteral("https://x"),
          {}, 0, 1 /* even 1 second, but TTL overrides */);
    QVERIFY(!c.tryGet(QStringLiteral("vid1")).isEmpty());
    QTest::qWait(80);
    QVERIFY(c.tryGet(QStringLiteral("vid1")).isEmpty());
}

void TestYouTubeCore::streamCache_clampsToMaxExpiry() {
    // A 24h expiry should be clamped to the default (5h). We don't
    // wait 5h; we just verify the cache still holds the entry.
    StreamCache c;
    c.put(QStringLiteral("vid1"), QStringLiteral("https://x"),
          {}, 0, /*expiresInSeconds=*/86'400);
    QVERIFY(!c.tryGet(QStringLiteral("vid1")).isEmpty());
}

void TestYouTubeCore::streamCache_remove() {
    StreamCache c;
    c.put(QStringLiteral("vid1"), QStringLiteral("https://x"));
    c.remove(QStringLiteral("vid1"));
    QVERIFY(c.tryGet(QStringLiteral("vid1")).isEmpty());
}

void TestYouTubeCore::streamCache_purgeExpired() {
    StreamCache c(/*ttlMs=*/30);
    c.put(QStringLiteral("vid1"), QStringLiteral("https://x"));
    c.put(QStringLiteral("vid2"), QStringLiteral("https://y"));
    QTest::qWait(50);
    c.purgeExpired();
    QCOMPARE(c.count(), 0);
}

void TestYouTubeCore::streamCache_emptyInputsIgnored() {
    StreamCache c;
    c.put(QString(), QStringLiteral("https://x"));
    c.put(QStringLiteral("vid1"), QString());
    QCOMPARE(c.count(), 0);
}

void TestYouTubeCore::streamCache_threadSafe_basic() {
    StreamCache c;
    // Smoke test: concurrent put/get from two threads must not crash.
    std::thread t1([&]{
        for (int i = 0; i < 100; ++i) {
            c.put(QStringLiteral("vid%1").arg(i), QStringLiteral("u%1").arg(i));
        }
    });
    std::thread t2([&]{
        for (int i = 0; i < 100; ++i) {
            c.tryGet(QStringLiteral("vid%1").arg(i));
        }
    });
    t1.join();
    t2.join();
    QVERIFY(c.count() > 0);
}

// ── PlayerResponseParser ─────────────────────────────────────────────

void TestYouTubeCore::playerResponse_classifyItag_audioOnly() {
    QCOMPARE(int(PlayerResponseParser::classifyItag(140, QStringLiteral("audio/mp4"))),
             int(PlayerFormat::Kind::AudioOnly));
    QCOMPARE(int(PlayerResponseParser::classifyItag(251, QStringLiteral("audio/webm"))),
             int(PlayerFormat::Kind::AudioOnly));
}

void TestYouTubeCore::playerResponse_classifyItag_progressive() {
    QCOMPARE(int(PlayerResponseParser::classifyItag(18, QStringLiteral("video/mp4"))),
             int(PlayerFormat::Kind::Progressive));
}

void TestYouTubeCore::playerResponse_classifyItag_videoOnly() {
    // 137 is video-only mp4
    QCOMPARE(int(PlayerResponseParser::classifyItag(137, QStringLiteral("video/mp4"))),
             int(PlayerFormat::Kind::VideoOnly));
    // mime starts with "audio/" but contains "video" → still videoOnly
    QCOMPARE(int(PlayerResponseParser::classifyItag(999, QStringLiteral("audio/x; video=y"))),
             int(PlayerFormat::Kind::VideoOnly));
}

void TestYouTubeCore::playerResponse_parse_unplayableStatus() {
    QJsonObject root;
    QJsonObject ps;
    ps.insert(QStringLiteral("status"), QStringLiteral("ERROR"));
    ps.insert(QStringLiteral("reason"), QStringLiteral("Video not available"));
    root.insert(QStringLiteral("playabilityStatus"), ps);

    const auto r = PlayerResponseParser::parse(root);
    QVERIFY(!r.isPlayable());
    QCOMPARE(int(r.status), int(PlayerResponse::PlayabilityStatus::Unplayable));
    QVERIFY(r.errorMessage.contains(QStringLiteral("not available")));
}

void TestYouTubeCore::playerResponse_parse_signaturedCipher() {
    // Simulate a streamingData with a ciphered audio format.
    QJsonObject root;
    QJsonObject ps;
    ps.insert(QStringLiteral("status"), QStringLiteral("OK"));
    root.insert(QStringLiteral("playabilityStatus"), ps);

    QJsonObject vd;
    vd.insert(QStringLiteral("videoId"), QStringLiteral("dQw4w9WgXcQ"));
    vd.insert(QStringLiteral("title"),   QStringLiteral("Test Track"));
    vd.insert(QStringLiteral("author"),  QStringLiteral("Test Channel"));
    root.insert(QStringLiteral("videoDetails"), vd);

    QJsonObject afmt;
    afmt.insert(QStringLiteral("itag"), 140);
    afmt.insert(QStringLiteral("bitrate"), 128'000);
    afmt.insert(QStringLiteral("mimeType"), QStringLiteral("audio/mp4; codecs=\"mp4a.40.2\""));
    afmt.insert(QStringLiteral("audioSampleRate"), 44100);
    afmt.insert(QStringLiteral("signatureCipher"),
                QStringLiteral("s=ABCDEFG&sp=sig&url=https://x.example/stream?itag=140"));

    QJsonArray af;
    af.append(afmt);

    QJsonObject streaming;
    streaming.insert(QStringLiteral("adaptiveFormats"), af);
    root.insert(QStringLiteral("streamingData"), streaming);

    const auto r = PlayerResponseParser::parse(root);
    QVERIFY(r.isPlayable());
    QCOMPARE(r.videoId, QStringLiteral("dQw4w9WgXcQ"));
    QCOMPARE(r.formats.size(), 1);
    const auto& f = r.formats.first();
    QVERIFY(f.isCiphered());
    QCOMPARE(f.signature, QStringLiteral("ABCDEFG"));
    QCOMPARE(f.signatureParam, QStringLiteral("sig"));
    QCOMPARE(f.url, QStringLiteral("https://x.example/stream?itag=140"));
    QCOMPARE(int(f.kind), int(PlayerFormat::Kind::AudioOnly));
}

void TestYouTubeCore::playerResponse_parse_combinesFormats() {
    QJsonObject root;
    QJsonObject ps;
    ps.insert(QStringLiteral("status"), QStringLiteral("OK"));
    root.insert(QStringLiteral("playabilityStatus"), ps);

    QJsonObject vd;
    vd.insert(QStringLiteral("videoId"), QStringLiteral("vid1"));
    root.insert(QStringLiteral("videoDetails"), vd);

    QJsonArray f1, f2;
    QJsonObject a; a.insert(QStringLiteral("itag"), 18);  f1.append(a);  // progressive
    QJsonObject b; b.insert(QStringLiteral("itag"), 140); f2.append(b);  // audio-only

    QJsonObject streaming;
    streaming.insert(QStringLiteral("formats"), f1);
    streaming.insert(QStringLiteral("adaptiveFormats"), f2);
    root.insert(QStringLiteral("streamingData"), streaming);

    const auto r = PlayerResponseParser::parse(root);
    QCOMPARE(r.formats.size(), 2);
}

void TestYouTubeCore::playerResponse_parse_capturesAssetsJsAtRoot() {
    // Canonical path: assets.js lives at the response root.
    QJsonObject root;
    QJsonObject ps;
    ps.insert(QStringLiteral("status"), QStringLiteral("OK"));
    root.insert(QStringLiteral("playabilityStatus"), ps);

    QJsonObject vd;
    vd.insert(QStringLiteral("videoId"), QStringLiteral("vidA"));
    root.insert(QStringLiteral("videoDetails"), vd);

    QJsonObject assets;
    assets.insert(QStringLiteral("js"),
                  QStringLiteral("https://www.youtube.com/s/player/abc123/base.js"));
    root.insert(QStringLiteral("assets"), assets);

    const auto r = PlayerResponseParser::parse(root);
    QVERIFY(r.hasPlayerJsUrl());
    QCOMPARE(r.playerJsUrl,
             QStringLiteral("https://www.youtube.com/s/player/abc123/base.js"));
}

void TestYouTubeCore::playerResponse_parse_capturesAssetsJsNested() {
    // TVHTML5 path: assets.js lives under playerConfig.assets.js.
    QJsonObject root;
    QJsonObject ps;
    ps.insert(QStringLiteral("status"), QStringLiteral("OK"));
    root.insert(QStringLiteral("playabilityStatus"), ps);

    QJsonObject vd;
    vd.insert(QStringLiteral("videoId"), QStringLiteral("vidB"));
    root.insert(QStringLiteral("videoDetails"), vd);

    QJsonObject assets;
    assets.insert(QStringLiteral("js"),
                  QStringLiteral("https://www.youtube.com/s/player/tvhtml5/base.js"));
    QJsonObject playerConfig;
    playerConfig.insert(QStringLiteral("assets"), assets);
    root.insert(QStringLiteral("playerConfig"), playerConfig);

    const auto r = PlayerResponseParser::parse(root);
    QVERIFY(r.hasPlayerJsUrl());
    QCOMPARE(r.playerJsUrl,
             QStringLiteral("https://www.youtube.com/s/player/tvhtml5/base.js"));
}

// ── YouTubeExtractor ─────────────────────────────────────────────────

static PlayerResponse makeResponse(QList<PlayerFormat> formats) {
    PlayerResponse r;
    r.videoId = QStringLiteral("vid1");
    r.title   = QStringLiteral("Test");
    r.formats = std::move(formats);
    r.status  = PlayerResponse::PlayabilityStatus::OK;
    return r;
}

void TestYouTubeCore::extractor_picksAudioOnlyOverProgressive() {
    PlayerFormat prog; prog.itag = 18; prog.kind = PlayerFormat::Kind::Progressive;
    prog.url = QStringLiteral("https://x/stream?itag=18");
    prog.bitrate = 100'000;
    PlayerFormat audio; audio.itag = 140; audio.kind = PlayerFormat::Kind::AudioOnly;
    audio.url = QStringLiteral("https://x/stream?itag=140");
    audio.bitrate = 128'000;
    audio.audioSampleRate = 44100;
    PlayerResponse r = makeResponse({prog, audio});

    QString err;
    const auto u = YouTubeExtractor::pickBestAudio(r, nullptr, &err);
    QVERIFY(u.contains(QStringLiteral("itag=140")));
    QVERIFY(err.isEmpty());
}

void TestYouTubeCore::extractor_returnsUncipheredUrlAsIs() {
    PlayerFormat audio; audio.itag = 140; audio.kind = PlayerFormat::Kind::AudioOnly;
    audio.url = QStringLiteral("https://x/stream?itag=140&direct=1");
    audio.bitrate = 128'000;
    PlayerResponse r = makeResponse({audio});
    QString err;
    QCOMPARE(YouTubeExtractor::pickBestAudio(r, nullptr, &err),
             QStringLiteral("https://x/stream?itag=140&direct=1"));
}

void TestYouTubeCore::extractor_deciphersCipheredFormat() {
    // No base url on the format → buildUrl returns empty string.
    // pickBestAudio surfaces that as an empty result, with no error
    // message (the failure mode is "format is unusable", not "bad
    // request"). The error-out field stays untouched.
    {
        PlayerFormat audio; audio.itag = 140; audio.kind = PlayerFormat::Kind::AudioOnly;
        audio.url = QString();
        audio.signature = QStringLiteral("ABCDEFG");
        audio.signatureParam = QStringLiteral("sig");
        audio.bitrate = 128'000;
        PlayerResponse r = makeResponse({audio});
        auto cipher = [](const QString& s) {
            return Cipher::decipher(s, QStringLiteral("4"));
        };
        QString err = QStringLiteral("sentinel");
        const auto u = YouTubeExtractor::pickBestAudio(r, cipher, &err);
        QVERIFY(u.isEmpty());
        QCOMPARE(err, QStringLiteral("sentinel")); // not overwritten
    }

    // With a real base url, the cipher is applied and the sig is
    // appended to the URL.
    {
        PlayerFormat audio; audio.itag = 140; audio.kind = PlayerFormat::Kind::AudioOnly;
        audio.url = QStringLiteral("https://x/stream?itag=140");
        audio.signature = QStringLiteral("ABCDEFG");
        audio.signatureParam = QStringLiteral("sig");
        audio.bitrate = 128'000;
        PlayerResponse r = makeResponse({audio});
        auto cipher = [](const QString& s) {
            return Cipher::decipher(s, QStringLiteral("4")); // reverse
        };
        QString err;
        const auto u = YouTubeExtractor::pickBestAudio(r, cipher, &err);
        QVERIFY(err.isEmpty());
        QVERIFY(u.contains(QStringLiteral("sig=GFEDCBA")));
    }
}

void TestYouTubeCore::extractor_noAudioFormats_reportsError() {
    PlayerFormat vid; vid.itag = 137; vid.kind = PlayerFormat::Kind::VideoOnly;
    vid.url = QStringLiteral("https://x/stream?itag=137");
    PlayerResponse r = makeResponse({vid});
    QString err;
    QVERIFY(YouTubeExtractor::pickBestAudio(r, nullptr, &err).isEmpty());
    QVERIFY(err.contains(QStringLiteral("No audio formats")));
}

void TestYouTubeCore::extractor_cipheredNoCallback_reportsError() {
    PlayerFormat audio; audio.itag = 140; audio.kind = PlayerFormat::Kind::AudioOnly;
    audio.url = QStringLiteral("https://x/stream?itag=140");
    audio.signature = QStringLiteral("ABCDEFG");
    audio.signatureParam = QStringLiteral("sig");
    audio.bitrate = 128'000;
    PlayerResponse r = makeResponse({audio});
    QString err;
    // nullptr decipher callback
    const auto u = YouTubeExtractor::pickBestAudio(r, nullptr, &err);
    QVERIFY(u.isEmpty());
    QVERIFY(err.contains(QStringLiteral("deobfuscator")));
}

void TestYouTubeCore::extractor_cipheredCallbackFails_reportsError() {
    PlayerFormat audio; audio.itag = 140; audio.kind = PlayerFormat::Kind::AudioOnly;
    audio.url = QStringLiteral("https://x/stream?itag=140");
    audio.signature = QStringLiteral("ABCDEFG");
    audio.signatureParam = QStringLiteral("sig");
    audio.bitrate = 128'000;
    PlayerResponse r = makeResponse({audio});
    auto bad = [](const QString&) { return QString(); };
    QString err;
    const auto u = YouTubeExtractor::pickBestAudio(r, bad, &err);
    QVERIFY(u.isEmpty());
    QVERIFY(err.contains(QStringLiteral("deobfuscation failed")));
}

void TestYouTubeCore::extractor_unplayable_reportsError() {
    PlayerResponse r;
    r.status = PlayerResponse::PlayabilityStatus::Unplayable;
    r.errorMessage = QStringLiteral("login required");
    QString err;
    QVERIFY(YouTubeExtractor::pickBestAudio(r, nullptr, &err).isEmpty());
    QVERIFY(err.contains(QStringLiteral("login required")));
}

void TestYouTubeCore::extractor_buildUrl_appendsSig() {
    PlayerFormat f;
    f.url = QStringLiteral("https://x/stream?itag=140&ratebypass=yes");
    f.signatureParam = QString();
    const auto u = YouTubeExtractor::buildUrl(f, QStringLiteral("ABCDEF"));
    QVERIFY(u.contains(QStringLiteral("sig=ABCDEF")));
    QVERIFY(u.contains(QStringLiteral("itag=140")));
}

void TestYouTubeCore::extractor_buildUrl_usesCustomSigParam() {
    PlayerFormat f;
    f.url = QStringLiteral("https://x/stream?itag=140");
    f.signatureParam = QStringLiteral("signature");
    const auto u = YouTubeExtractor::buildUrl(f, QStringLiteral("ABCDEF"));
    QVERIFY(u.contains(QStringLiteral("signature=ABCDEF")));
    QVERIFY(!u.contains(QStringLiteral("sig=ABCDEF")));
}

// ── YouTubeExtractor::bestThumbnailUrl ──────────────────────────────

void TestYouTubeCore::extractor_bestThumb_returnsFirstForEmptyArray() {
    QJsonArray empty;
    QVERIFY(YouTubeExtractor::bestThumbnailUrl(empty).isEmpty());
}

void TestYouTubeCore::extractor_bestThumb_picksExactMatch() {
    QJsonArray thumbs;
    QJsonObject t1;
    t1["url"] = QStringLiteral("https://i.ytimg.com/vi/abc/96.jpg");
    t1["width"] = 96;
    t1["height"] = 96;
    thumbs.append(t1);
    QJsonObject t2;
    t2["url"] = QStringLiteral("https://i.ytimg.com/vi/abc/320.jpg");
    t2["width"] = 320;
    t2["height"] = 320;
    thumbs.append(t2);

    // Requesting Small (96) → picks the 96px entry.
    const auto url = YouTubeExtractor::bestThumbnailUrl(
        thumbs, mf::core::sources::youtube::ThumbnailSize::Small);
    QCOMPARE(url, QStringLiteral("https://i.ytimg.com/vi/abc/96.jpg"));
}

void TestYouTubeCore::extractor_bestThumb_picksClosestOver() {
    QJsonArray thumbs;
    QJsonObject t1;
    t1["url"] = QStringLiteral("https://i.ytimg.com/vi/abc/96.jpg");
    t1["width"] = 96;
    thumbs.append(t1);
    QJsonObject t2;
    t2["url"] = QStringLiteral("https://i.ytimg.com/vi/abc/320.jpg");
    t2["width"] = 320;
    thumbs.append(t2);
    QJsonObject t3;
    t3["url"] = QStringLiteral("https://i.ytimg.com/vi/abc/480.jpg");
    t3["width"] = 480;
    thumbs.append(t3);

    // Requesting Medium (320) → picks the 320px entry.
    const auto url = YouTubeExtractor::bestThumbnailUrl(
        thumbs, mf::core::sources::youtube::ThumbnailSize::Medium);
    QCOMPARE(url, QStringLiteral("https://i.ytimg.com/vi/abc/320.jpg"));
}

void TestYouTubeCore::extractor_bestThumb_fallsBackToLargestUnder() {
    QJsonArray thumbs;
    QJsonObject t1;
    t1["url"] = QStringLiteral("https://i.ytimg.com/vi/abc/96.jpg");
    t1["width"] = 96;
    thumbs.append(t1);
    QJsonObject t2;
    t2["url"] = QStringLiteral("https://i.ytimg.com/vi/abc/120.jpg");
    t2["width"] = 120;
    thumbs.append(t2);

    // Requesting Medium (320) → no entry >= 320, picks the largest (120).
    const auto url = YouTubeExtractor::bestThumbnailUrl(
        thumbs, mf::core::sources::youtube::ThumbnailSize::Medium);
    QCOMPARE(url, QStringLiteral("https://i.ytimg.com/vi/abc/120.jpg"));
}

void TestYouTubeCore::extractor_bestThumb_usesFirstAsLastResort() {
    QJsonArray thumbs;
    QJsonObject t1;
    t1["url"] = QStringLiteral("https://i.ytimg.com/vi/abc/default.jpg");
    thumbs.append(t1);

    const auto url = YouTubeExtractor::bestThumbnailUrl(
        thumbs, mf::core::sources::youtube::ThumbnailSize::Full);
    QCOMPARE(url, QStringLiteral("https://i.ytimg.com/vi/abc/default.jpg"));
}

QTEST_GUILESS_MAIN(TestYouTubeCore)
#include "testyoutubecore.moc"

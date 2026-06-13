// YouTubeProvider.cpp
// InnerTube-based YouTube Music source. See YouTubeProvider.h for the
// full implementation outline. This file wires up the network-facing
// layer (InnerTubeClient, JS cipher extractor) onto the deterministic
// primitives that already live under youtube/.
//
// yt-dlp subprocess fallback (Block 5.2.C) plugs in at the failure
// boundary of each public method.

#include "YouTubeProvider.h"

#include "../services/ImageCache.h"
#include "youtube/Cipher.h"
#include "youtube/JsCipherExtractor.h"
#include "youtube/PlayerResponse.h"
#include "youtube/YouTubeExtractor.h"
#include "youtube/YtDlpProcess.h"

#include <QDateTime>
#include <QJsonArray>
#include <QJsonDocument>
#include <QJsonObject>
#include <QJsonValue>
#include <QStringList>
#include <QUrl>
#include <QUrlQuery>
#include <QRandomGenerator>

#include <functional>
#include <utility>

namespace mf::core::sources {

using mf::core::interfaces::IMusicSourceSession;
using mf::core::models::MusicFile;
using mf::core::models::SourceConfigField;
using mf::core::models::StreamingSource;
using mf::core::services::ImageCache;
using mf::core::sources::youtube::YtDlpProcess;
    // Forward declarations of helpers (defined in namespace re-open below)
    QList<MusicFile> parseMusicCardShelf(const QJsonObject& obj);
    QList<MusicFile> parseMusicCard(const QJsonObject& obj);
    QList<MusicFile> parseSearchSectionList(const QJsonObject& sectionList, int limit);
    QString          textAt(const QJsonArray& runs);
    QString          joinRuns(const QJsonArray& runs);
    QString          firstThumbUrl(const QJsonObject& obj);
    QString          joinErrorReason(const QJsonObject& obj);

namespace {

constexpr qint64 kSearchTtlMs     = 30LL * 60LL * 1000LL;
constexpr qint64 kCipherTtlMs     = 60LL * 60LL * 1000LL;
constexpr qint64 kCipherFailTtlMs =  5LL * 60LL * 1000LL;
constexpr int    kFallbackLimit   = 25;

// Generate a random 11-character visitor data string. This is used
// when the bot detection mitigator triggers a rotation.
QString generateVisitorData() {
    static const char kChars[] =
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789_-";
    QString result;
    result.reserve(11);
    auto* rng = QRandomGenerator::global();
    for (int i = 0; i < 11; ++i) {
        result.append(QLatin1Char(kChars[rng->bounded(int(sizeof(kChars) - 1))]));
    }
    return result;
}


} // namespace

// ── YouTubeSession ────────────────────────────────────────────────────

YouTubeSession::YouTubeSession(QString sourceId,
                               HttpClient* http,
                               std::shared_ptr<youtube::StreamCache> streamCache,
                               ImageCache* imageCache,
                               youtube::YtDlpProcess* ytDlp,
                               bool useYtDlpFallback)
    : sourceId_(std::move(sourceId))
    , http_(http)
    , streamCache_(streamCache ? std::move(streamCache)
                               : std::make_shared<youtube::StreamCache>())
    , imageCache_(imageCache)
    , client_(http, QStringLiteral("AIzaSyAO_FJ2SlqU8Q4STEHLGCilw_Y9_11qcW8"))
    , ytDlp_(ytDlp)
    , useYtDlpFallback_(useYtDlpFallback)
{
    client_.setRegionLanguage(region_, language_);
}

YouTubeSession::~YouTubeSession() = default;

QString YouTubeSession::normalizeVideoId(const QString& urlOrId) {
    youtube::ParsedUrl pu = youtube::YouTubeUrlParser::parse(urlOrId);
    return pu.videoId;
}

youtube::ParsedUrl YouTubeSession::parseUrl(const QString& url) {
    return youtube::YouTubeUrlParser::parse(url);
}

QString YouTubeSession::cachedCipherOps(const QString& playerJsUrl) const {
    if (playerJsUrl.isEmpty()) return QString();
    const qint64 now = QDateTime::currentMSecsSinceEpoch();
    auto it = cipherCache_.constFind(playerJsUrl);
    if (it != cipherCache_.constEnd() && it->second > now) {
        return it->first;
    }
    return QString();
}

void YouTubeSession::doSearchInnerTube(const QString& query,
                                       int limit,
                                       std::function<void(QList<MusicFile>, QString)> cb) {
    const QString key = QStringLiteral("%1|%2").arg(query, QString::number(limit));
    const qint64 now = QDateTime::currentMSecsSinceEpoch();
    auto it = searchCache_.constFind(key);
    if (it != searchCache_.constEnd() && it->expiresAtMs > now) {
        if (cb) cb(it->tracks, QString());
        return;
    }

    // InnerTube search uses a `params` token to select the
    // result section. EgWKAQ = songs. Other tokens exist
    // (albums, artists, …) and can be added later.
    constexpr const char* kSongsParams = "EgWKAQ";
    client_.search(query, QString::fromLatin1(kSongsParams),
        [this, key, query, limit, cb](QJsonObject obj, QString err) mutable {
            if (!err.isEmpty()) {
                if (cb) cb({}, err);
                return;
            }
            const QJsonObject sectionListRenderer =
                obj.value(QStringLiteral("contents"))
                   .toObject()
                   .value(QStringLiteral("tabbedSearchResultsRenderer"))
                   .toObject()
                   .value(QStringLiteral("tabs"))
                   .toArray()
                   .first()
                   .toObject()
                   .value(QStringLiteral("tabRenderer"))
                   .toObject()
                   .value(QStringLiteral("content"))
                   .toObject()
                   .value(QStringLiteral("sectionListRenderer"))
                   .toObject();

            QList<MusicFile> tracks = parseSearchSectionList(sectionListRenderer, limit);
            if (tracks.isEmpty()) {
                // Some queries land in a musicCardShelfRenderer instead.
                tracks = parseMusicCardShelf(sectionListRenderer);
            }
            if (!tracks.isEmpty()) {
                SearchCacheEntry e;
                e.tracks      = tracks;
                e.expiresAtMs = QDateTime::currentMSecsSinceEpoch() + kSearchTtlMs;
                searchCache_.insert(key, e);
                if (cb) cb(tracks, QString());
                return;
            }
            // InnerTube returned 0 results. Last-resort: yt-dlp
            // dump-json search. Only attempted if the session has the
            // fallback enabled and a wrapper is bound.
            if (!useYtDlpFallback_ || !ytDlp_) {
                if (cb) cb({}, QString());
                return;
            }
            ytDlp_->search(query, limit,
                [this, key, cb](QList<YtDlpProcess::SearchEntry> entries,
                                QString ytErr) mutable {
                    if (!ytErr.isEmpty()) {
                        if (cb) cb({}, ytErr);
                        return;
                    }
                    QList<MusicFile> out;
                    out.reserve(entries.size());
                    for (const auto& e : entries) {
                        if (e.id.isEmpty()) continue;
                        MusicFile mf;
                        mf.setSourceType(QStringLiteral("youtube"));
                        mf.setYouTubeVideoId(e.id);
                        mf.setId(e.id);
                        if (!e.title.isEmpty())    mf.setTitle(e.title);
                        if (!e.uploader.isEmpty()) mf.setArtist(e.uploader);
                        if (e.durationSeconds > 0) {
                            mf.setDuration(std::chrono::seconds(e.durationSeconds));
                        }
                        out.append(std::move(mf));
                    }
                    SearchCacheEntry ce;
                    ce.tracks      = out;
                    ce.expiresAtMs = QDateTime::currentMSecsSinceEpoch() + kSearchTtlMs;
                    searchCache_.insert(key, ce);
                    if (cb) cb(out, QString());
                });
        });
}

void YouTubeSession::doPlayerInnerTube(const QString& videoId,
                                       std::function<void(youtube::PlayerResponse, QString)> cb) {
    client_.player(videoId,
        [cb = std::move(cb)](QJsonObject obj, QString err) {
            if (!err.isEmpty()) {
                if (cb) cb({}, err);
                return;
            }
            auto pr = youtube::PlayerResponseParser::parse(obj);
            if (cb) cb(pr, QString());
        });
}

void YouTubeSession::doBrowseInnerTube(const QString& browseId,
                                       std::function<void(QJsonObject, QString)> cb) {
    client_.browse(browseId, std::move(cb));
}

void YouTubeSession::searchTracks(QString query, int limit,
                                   ResultCallback onDone,
                                   StringCallback onError) {
    doSearchInnerTube(query, limit > 0 ? limit : kFallbackLimit,
        [onDone, onError](QList<MusicFile> tracks, QString err) mutable {
            if (!err.isEmpty()) {
                if (onError) onError(err);
                return;
            }
            if (onDone) onDone(tracks);
        });
}

void YouTubeSession::ensureCipherOps(const QString& playerJsUrl,
                                     std::function<void(QString)> cb) {
    if (playerJsUrl.isEmpty()) {
        if (cb) cb(QString());
        return;
    }
    const QString cached = cachedCipherOps(playerJsUrl);
    if (!cached.isEmpty()) {
        if (cb) cb(cached);
        return;
    }
    const qint64 now = QDateTime::currentMSecsSinceEpoch();
    auto failIt = cipherFailureCache_.constFind(playerJsUrl);
    if (failIt != cipherFailureCache_.constEnd() && failIt.value() > now) {
        if (cb) cb(QString());
        return;
    }

    mf::core::sources::HttpRequest req;
    req.url    = QString(playerJsUrl);
    req.method = QByteArrayLiteral("GET");
    req.timeoutMs = 15'000;
    req.headers.insert(QStringLiteral("User-Agent"),
                       QStringLiteral("Mozilla/5.0 (compatible; Musicefy)"));
    http_->get(req,
        [this, playerJsUrl, cb](mf::core::sources::HttpResponse resp) mutable {
            if (!resp.ok()) {
                cipherFailureCache_.insert(playerJsUrl,
                    QDateTime::currentMSecsSinceEpoch() + kCipherFailTtlMs);
                if (cb) cb(QString());
                return;
            }
            const QString body = QString::fromUtf8(resp.body);
            const QString ops  = youtube::JsCipherExtractor::extractFromJs(body);
            if (ops.isEmpty()) {
                cipherFailureCache_.insert(playerJsUrl,
                    QDateTime::currentMSecsSinceEpoch() + kCipherFailTtlMs);
                if (cb) cb(QString());
                return;
            }
            cipherCache_.insert(playerJsUrl,
                qMakePair(ops, QDateTime::currentMSecsSinceEpoch() + kCipherTtlMs));
            if (cb) cb(ops);
        });
}

void YouTubeSession::fetchStreamUrlInnerTube(const QString& videoId,
                                             UrlResultCb cb) {
    doPlayerInnerTube(videoId,
        [this, videoId, cb = std::move(cb)]
        (youtube::PlayerResponse pr, QString err) mutable {
            if (!err.isEmpty()) {
                if (cb) cb(QString(), err);
                return;
            }
            if (!pr.isPlayable()) {
                if (cb) cb(QString(), QStringLiteral("Unplayable: %1")
                    .arg(pr.errorMessage.isEmpty()
                             ? QStringLiteral("unknown")
                             : pr.errorMessage));
                return;
            }

            bool needsCipher = false;
            for (const auto& f : pr.formats) {
                if (f.isCiphered()) { needsCipher = true; break; }
            }

            // Identity-decipher: returns the raw signature as-is. Used
            // when we don't have (or couldn't fetch) the JS ops table.
            // The player will 4xx; the yt-dlp fallback catches it.
            auto identityDecipher = [](const QString& sig) { return sig; };

            auto extractAndFinish = [cb = cb](
                const youtube::PlayerResponse& prx,
                youtube::YouTubeExtractor::DecipherFn decipher) mutable {
                QString extractErr;
                const QString url = youtube::YouTubeExtractor::pickBestAudio(
                    prx, decipher, &extractErr);
                if (url.isEmpty()) {
                    if (cb) cb(QString(), extractErr);
                    return;
                }
                if (cb) cb(url, QString());
            };

            if (!needsCipher) {
                extractAndFinish(pr, identityDecipher);
                return;
            }

            if (!pr.hasPlayerJsUrl()) {
                extractAndFinish(pr, identityDecipher);
                return;
            }

            const QString jsUrl = pr.playerJsUrl;
            ensureCipherOps(jsUrl,
                [this, pr, jsUrl, extractAndFinish, identityDecipher, cb]
                (QString ops) mutable {
                    if (ops.isEmpty()) {
                        extractAndFinish(pr, identityDecipher);
                        return;
                    }
                    Q_UNUSED(jsUrl);
                    auto decipher = [ops](const QString& sig) {
                        return youtube::Cipher::decipher(sig, ops);
                    };
                    extractAndFinish(pr, decipher);
                });
        });
}

void YouTubeSession::fetchStreamUrl(QString trackId,
                                    StringCallback onDone,
                                    StringCallback onError) {
    const QString videoId = normalizeVideoId(trackId);
    if (videoId.isEmpty()) {
        if (onError) onError(QStringLiteral("Invalid video id: %1").arg(trackId));
        return;
    }

    // Check if bot detection needs visitor-data rotation.
    if (botMitigator_.shouldRotateVisitorData()) {
        const QString newVd = generateVisitorData();
        client_.setVisitorData(newVd);
        botMitigator_.reset();
    }

    if (streamCache_) {
        const QString cached = streamCache_->tryGet(videoId);
        if (!cached.isEmpty()) {
            botMitigator_.notifyPlaybackSuccess();
            if (onDone) onDone(cached);
            return;
        }
    }

    fetchStreamUrlInnerTube(videoId,
        [this, videoId, onDone, onError](QString url, QString err) mutable {
            if (!url.isEmpty()) {
                botMitigator_.notifyPlaybackSuccess();
                if (onDone) onDone(url);
                return;
            }
            // InnerTube chain failed. Notify bot detection.
            botMitigator_.notifyPlaybackFailure();

            // Surface the error directly if yt-dlp fallback is
            // disabled or no wrapper is bound.
            if (!useYtDlpFallback_ || !ytDlp_) {
                if (onError) onError(err);
                return;
            }
            ytDlp_->resolveStreamUrl(videoId,
                [this, videoId, onDone, onError, err]
                (QString u, QString e) mutable {
                    if (u.isEmpty()) {
                        if (onError) onError(e.isEmpty() ? err : e);
                        return;
                    }
                    botMitigator_.notifyPlaybackSuccess();
                    if (streamCache_) {
                        streamCache_->put(videoId, u, QString(), 0, 21'540);
                    }
                    if (onDone) onDone(u);
                });
        });
}

void YouTubeSession::fetchLyrics(QString trackId,
                                 StringCallback onDone,
                                 StringCallback onError) {
    const QString videoId = normalizeVideoId(trackId);
    if (videoId.isEmpty()) {
        if (onError) onError(QStringLiteral("Invalid video id: %1").arg(trackId));
        return;
    }
    constexpr const char* kLyricsBrowse = "MPLYt";
    doBrowseInnerTube(QString::fromLatin1(kLyricsBrowse) + QStringLiteral("|") + videoId,
        [onDone, onError](QJsonObject obj, QString err) {
            if (!err.isEmpty()) {
                if (onError) onError(err);
                return;
            }
            const QJsonArray runs =
                obj.value(QStringLiteral("contents"))
                   .toObject()
                   .value(QStringLiteral("sectionListRenderer"))
                   .toObject()
                   .value(QStringLiteral("contents"))
                   .toArray()
                   .first()
                   .toObject()
                   .value(QStringLiteral("musicDescriptionShelfRenderer"))
                   .toObject()
                   .value(QStringLiteral("description"))
                   .toObject()
                   .value(QStringLiteral("runs"))
                   .toArray();
            const QString lyrics = joinRuns(runs);
            if (lyrics.isEmpty()) {
                if (onError) onError(QStringLiteral("No lyrics found"));
                return;
            }
            if (onDone) onDone(lyrics);
        });
}

void YouTubeSession::fetchCover(QString trackId,
                                BytesCallback onDone,
                                StringCallback onError) {
    const QString videoId = normalizeVideoId(trackId);
    if (videoId.isEmpty()) {
        if (onError) onError(QStringLiteral("Invalid video id: %1").arg(trackId));
        return;
    }

    // The YouTube thumbnail URL scheme is fixed:
    //   https://i.ytimg.com/vi/<videoId>/<quality>.jpg
    // The qualities are tried in descending resolution order; the
    // first one the server returns (any 2xx) is used.
    static const QStringList kQualities = {
        QStringLiteral("maxresdefault"),
        QStringLiteral("sddefault"),
        QStringLiteral("hqdefault"),
        QStringLiteral("mqdefault"),
        QStringLiteral("default"),
    };

    std::function<void(int)> tryNext;
    tryNext = [this, videoId, onDone, onError, &tryNext](int idx) {
        if (idx >= kQualities.size()) {
            if (onError) onError(QStringLiteral("No cover thumbnail available"));
            return;
        }
        const QString quality = kQualities[idx];
        const QUrl url(QStringLiteral("https://i.ytimg.com/vi/%1/%2.jpg")
                           .arg(videoId, quality));

        if (imageCache_) {
            imageCache_->get(url,
                [onDone, onError, &tryNext, idx]
                (QByteArray bytes, QString, QString err) {
                    if (!err.isEmpty() || bytes.isEmpty()) {
                        // Cache miss / network error → try next quality.
                        tryNext(idx + 1);
                        return;
                    }
                    if (onDone) onDone(bytes);
                });
        } else {
            // No cache — go straight to HttpClient.
            mf::core::sources::HttpRequest req;
            req.url = url.toString().toUtf8();
            req.method = QByteArrayLiteral("GET");
            req.timeoutMs = 15'000;
            http_->get(req,
                [onDone, onError, &tryNext, idx]
                (mf::core::sources::HttpResponse resp) {
                    if (!resp.ok() || resp.body.isEmpty()) {
                        tryNext(idx + 1);
                        return;
                    }
                    if (onDone) onDone(resp.body);
                });
        }
    };

    tryNext(0);
}

void YouTubeSession::ping(BoolCallback onDone) {
    client_.search(QStringLiteral("test"),
                   QStringLiteral("EgWKAQ"),
        [onDone](QJsonObject, QString err) {
            // Any HTTP-2xx response (even empty) is "healthy". Only
            // the final-client-failed error is unhealthy.
            if (onDone) onDone(err.isEmpty(), err);
        });
}

// ── YouTubeProvider ───────────────────────────────────────────────────

YouTubeProvider::YouTubeProvider()
    : http_(new HttpClient())
    , ownsHttp_(true)
    , streamCache_(std::make_shared<youtube::StreamCache>())
    , ytDlp_(new youtube::YtDlpProcess())
    , ownsYtDlp_(true)
{
}

YouTubeProvider::YouTubeProvider(HttpClient* sharedHttp)
    : http_(sharedHttp)
    , ownsHttp_(false)
    , streamCache_(std::make_shared<youtube::StreamCache>())
    , ytDlp_(new youtube::YtDlpProcess())
    , ownsYtDlp_(true)
{
}

YouTubeProvider::~YouTubeProvider() {
    if (ownsHttp_) {
        delete http_;
    }
    if (ownsYtDlp_) {
        delete ytDlp_;
    }
}

QList<SourceConfigField> YouTubeProvider::configFields() const {
    auto make = [](const QString& key, const QString& label,
                   const QString& placeholder, const QString& def,
                   const QString& fieldType, bool required) {
        SourceConfigField f;
        f.setKey(key);
        f.setLabel(label);
        f.setPlaceholder(placeholder);
        f.setDefaultValue(def);
        f.setFieldType(fieldType);
        f.setIsRequired(required);
        return f;
    };
    return {
        make(QStringLiteral("region"),
             QStringLiteral("Region code"),
             QStringLiteral("US"),
             QStringLiteral("US"),
             QStringLiteral("text"), false),
        make(QStringLiteral("language"),
             QStringLiteral("Language code"),
             QStringLiteral("en"),
             QStringLiteral("en"),
             QStringLiteral("text"), false),
        make(QStringLiteral("ytDlpEnabled"),
             QStringLiteral("Use yt-dlp fallback"),
             QStringLiteral("true"),
             QStringLiteral("true"),
             QStringLiteral("checkbox"), false),
    };
}

std::unique_ptr<IMusicSourceSession> YouTubeProvider::createSession(
    const StreamingSource& source) const {
    return std::make_unique<YouTubeSession>(source.id(), http_, streamCache_,
                                            imageCache_, ytDlp_,
                                            useYtDlpFallback_);
}

} // namespace mf::core::sources

// ── Local helpers ─────────────────────────────────────────────────────

namespace mf::core::sources {

QString textAt(const QJsonArray& runs) {
    return joinRuns(runs);
}

QString joinRuns(const QJsonArray& runs) {
    QString out;
    for (const QJsonValue& v : runs) {
        if (!v.isObject()) continue;
        const QJsonObject r = v.toObject();
        const QString t = r.value(QStringLiteral("text")).toString();
        if (!t.isEmpty()) out += t;
    }
    return out;
}

QString firstThumbUrl(const QJsonObject& obj) {
    const QJsonArray thumbs = obj.value(QStringLiteral("thumbnails")).toArray();
    if (thumbs.isEmpty()) return QString();
    const QJsonObject t0 = thumbs.first().toObject();
    return t0.value(QStringLiteral("url")).toString();
}

QString joinErrorReason(const QJsonObject& obj) {
    return obj.value(QStringLiteral("reason")).toString();
}

QList<MusicFile> parseMusicCard(const QJsonObject& obj) {
    MusicFile mf;
    mf.setSourceType(QStringLiteral("youtube"));
    mf.setYouTubeVideoId(obj.value(QStringLiteral("videoId")).toString());
    mf.setId(mf.youTubeVideoId());
    mf.setTitle(joinRuns(obj.value(QStringLiteral("title")).toObject()
                             .value(QStringLiteral("runs")).toArray()));
    if (mf.title().isEmpty()) {
        mf.setTitle(obj.value(QStringLiteral("title")).toObject()
                        .value(QStringLiteral("simpleText")).toString());
    }
    const QJsonArray bylineRuns = obj.value(QStringLiteral("byline")).toObject()
                                      .value(QStringLiteral("runs")).toArray();
    if (!bylineRuns.isEmpty()) {
        mf.setArtist(joinRuns(bylineRuns));
    }
    const QString thumb = firstThumbUrl(obj.value(QStringLiteral("thumbnail"))
                                            .toObject());
    mf.setCoverUrl(thumb);
    const QString dur = obj.value(QStringLiteral("lengthText")).toObject()
                            .value(QStringLiteral("simpleText")).toString();
    if (!dur.isEmpty()) {
        QStringList parts = dur.split(QLatin1Char(':'));
        int total = 0;
        for (int i = 0; i < parts.size(); ++i) {
            total = total * 60 + parts[i].toInt();
        }
        mf.setDuration(std::chrono::seconds(total));
    }
    return {mf};
}

QList<MusicFile> parseMusicCardShelf(const QJsonObject& obj) {
    QList<MusicFile> out;
    const QJsonArray contents = obj.value(QStringLiteral("contents")).toArray();
    for (const QJsonValue& v : contents) {
        if (!v.isObject()) continue;
        const QJsonObject mr = v.toObject()
                                  .value(QStringLiteral("musicCardRenderer")).toObject();
        if (mr.isEmpty()) {
            const QJsonObject mcr = v.toObject()
                                      .value(QStringLiteral("musicCardShelfRenderer")).toObject();
            if (!mcr.isEmpty()) {
                out.append(parseMusicCard(mcr));
            }
            continue;
        }
        out.append(parseMusicCard(mr));
    }
    return out;
}

QList<MusicFile> parseSearchSectionList(const QJsonObject& sectionList, int limit) {
    QList<MusicFile> out;
    const QJsonArray contents = sectionList.value(QStringLiteral("contents")).toArray();
    for (const QJsonValue& v : contents) {
        if (!v.isObject()) continue;
        const QJsonObject mshr = v.toObject()
                                    .value(QStringLiteral("musicShelfRenderer")).toObject();
        if (mshr.isEmpty()) continue;
        const QJsonArray items = mshr.value(QStringLiteral("contents")).toArray();
        for (const QJsonValue& iv : items) {
            if (!iv.isObject()) continue;
            const QJsonObject mr = iv.toObject()
                                      .value(QStringLiteral("musicResponsiveListItemRenderer")).toObject();
            if (mr.isEmpty()) continue;
            MusicFile mf;
            mf.setSourceType(QStringLiteral("youtube"));
            const QJsonObject ovr = mr.value(QStringLiteral("overlay")).toObject()
                                       .value(QStringLiteral("musicItemThumbnailOverlayRenderer"))
                                       .toObject()
                                       .value(QStringLiteral("content"))
                                       .toObject()
                                       .value(QStringLiteral("musicPlayButtonRenderer"))
                                       .toObject()
                                       .value(QStringLiteral("playNavigationEndpoint"))
                                       .toObject()
                                       .value(QStringLiteral("watchEndpoint"))
                                       .toObject();
            mf.setYouTubeVideoId(ovr.value(QStringLiteral("videoId")).toString());
            mf.setId(mf.youTubeVideoId());
            const QJsonArray flex = mr.value(QStringLiteral("flexColumns")).toArray();
            if (flex.size() >= 1) {
                const QString t = joinRuns(flex.at(0).toObject()
                                              .value(QStringLiteral("musicResponsiveListItemFlexColumnRenderer"))
                                              .toObject()
                                              .value(QStringLiteral("text")).toObject()
                                              .value(QStringLiteral("runs")).toArray());
                mf.setTitle(t);
            }
            if (flex.size() >= 2) {
                const QString a = joinRuns(flex.at(1).toObject()
                                              .value(QStringLiteral("musicResponsiveListItemFlexColumnRenderer"))
                                              .toObject()
                                              .value(QStringLiteral("text")).toObject()
                                              .value(QStringLiteral("runs")).toArray());
                mf.setArtist(a);
            }
            const QJsonArray thumbs = mr.value(QStringLiteral("thumbnail"))
                                          .toObject()
                                          .value(QStringLiteral("musicThumbnailRenderer"))
                                          .toObject()
                                          .value(QStringLiteral("thumbnail"))
                                          .toObject()
                                          .value(QStringLiteral("thumbnails")).toArray();
            if (!thumbs.isEmpty()) {
                mf.setCoverUrl(thumbs.first().toObject()
                                  .value(QStringLiteral("url")).toString());
            }
            if (!mf.youTubeVideoId().isEmpty()) {
                out.append(mf);
            }
            if (out.size() >= limit) return out;
        }
    }
    return out;
}

} // namespace mf::core::sources
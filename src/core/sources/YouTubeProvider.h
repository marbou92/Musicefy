// YouTubeProvider.h
// InnerTube-based YouTube Music source. Multi-client fallback, bot-detection
// mitigation (visitor data, PoToken), signature-cipher decryption, and
// stream URL extraction are all required for a working implementation.
//
// The deterministic primitives (URL parsing, cipher math, stream cache,
// player response DTO, extractor) live under sources/youtube/. The
// network-facing layer (InnerTubeClient, JS cipher extractor) is wired
// up here.
//
// yt-dlp subprocess fallback (Block 5.2.C) plugs in at the failure
// boundary of fetchStreamUrl / searchTracks / fetchCover / fetchLyrics.
// The pipeline is shaped so the fallback can be a one-line swap.

#pragma once

#include "../interfaces/IMusicSourceProvider.h"
#include "../interfaces/IMusicSourceSession.h"
#include "../models/MusicFile.h"
#include "../models/SourceConfigField.h"
#include "../models/StreamingSource.h"
#include "youtube/InnerTubeClient.h"
#include "youtube/StreamCache.h"
#include "youtube/BotDetectionMitigator.h"
#include "youtube/YtDlpProcess.h"
#include "youtube/YouTubeUrlParser.h"
#include "youtube/PlayerResponse.h"

#include <QHash>
#include <QJsonObject>
#include <QList>
#include <QString>
#include <QStringList>

#include <functional>
#include <memory>

#include "HttpClient.h"

namespace mf::core::services { class ImageCache; }

namespace mf::core::sources {

class YouTubeSession : public QObject, public mf::core::interfaces::IMusicSourceSession {
    Q_OBJECT
public:
    explicit YouTubeSession(QString sourceId,
                            HttpClient* http,
                            std::shared_ptr<youtube::StreamCache> streamCache,
                            mf::core::services::ImageCache* imageCache = nullptr,
                            youtube::YtDlpProcess* ytDlp = nullptr,
                            bool useYtDlpFallback = true);
    ~YouTubeSession() override;

    QString sourceType() const override { return QStringLiteral("youtube"); }
    QString sourceId()   const override { return sourceId_; }
    bool    isHealthy()  const override { return healthy_; }

    void searchTracks(QString query, int limit,
                      ResultCallback onDone,
                      StringCallback onError) override;
    void fetchStreamUrl(QString trackId, StringCallback onDone, StringCallback onError) override;
    void fetchLyrics(QString trackId, StringCallback onDone, StringCallback onError) override;
    void fetchCover(QString trackId, BytesCallback onDone, StringCallback onError) override;
    void ping(BoolCallback onDone) override;

    // Public utility: extract a video id from a URL or pass through a
    // bare 11-char id. Returns an empty string on failure.
    static QString normalizeVideoId(const QString& urlOrId);

    // Public utility: parse a URL into its typed form. Used by callers
    // that want to know whether an input is a video / playlist / artist
    // / album.
    static youtube::ParsedUrl parseUrl(const QString& url);

    // The InnerTubeClient is per-session (visitorData / poToken are
    // session-scoped). Exposed for tests.
    youtube::InnerTubeClient& innerTube() { return client_; }

    // Bot detection mitigator. Tracks consecutive failures and
    // triggers visitor-data rotation. Exposed for tests.
    youtube::BotDetectionMitigator& botMitigator() { return botMitigator_; }

    // The YtDlpProcess wrapper. May be null (tests disable the
    // fallback by passing a null pointer). Exposed for tests.
    youtube::YtDlpProcess* ytDlp() const { return ytDlp_; }
    bool useYtDlpFallback() const { return useYtDlpFallback_; }

    // Test seam: enable/disable the yt-dlp fallback mid-session.
    void setUseYtDlpFallback(bool enabled) { useYtDlpFallback_ = enabled; }

signals:
    void healthChanged(bool isHealthy);

private:
    struct SearchCacheEntry {
        QList<mf::core::models::MusicFile> tracks;
        qint64                              expiresAtMs = 0;
    };

    // InnerTube call paths used by the public methods.
    void doSearchInnerTube(const QString& query, int limit,
                           std::function<void(QList<mf::core::models::MusicFile>, QString)> cb);
    void doPlayerInnerTube(const QString& videoId,
                           std::function<void(youtube::PlayerResponse, QString)> cb);
    void doBrowseInnerTube(const QString& browseId,
                           std::function<void(QJsonObject, QString)> cb);

    // Run the InnerTube + cipher chain for fetchStreamUrl; surface
    // the result as a single (url, error) pair so the caller can
    // layer yt-dlp on top.
    using UrlResultCb = std::function<void(QString /*url*/, QString /*err*/)>;
    void fetchStreamUrlInnerTube(const QString& videoId, UrlResultCb cb);

    // Look up a previously-cached cipher ops table. Returns empty
    // string on miss. If a JS URL is provided and the table is not
    // cached, fetches the player JS body, runs JsCipherExtractor, and
    // invokes `cb` with the resolved ops string (or empty on
    // failure). On failure the URL is added to a short-lived negative
    // cache so we don't hammer the same dead URL on every track.
    void ensureCipherOps(const QString& playerJsUrl,
                         std::function<void(QString)> cb);

    // Direct cache lookup (no fetch). Returns empty on miss. Tests
    // use this to assert that the cache layer is being hit.
    QString cachedCipherOps(const QString& playerJsUrl) const;

    QString     sourceId_;
    HttpClient* http_;
    std::shared_ptr<youtube::StreamCache>            streamCache_;
    mf::core::services::ImageCache*                  imageCache_ = nullptr;
    youtube::InnerTubeClient                         client_;
    youtube::YtDlpProcess*                           ytDlp_ = nullptr;
    bool                                             useYtDlpFallback_ = true;
    bool                                             healthy_ = false;

    // Per-session caches.
    QHash<QString, SearchCacheEntry> searchCache_;
    QHash<QString, QPair<QString, qint64>> cipherCache_;
    QHash<QString, qint64>              cipherFailureCache_;

    // InnerTube client state (overrides the InnerTubeClient defaults).
    QString      visitorData_;
    QString      poToken_;
    QString      region_   = QStringLiteral("US");
    QString      language_ = QStringLiteral("en");

    // Bot detection mitigator.
    youtube::BotDetectionMitigator botMitigator_;
};

class YouTubeProvider : public QObject, public mf::core::interfaces::IMusicSourceProvider {
    Q_OBJECT
public:
    YouTubeProvider();
    explicit YouTubeProvider(HttpClient* sharedHttp);
    ~YouTubeProvider() override;

    QString sourceType() const override { return QStringLiteral("youtube"); }
    QString displayName() const override { return QStringLiteral("YouTube Music"); }
    QList<mf::core::models::SourceConfigField> configFields() const override;

    std::unique_ptr<mf::core::interfaces::IMusicSourceSession> createSession(
        const mf::core::models::StreamingSource& source) const override;

    // Per-process stream cache. All sessions created by this provider
    // share the same cache so a stream URL resolved in one session
    // is available in another (typical when the user replays a track
    // they fetched via search 5 minutes ago).
    std::shared_ptr<youtube::StreamCache> streamCache() const { return streamCache_; }

    // Cover-art cache. Owned by the application; the provider does
    // not delete it. Null means cover URLs are downloaded directly
    // (slower, no caching across sessions).
    void                    setImageCache(mf::core::services::ImageCache* cache) { imageCache_ = cache; }
    mf::core::services::ImageCache* imageCache() const { return imageCache_; }

    // yt-dlp fallback gate. When false, sessions will NOT spawn
    // yt-dlp even if available. Defaults to true.
    void setUseYtDlpFallback(bool enabled) { useYtDlpFallback_ = enabled; }
    bool useYtDlpFallback() const { return useYtDlpFallback_; }

    // Replace the yt-dlp wrapper (test seam — production code leaves
    // the default wrapper in place). When the test injects its own
    // pointer, the provider does NOT take ownership; the test must
    // delete it.
    void setYtDlpProcess(youtube::YtDlpProcess* p, bool takeOwnership = false) {
        if (ownsYtDlp_ && ytDlp_) delete ytDlp_;
        ytDlp_      = p;
        ownsYtDlp_  = takeOwnership;
    }
    youtube::YtDlpProcess* ytDlpProcess() const { return ytDlp_; }

private:
    HttpClient*                          http_ = nullptr;
    bool                                 ownsHttp_ = false;
    std::shared_ptr<youtube::StreamCache> streamCache_;
    mf::core::services::ImageCache*       imageCache_ = nullptr;
    youtube::YtDlpProcess*                ytDlp_ = nullptr;
    bool                                  ownsYtDlp_ = false;
    bool                                  useYtDlpFallback_ = true;
};

} // namespace mf::core::sources

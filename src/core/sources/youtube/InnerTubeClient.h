// InnerTubeClient.h
// Thin wrapper around HttpClient for YouTube's InnerTube API.
// All InnerTube calls go through POST https://<host>/youtubei/v1/<endpoint>
// with a JSON body that always contains a "context.client" object.
//
// The client is multi-client: it tries the request with the first
// configured InnerTube client, falls back to the next on HTTP 4xx,
// JSON parse failure, or PLAYABILITY_STATUS_LOGIN_REQUIRED. This
// matches the yt-dlp `player_client` fallback chain (default order
// varies by yt-dlp version; we pin to the 2025.09.26 release's
// recommended order: WEB → MWEB → TVHTML5_SIMPLY_EMBEDDED_PLAYER →
// ANDROID → IOS).
//
// Visitor data and PoToken are session-scoped state set by the
// caller (Block 5.2.B leaves them empty; a future BotDetectionMitigator
// can populate them).

#pragma once

#include "../HttpClient.h"

#include <QJsonObject>
#include <QList>
#include <QString>
#include <QStringList>

#include <functional>

namespace mf::core::sources::youtube {

class InnerTubeClient {
public:
    struct ClientConfig {
        QString     name;            // "WEB", "MWEB", "TVHTML5_SIMPLY_EMBEDDED_PLAYER", …
        int         clientId = 0;    // X-YouTube-Client-Name header (numeric)
        QString     clientVersion;   // X-YouTube-Client-Version
        QString     userAgent;       // User-Agent header
        QString     host = QStringLiteral("www.youtube.com");
        QStringList androidSdkVersions;  // ANDROID only: ["30", …]
        QString     deviceMake;      // IOS only
        QString     deviceModel;     // IOS only
        QString     osName;          // IOS / ANDROID
        QString     osVersion;       // IOS / ANDROID
    };

    using JsonCallback = std::function<void(QJsonObject /*response*/, QString /*error*/)>;

    InnerTubeClient(mf::core::sources::HttpClient* http, QString apiKey);

    // Pin to a single client (skip fallback). Pass an invalid
    // config (empty name) to restore auto-rotation.
    void setClientOverride(const ClientConfig& cfg);
    void clearClientOverride();
    bool hasClientOverride() const { return manualClientIndex_ >= 0; }
    int  currentClientIndex() const;

    void setVisitorData(QString vd);
    void setPoToken(QString pt);
    QString visitorData() const { return visitorData_; }
    QString poToken()     const { return poToken_; }

    void setRegionLanguage(QString region, QString language);
    QString region()   const { return region_; }
    QString language() const { return language_; }

    // POST https://<host>/youtubei/v1/<endpoint>?key=<apiKey>&prettyPrint=false
    // Body: { "context": { "client": { … } }, ...body-as-merged }
    // Tries clients in order; invokes cb once with the first
    // successful JSON response, or with the concatenated error
    // string after the last client fails.
    void post(const QString& endpoint, const QJsonObject& body, JsonCallback cb);

    void player(const QString& videoId, JsonCallback cb);
    void search(const QString& query, const QString& scope, JsonCallback cb);
    void browse(const QString& browseId, JsonCallback cb);

    // 5 known-good client triples pinned to yt-dlp 2025.09.26.
    // Refresh when YouTube rotates: grep
    // `INNERTUBE_CONTEXT_CLIENT_NAME` and the matching
    // `INNERTUBE_CONTEXT.client` block in yt-dlp's
    // `yt_dlp/extractor/youtube/_base.py`.
    static QList<ClientConfig> defaultClientFallbacks();

    // Public so tests can inspect the per-client triples.
    static QStringList defaultClientNames();

    int clientCount() const { return clientFallbacks_.size(); }

private:
    void tryClient(int index,
                   const QString& endpoint,
                   const QJsonObject& body,
                   JsonCallback cb);

    void buildContext(const ClientConfig& cfg, QJsonObject& contextOut) const;

    mf::core::sources::HttpClient* http_;
    QString                        apiKey_;
    QString                        visitorData_;
    QString                        poToken_;
    QString                        region_   = QStringLiteral("US");
    QString                        language_ = QStringLiteral("en");
    QList<ClientConfig>            clientFallbacks_;
    int                            manualClientIndex_ = -1;
};

} // namespace mf::core::sources::youtube

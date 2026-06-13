// InnerTubeClient.cpp — see InnerTubeClient.h.

#include "InnerTubeClient.h"

#include <QJsonArray>
#include <QJsonDocument>
#include <QJsonObject>

#include <QJsonDocument>
#include <QJsonParseError>
#include <QJsonValue>
#include <QUrl>
#include <QUrlQuery>

#include <utility>

namespace mf::core::sources::youtube {

InnerTubeClient::InnerTubeClient(mf::core::sources::HttpClient* http,
                                 QString apiKey)
    : http_(http)
    , apiKey_(std::move(apiKey))
    , clientFallbacks_(defaultClientFallbacks()) {
}

QList<InnerTubeClient::ClientConfig>
InnerTubeClient::defaultClientFallbacks() {
    return {
        // ── 1. WEB ─────────────────────────────────────────────────────
        // Standard web client. Default UA is set by HttpClient; the
        // body context omits userAgent so YouTube uses the request
        // header verbatim.
        {
            QStringLiteral("WEB"),
            /*clientId=*/1,
            QStringLiteral("2.20250925.01.00"),
            /*userAgent=*/QString(),
            /*host=*/QStringLiteral("www.youtube.com"),
        },
        // ── 2. MEB / Mobile web ────────────────────────────────────────
        // m.youtube.com. The iPad-Safari UA is the one yt-dlp
        // (2025.09.26) uses because it previously skipped PoToken.
        {
            QStringLiteral("MWEB"),
            /*clientId=*/2,
            QStringLiteral("2.20250925.01.00"),
            QStringLiteral("Mozilla/5.0 (iPad; CPU OS 16_7_10 like Mac OS X) "
                           "AppleWebKit/605.1.15 (KHTML, like Gecko) Version/16.6 "
                           "Mobile/15E148 Safari/604.1,gzip(gfe)"),
            /*host=*/QStringLiteral("m.youtube.com"),
        },
        // ── 3. TVHTML5_SIMPLY_EMBEDDED_PLAYER ─────────────────────────
        // In 2025.09.26 this requires auth; included as a
        // best-effort fallback for age-gated / embeddable content.
        // On a 401/403 the fallback chain advances to ANDROID.
        {
            QStringLiteral("TVHTML5_SIMPLY_EMBEDDED_PLAYER"),
            /*clientId=*/85,
            QStringLiteral("2.0"),
            /*userAgent=*/QString(),
            /*host=*/QStringLiteral("www.youtube.com"),
        },
        // ── 4. ANDROID ────────────────────────────────────────────────
        // Doesn't require JS player (REQUIRE_JS_PLAYER = false),
        // so works as a cipher-bypass fallback when the player JS
        // can't be fetched. Doesn't need PoToken for some formats.
        {
            QStringLiteral("ANDROID"),
            /*clientId=*/3,
            QStringLiteral("20.10.38"),
            QStringLiteral("com.google.android.youtube/20.10.38 (Linux; U; Android 11) gzip"),
            /*host=*/QStringLiteral("www.youtube.com"),
            /*androidSdkVersions=*/{QStringLiteral("30")},
            /*deviceMake=*/QString(),
            /*deviceModel=*/QString(),
            /*osName=*/QStringLiteral("Android"),
            /*osVersion=*/QStringLiteral("11"),
        },
        // ── 5. IOS ─────────────────────────────────────────────────────
        // HLS-friendly. iPhone16,2 gets 60fps formats. Often the
        // last-resort client since it requires PoToken for HTTPS.
        {
            QStringLiteral("IOS"),
            /*clientId=*/5,
            QStringLiteral("20.10.4"),
            QStringLiteral("com.google.ios.youtube/20.10.4 (iPhone16,2; U; CPU iOS 18_3_2 like Mac OS X;)"),
            /*host=*/QStringLiteral("www.youtube.com"),
            {},
            QStringLiteral("Apple"),
            QStringLiteral("iPhone16,2"),
            QStringLiteral("iPhone"),
            QStringLiteral("18.3.2.22D82"),
        },
    };
}

QStringList InnerTubeClient::defaultClientNames() {
    QStringList out;
    for (const auto& c : defaultClientFallbacks()) out << c.name;
    return out;
}

void InnerTubeClient::setClientOverride(const ClientConfig& cfg) {
    if (clientFallbacks_.isEmpty()) {
        clientFallbacks_ = defaultClientFallbacks();
    }
    if (manualClientIndex_ < 0) {
        // Replace the entire list with a single-entry one.
        clientFallbacks_ = {cfg};
        manualClientIndex_ = 0;
        return;
    }
    clientFallbacks_[manualClientIndex_] = cfg;
}

void InnerTubeClient::clearClientOverride() {
    clientFallbacks_ = defaultClientFallbacks();
    manualClientIndex_ = -1;
}

int InnerTubeClient::currentClientIndex() const {
    if (manualClientIndex_ >= 0) return manualClientIndex_;
    return 0;
}

void InnerTubeClient::setVisitorData(QString vd) {
    visitorData_ = std::move(vd);
}

void InnerTubeClient::setPoToken(QString pt) {
    poToken_ = std::move(pt);
}

void InnerTubeClient::setRegionLanguage(QString region, QString language) {
    if (!region.isEmpty())   region_   = std::move(region);
    if (!language.isEmpty()) language_ = std::move(language);
}

void InnerTubeClient::buildContext(const ClientConfig& cfg,
                                   QJsonObject& contextOut) const {
    QJsonObject client;
    client.insert(QStringLiteral("clientName"),    cfg.name);
    client.insert(QStringLiteral("clientVersion"), cfg.clientVersion);
    if (!cfg.userAgent.isEmpty()) {
        client.insert(QStringLiteral("userAgent"), cfg.userAgent);
    }
    if (!cfg.androidSdkVersions.isEmpty()) {
        QJsonArray sdk;
        for (const QString& v : cfg.androidSdkVersions) sdk.append(v);
        client.insert(QStringLiteral("androidSdkVersion"), sdk);
    }
    if (!cfg.deviceMake.isEmpty())  client.insert(QStringLiteral("deviceMake"),  cfg.deviceMake);
    if (!cfg.deviceModel.isEmpty()) client.insert(QStringLiteral("deviceModel"), cfg.deviceModel);
    if (!cfg.osName.isEmpty())      client.insert(QStringLiteral("osName"),      cfg.osName);
    if (!cfg.osVersion.isEmpty())   client.insert(QStringLiteral("osVersion"),   cfg.osVersion);
    client.insert(QStringLiteral("hl"), language_);
    client.insert(QStringLiteral("gl"), region_);
    if (!visitorData_.isEmpty()) client.insert(QStringLiteral("visitorData"), visitorData_);
    if (!poToken_.isEmpty())     client.insert(QStringLiteral("poToken"),     poToken_);

    contextOut = QJsonObject();
    contextOut.insert(QStringLiteral("client"), client);
}

void InnerTubeClient::post(const QString& endpoint,
                           const QJsonObject& body,
                           JsonCallback cb) {
    const int startIndex = (manualClientIndex_ >= 0) ? manualClientIndex_ : 0;
    tryClient(startIndex, endpoint, body, std::move(cb));
}

void InnerTubeClient::tryClient(int index,
                                const QString& endpoint,
                                const QJsonObject& body,
                                JsonCallback cb) {
    if (index >= clientFallbacks_.size()) {
        if (cb) cb(QJsonObject{},
                   QStringLiteral("InnerTube: all %1 clients failed for endpoint %2")
                       .arg(clientFallbacks_.size())
                       .arg(endpoint));
        return;
    }
    const ClientConfig cfg = clientFallbacks_[index];

    // Build the merged body: { context: { client: { … } }, ...body }.
    QJsonObject mergedBody;
    buildContext(cfg, mergedBody);
    for (auto it = body.constBegin(); it != body.constEnd(); ++it) {
        mergedBody.insert(it.key(), it.value());
    }
    const QByteArray payload = QJsonDocument(mergedBody).toJson(QJsonDocument::Compact);

    // Build the URL.
    QUrl url(QStringLiteral("https://%1/youtubei/v1/%2").arg(cfg.host, endpoint));
    QUrlQuery q;
    q.addQueryItem(QStringLiteral("key"),         apiKey_);
    q.addQueryItem(QStringLiteral("prettyPrint"), QStringLiteral("false"));
    if (!visitorData_.isEmpty()) {
        q.addQueryItem(QStringLiteral("visitorData"), visitorData_);
    }
    url.setQuery(q);

    // Build the headers.
    QHash<QString, QString> headers;
    headers.insert(QStringLiteral("Content-Type"),         QStringLiteral("application/json"));
    headers.insert(QStringLiteral("X-YouTube-Client-Name"),    QString::number(cfg.clientId));
    headers.insert(QStringLiteral("X-YouTube-Client-Version"), cfg.clientVersion);
    if (!cfg.userAgent.isEmpty()) {
        headers.insert(QStringLiteral("User-Agent"), cfg.userAgent);
    }
    if (cfg.name == QStringLiteral("TVHTML5_SIMPLY_EMBEDDED_PLAYER")) {
        headers.insert(QStringLiteral("Origin"), QStringLiteral("https://www.youtube.com"));
        headers.insert(QStringLiteral("Referer"),
                      QStringLiteral("https://www.youtube.com/"));
    }

    mf::core::sources::HttpRequest req;
    req.url         = url.toString();
    req.method = QByteArrayLiteral("POST");
    req.headers     = headers;
    req.body        = payload;
    req.contentType = QStringLiteral("application/json");
    req.timeoutMs   = 15'000;

    http_->post(req,
        [this, index, endpoint, body, cb = std::move(cb)]
        (mf::core::sources::HttpResponse resp) mutable {
            const QString rotateReason = [&]() -> QString {
                if (!resp.ok()) {
                    return QStringLiteral("HTTP %1: %2")
                        .arg(resp.statusCode)
                        .arg(resp.errorMessage);
                }
                QJsonParseError perr;
                const QJsonDocument doc = QJsonDocument::fromJson(resp.body, &perr);
                if (perr.error != QJsonParseError::NoError) {
                    return QStringLiteral("JSON parse error: %1")
                        .arg(perr.errorString());
                }
                if (!doc.isObject()) {
                    return QStringLiteral("response is not a JSON object");
                }
                const QJsonObject status =
                    doc.object().value(QStringLiteral("playabilityStatus")).toObject();
                const QString sStatus = status.value(QStringLiteral("status")).toString();
                if (sStatus == QStringLiteral("LOGIN_REQUIRED")) {
                    return QStringLiteral("playabilityStatus=LOGIN_REQUIRED");
                }
                return QString();
            }();

            if (!rotateReason.isEmpty()) {
                // Advance to the next client.
                tryClient(index + 1, endpoint, body, std::move(cb));
                return;
            }

            const QJsonDocument doc = QJsonDocument::fromJson(resp.body);
            if (cb) cb(doc.object(), QString());
        });
}

void InnerTubeClient::player(const QString& videoId, JsonCallback cb) {
    QJsonObject body;
    body.insert(QStringLiteral("videoId"),        videoId);
    body.insert(QStringLiteral("contentCheckOk"), true);
    body.insert(QStringLiteral("racyCheckOk"),    true);
    post(QStringLiteral("player"), body, std::move(cb));
}

void InnerTubeClient::search(const QString& query,
                             const QString& scope,
                             JsonCallback cb) {
    QJsonObject body;
    body.insert(QStringLiteral("query"), query);
    if (!scope.isEmpty()) {
        body.insert(QStringLiteral("params"), scope);
    }
    post(QStringLiteral("search"), body, std::move(cb));
}

void InnerTubeClient::browse(const QString& browseId, JsonCallback cb) {
    QJsonObject body;
    body.insert(QStringLiteral("browseId"), browseId);
    post(QStringLiteral("browse"), body, std::move(cb));
}

} // namespace mf::core::sources::youtube
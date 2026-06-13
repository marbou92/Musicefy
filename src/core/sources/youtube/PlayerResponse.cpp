// PlayerResponse.cpp — see PlayerResponse.h.

#include "PlayerResponse.h"

#include <QJsonValue>
#include <QSet>
#include <QStringList>

namespace mf::core::sources::youtube {

namespace {

int jsonIntOr(const QJsonObject& obj, const QString& key, int fallback) {
    const QJsonValue v = obj.value(key);
    if (v.isDouble()) return v.toInt(fallback);
    if (v.isString()) {
        bool ok = false;
        const int n = v.toString().toInt(&ok);
        return ok ? n : fallback;
    }
    return fallback;
}

QString jsonStringOr(const QJsonObject& obj, const QString& key) {
    const QJsonValue v = obj.value(key);
    return v.isString() ? v.toString() : QString();
}

QString jsonStringDeepOr(const QJsonObject& obj,
                         std::initializer_list<const char*> path) {
    QJsonValue cur = obj;
    for (const char* k : path) {
        if (!cur.isObject()) return QString();
        cur = cur.toObject().value(QString::fromLatin1(k));
    }
    return cur.isString() ? cur.toString() : QString();
}

QString pickFirstString(const QJsonObject& obj,
                         std::initializer_list<const char*> keys) {
    for (const char* k : keys) {
        const QString s = jsonStringOr(obj, QString::fromLatin1(k));
        if (!s.isEmpty()) return s;
    }
    return QString();
}

QList<PlayerFormat> parseFormatsArray(const QJsonArray& arr) {
    QList<PlayerFormat> out;
    out.reserve(arr.size());
    for (const QJsonValue& v : arr) {
        if (!v.isObject()) continue;
        const QJsonObject obj = v.toObject();

        PlayerFormat f;
        f.itag            = jsonIntOr(obj, QStringLiteral("itag"), 0);
        f.mimeType        = pickFirstString(obj, {"mimeType", "type"});
        f.bitrate         = jsonIntOr(obj, QStringLiteral("bitrate"), 0);
        f.contentLength   = jsonIntOr(obj, QStringLiteral("contentLength"),
                                      jsonIntOr(obj, QStringLiteral("content_length"), 0));
        f.audioSampleRate = jsonIntOr(obj, QStringLiteral("audioSampleRate"), 0);
        f.qualityLabel    = pickFirstString(obj, {"qualityLabel", "quality"});
        f.width           = jsonIntOr(obj, QStringLiteral("width"), 0);
        f.height          = jsonIntOr(obj, QStringLiteral("height"), 0);
        f.fps             = jsonIntOr(obj, QStringLiteral("fps"), 0);
        f.kind            = PlayerResponseParser::classifyItag(f.itag, f.mimeType);

        // InnerTube returns either a direct `url` (unciphered format)
        // or a `signatureCipher` blob (ciphered format) — never both.
        // The blob is a query-string-style concatenation:
        //   s=<escaped signature>&sp=<signature param>&url=<base url>
        const QString cipher = pickFirstString(obj, {"signatureCipher"});
        const QString direct = pickFirstString(obj, {"url"});
        if (!cipher.isEmpty()) {
            const QStringList parts = cipher.split(QLatin1Char('&'));
            for (const QString& p : parts) {
                if      (p.startsWith(QLatin1String("s=")))   f.signature      = p.mid(2);
                else if (p.startsWith(QLatin1String("sp=")))  f.signatureParam = p.mid(3);
                else if (p.startsWith(QLatin1String("url="))) f.url            = p.mid(4);
            }
        } else {
            f.url = direct;
        }
        out.push_back(std::move(f));
    }
    return out;
}

QString joinErrorReason(const QJsonObject& obj) {
    QString reason = jsonStringDeepOr(obj, {"playabilityStatus", "reason"});
    if (reason.isEmpty()) reason = jsonStringDeepOr(obj, {"playabilityStatus", "errorScreen", "playerErrorMessageRenderer", "simpleText"});
    return reason;
}

} // namespace

PlayerFormat::Kind PlayerResponseParser::classifyItag(int itag, const QString& mimeType) {
    // Audio-only itags commonly seen in 2024-2025 player responses.
    static const QSet<int> kAudioOnly = {
        139, 140, 141, 171, 249, 250, 251,
        // 5xxx-range iOS itags
        18   // progressive (kept Unknown; caller falls back to mimeType)
    };
    if (itag == 18) return PlayerFormat::Kind::Progressive;
    if (kAudioOnly.contains(itag)) return PlayerFormat::Kind::AudioOnly;
    if (mimeType.startsWith(QStringLiteral("audio/")) &&
        !mimeType.contains(QStringLiteral("video"))) {
        return PlayerFormat::Kind::AudioOnly;
    }
    if (mimeType.contains(QStringLiteral("video"))) return PlayerFormat::Kind::VideoOnly;
    return PlayerFormat::Kind::Unknown;
}

PlayerResponse PlayerResponseParser::parse(const QJsonObject& root) {
    PlayerResponse r;

    // Playability gate.
    const QString status = jsonStringDeepOr(root, {"playabilityStatus", "status"});
    if (status == QStringLiteral("OK")) {
        r.status = PlayerResponse::PlayabilityStatus::OK;
    } else if (status == QStringLiteral("LOGIN_REQUIRED")) {
        r.status = PlayerResponse::PlayabilityStatus::LoginRequired;
        r.errorMessage = joinErrorReason(root);
    } else {
        r.status = PlayerResponse::PlayabilityStatus::Unplayable;
        r.errorMessage = joinErrorReason(root);
        if (r.errorMessage.isEmpty()) r.errorMessage = status;
    }

    // VideoDetails.
    r.videoId  = jsonStringDeepOr(root, {"videoDetails", "videoId"});
    r.title    = jsonStringDeepOr(root, {"videoDetails", "title"});
    r.author   = jsonStringDeepOr(root, {"videoDetails", "author"});
    r.channelId = jsonStringDeepOr(root, {"videoDetails", "channelId"});
    r.lengthSeconds = jsonIntOr(root.value(QStringLiteral("videoDetails")).toObject(),
                                 QStringLiteral("lengthSeconds"), 0);

    // Streaming data.
    const QJsonObject streaming = root.value(QStringLiteral("streamingData")).toObject();
    r.hlsManifestUrl  = jsonStringOr(streaming, QStringLiteral("hlsManifestUrl"));
    r.dashManifestUrl = jsonStringOr(streaming, QStringLiteral("dashManifestUrl"));

    QList<PlayerFormat> formats =
        parseFormatsArray(streaming.value(QStringLiteral("formats")).toArray());
    formats.append(parseFormatsArray(streaming.value(QStringLiteral("adaptiveFormats")).toArray()));
    r.formats = std::move(formats);

    // Player JS URL. The `assets.js` field is at the response root in
    // most player builds, but some clients (TVHTML5) nest it under
    // `playerConfig.assets.js`. Capture both.
    r.playerJsUrl = jsonStringDeepOr(root, {"assets", "js"});
    if (r.playerJsUrl.isEmpty()) {
        r.playerJsUrl = jsonStringDeepOr(root, {"playerConfig", "assets", "js"});
    }
    if (r.playerJsUrl.isEmpty()) {
        r.playerJsUrl = jsonStringOr(root, QStringLiteral("assets.js"));
    }

    return r;
}

} // namespace mf::core::sources::youtube
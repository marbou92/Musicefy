// YouTubeExtractor.cpp — see YouTubeExtractor.h.

#include "YouTubeExtractor.h"

#include "Cipher.h"

#include <QJsonArray>
#include <QJsonObject>
#include <QString>
#include <QUrl>
#include <QUrlQuery>

namespace mf::core::sources::youtube {

QString YouTubeExtractor::buildUrl(const PlayerFormat& fmt, const QString& decipheredSig) {
    if (fmt.url.isEmpty()) return QString();

    // The base URL is fine as-is when the format is not ciphered.
    if (decipheredSig.isEmpty()) {
        return fmt.url;
    }

    // Append the deciphered signature. The signature parameter name
    // is whatever the player told us (typically "sig" or "signature");
    // default to "sig" if it omitted one.
    const QString param = fmt.signatureParam.isEmpty()
                              ? QStringLiteral("sig")
                              : fmt.signatureParam;

    QUrl u(fmt.url);
    QUrlQuery q(u);
    q.removeAllQueryItems(param);
    q.addQueryItem(param, decipheredSig);
    u.setQuery(q);
    return u.toString();
}

QString YouTubeExtractor::pickBestAudio(const PlayerResponse& response,
                                        DecipherFn decipher,
                                        QString* errorOut) {
    auto fail = [&](const QString& reason) -> QString {
        if (errorOut) *errorOut = reason;
        return QString();
    };

    if (!response.isPlayable()) {
        return fail(QStringLiteral("Player response is not playable: %1")
                        .arg(response.errorMessage.isEmpty()
                                 ? QStringLiteral("unknown")
                                 : response.errorMessage));
    }

    // Index candidate formats: prefer AudioOnly, then Progressive.
    const PlayerFormat* best = nullptr;
    int bestScore = -1;
    for (const PlayerFormat& f : response.formats) {
        if (f.kind != PlayerFormat::Kind::AudioOnly &&
            f.kind != PlayerFormat::Kind::Progressive) {
            continue;
        }
        // Score: bitrate dominates; sample rate breaks ties; smaller
        // itag is preferred when both are equal (lower file size).
        const int score = f.bitrate
                        + (f.audioSampleRate / 100)
                        - (f.itag / 1000);
        if (score > bestScore) {
            bestScore = score;
            best = &f;
        }
    }
    if (!best) {
        return fail(QStringLiteral("No audio formats in player response"));
    }

    // Unciphered? Just return the URL.
    if (!best->isCiphered()) {
        return best->url;
    }

    // Ciphered — apply the deobfuscator. The session is expected to
    // have already parsed the cipher operations from the player's
    // JavaScript and bound them into the DecipherFn. If no
    // deobfuscator is registered we can't help.
    if (!decipher) {
        return fail(QStringLiteral("Format is ciphered but no deobfuscator is registered"));
    }
    const QString sig = decipher(best->signature);
    if (sig.isEmpty()) {
        return fail(QStringLiteral("Cipher deobfuscation failed (unsupported operations?)"));
    }
    return buildUrl(*best, sig);
}

QString YouTubeExtractor::bestThumbnailUrl(const QJsonArray& thumbnails,
                                           ThumbnailSize size) {
    if (thumbnails.isEmpty()) return QString();

    const int target = static_cast<int>(size);

    // Find the thumbnail whose width is >= target and closest to it.
    // If none qualify, fall back to the largest available.
    QString bestUrl;
    int bestWidth = 0;
    int bestDiff  = INT_MAX;

    for (const QJsonValue& v : thumbnails) {
        if (!v.isObject()) continue;
        const QJsonObject t = v.toObject();
        const QString url = t.value(QStringLiteral("url")).toString();
        const int w = t.value(QStringLiteral("width")).toInt(0);
        if (url.isEmpty()) continue;

        if (w >= target) {
            const int diff = w - target;
            if (diff < bestDiff) {
                bestDiff = diff;
                bestUrl = url;
                bestWidth = w;
            }
        } else if (w > bestWidth) {
            // Under-sized but larger than previous best under-sized.
            bestUrl = url;
            bestWidth = w;
        }
    }

    // Final fallback: if nothing matched at all, use the first entry.
    if (bestUrl.isEmpty() && !thumbnails.isEmpty()) {
        const QJsonObject t0 = thumbnails.first().toObject();
        bestUrl = t0.value(QStringLiteral("url")).toString();
    }

    return bestUrl;
}

} // namespace mf::core::sources::youtube

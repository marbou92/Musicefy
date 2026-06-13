// LyricsService.cpp
// Implementation of the LRCLIB lyrics fetcher.

#include "LyricsService.h"

#include "../models/MusicFile.h"
#include "../sources/HttpClient.h"

#include <QJsonArray>
#include <QJsonDocument>
#include <QJsonObject>
#include <QNetworkRequest>
#include <QRegularExpression>
#include <QUrl>
#include <QUrlQuery>

namespace mf::core::services {

using mf::core::models::MusicFile;
using mf::core::sources::HttpClient;
using mf::core::sources::HttpRequest;
using mf::core::sources::HttpResponse;

static const QString kApiBaseUrl = QStringLiteral("https://lrclib.net/api");
static const QString kUserAgent  = QStringLiteral("MusicefyQt/2.0");

LyricsService::LyricsService(HttpClient* http, QObject* parent)
    : QObject(parent)
    , http_(http)
{
}

QString LyricsService::embeddedLyrics(const MusicFile& track) const
{
    return track.lyrics();
}

// ---------------------------------------------------------------------------
// fetchLyrics
// ---------------------------------------------------------------------------
void LyricsService::fetchLyrics(const MusicFile& track, LyricsCallback callback)
{
    if (!http_ || !callback) {
        if (callback) callback(QString());
        return;
    }

    // Build the /api/get URL with all available metadata.
    QUrl url(kApiBaseUrl + QStringLiteral("/get"));
    QUrlQuery query;

    if (!track.artist().isEmpty())
        query.addQueryItem(QStringLiteral("artist_name"), track.artist());
    if (!track.title().isEmpty())
        query.addQueryItem(QStringLiteral("track_name"), track.title());
    if (!track.album().isEmpty())
        query.addQueryItem(QStringLiteral("album_name"), track.album());

    const int durationSec = static_cast<int>(track.duration().count());
    if (durationSec > 0)
        query.addQueryItem(QStringLiteral("duration"), QString::number(durationSec));

    url.setQuery(query);

    HttpRequest req;
    req.url = url.toString();
    req.headers.insert(QStringLiteral("User-Agent"), kUserAgent);
    req.timeoutMs = 10000;

    http_->get(req, [this, track, callback](HttpResponse resp) {
        if (!resp.ok()) {
            // API miss — fall back to embedded lyrics.
            const QString fallback = embeddedLyrics(track);
            if (callback) callback(fallback);
            emit lyricsFetched(track.title(), fallback);
            return;
        }

        const QJsonDocument doc = QJsonDocument::fromJson(resp.body);
        if (!doc.isObject()) {
            const QString fallback = embeddedLyrics(track);
            if (callback) callback(fallback);
            emit lyricsFetched(track.title(), fallback);
            return;
        }

        const QJsonObject obj = doc.object();

        // Prefer plainLyrics; fall back to stripping timestamps from syncedLyrics.
        QString lyrics = obj.value(QStringLiteral("plainLyrics")).toString();
        if (lyrics.isEmpty()) {
            const QString synced = obj.value(QStringLiteral("syncedLyrics")).toString();
            if (!synced.isEmpty()) {
                lyrics = stripTimestamps(synced);
            }
        }

        // If still empty, try embedded lyrics.
        if (lyrics.isEmpty()) {
            lyrics = embeddedLyrics(track);
        }

        if (callback) callback(lyrics);
        emit lyricsFetched(track.title(), lyrics);
    });
}

// ---------------------------------------------------------------------------
// stripTimestamps  –  remove [mm:ss.xx] tags from LRC text
// ---------------------------------------------------------------------------
QString LyricsService::stripTimestamps(const QString& syncedLyrics)
{
    // LRC timestamp pattern: [mm:ss.xx] or [mm:ss.xxx] or [mm:ss]
    static const QRegularExpression re(QStringLiteral("\\[\\d{1,2}:\\d{2}\\.?\\d{0,3}\\]"));
    QString result = syncedLyrics;
    result.remove(re);
    return result.trimmed();
}

} // namespace mf::core::services

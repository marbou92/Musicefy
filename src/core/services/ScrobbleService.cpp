// ScrobbleService.cpp
// Implementation of the Last.fm scrobble service.

#include "ScrobbleService.h"

#include "../models/MusicFile.h"
#include "../services/SettingsControl.h"
#include "../sources/HttpClient.h"

#include <QCryptographicHash>
#include <QDateTime>
#include <QJsonDocument>
#include <QJsonObject>
#include <QUrlQuery>

namespace mf::core::services {

// ── Settings keys ──────────────────────────────────────────────────────
static const QString kKeySessionKey = QStringLiteral("scrobbler/lastfm_session_key");
static const QString kKeyApiKey     = QStringLiteral("scrobbler/lastfm_api_key");
static const QString kKeyApiSecret  = QStringLiteral("scrobbler/lastfm_api_secret");

static const QString kApiBaseUrl = QStringLiteral("https://ws.audioscrobbler.com/2.0/");

// ── Constructor / Destructor ──────────────────────────────────────────

ScrobbleService::ScrobbleService(mf::core::sources::HttpClient* http,
                                 mf::core::services::SettingsControl* settings,
                                 QObject* parent)
    : QObject(parent)
    , http_(http)
    , settings_(settings)
{
    // Flush timer: every 10 seconds, try to submit queued scrobbles.
    flushTimer_.setInterval(10000);
    connect(&flushTimer_, &QTimer::timeout,
            this, &ScrobbleService::flushQueue);

    // Load enabled state from settings (default: false).
    if (settings_) {
        QVariant v = settings_->get(QStringLiteral("scrobbler/enabled"));
        enabled_ = v.isValid() && v.toBool();
    }

    if (enabled_) {
        flushTimer_.start();
    }
}

ScrobbleService::~ScrobbleService() {
    flushTimer_.stop();
}

// ── Property accessors ────────────────────────────────────────────────

void ScrobbleService::setEnabled(bool v) {
    if (enabled_ == v) return;
    enabled_ = v;
    if (settings_) {
        settings_->set(QStringLiteral("scrobbler/enabled"), v);
    }
    if (v) {
        flushTimer_.start();
    } else {
        flushTimer_.stop();
    }
    emit enabledChanged();
}

bool ScrobbleService::isAuthenticated() const {
    return !apiKey().isEmpty() && !sessionKey().isEmpty();
}

// ── API key / secret / session management ─────────────────────────────

void ScrobbleService::setApiKey(const QString& key) {
    if (settings_) {
        settings_->set(kKeyApiKey, key);
        emit authenticatedChanged();
    }
}

void ScrobbleService::setApiSecret(const QString& secret) {
    if (settings_) {
        settings_->set(kKeyApiSecret, secret);
    }
}

void ScrobbleService::setSessionKey(const QString& sk) {
    if (settings_) {
        settings_->set(kKeySessionKey, sk);
        emit authenticatedChanged();
    }
}

QString ScrobbleService::apiKey() const {
    if (!settings_) return {};
    return settings_->get(kKeyApiKey).toString();
}

QString ScrobbleService::sessionKey() const {
    if (!settings_) return {};
    return settings_->get(kKeySessionKey).toString();
}

void ScrobbleService::clearCredentials() {
    if (!settings_) return;
    settings_->remove(kKeySessionKey);
    emit authenticatedChanged();
}

// ── Public API ────────────────────────────────────────────────────────

void ScrobbleService::nowPlaying(const mf::core::models::MusicFile& track) {
    if (!isAuthenticated()) return;

    ScrobbleRecord record;
    record.artist      = track.artist();
    record.track       = track.title();
    record.album       = track.album();
    record.durationSec = static_cast<int>(track.duration().count());
    record.timestamp   = QDateTime::currentMSecsSinceEpoch() / 1000;

    submitNowPlaying(record);
}

void ScrobbleService::trackFinished(const mf::core::models::MusicFile& track,
                                    int playedDurationSec) {
    if (!isAuthenticated() || !enabled_) return;

    // Last.fm scrobble rule: at least 50% of duration or 240 seconds,
    // whichever is less.
    int durationTotal = static_cast<int>(track.duration().count());
    int threshold = qMin(kScrobbleMinSec, durationTotal / 2);

    if (playedDurationSec < threshold) return;

    ScrobbleRecord record;
    record.artist      = track.artist();
    record.track       = track.title();
    record.album       = track.album();
    record.durationSec = durationTotal;
    record.timestamp   = QDateTime::currentMSecsSinceEpoch() / 1000;

    pendingQueue_.enqueue(record);

    // Try an immediate flush; if the network is down the timer will retry.
    flushQueue();
}

void ScrobbleService::flush() {
    flushQueue();
}

// ── MD5 API signature ────────────────────────────────────────────────

QString ScrobbleService::generateApiSignature(
        const QHash<QString, QString>& params) const {
    // Sort keys alphabetically, concatenate key+value, append secret.
    QStringList keys = params.keys();
    keys.sort(Qt::CaseSensitive);

    QByteArray data;
    for (const QString& k : keys) {
        data.append(k.toUtf8());
        data.append(params.value(k).toUtf8());
    }

    // Append the API secret (not URL-encoded).
    const QString secret = settings_ ? settings_->get(kKeyApiSecret).toString()
                                     : QString();
    data.append(secret.toUtf8());

    return QString::fromLatin1(
        QCryptographicHash::hash(data, QCryptographicHash::Md5).toHex());
}

// ── Internal: build POST params and send ──────────────────────────────

void ScrobbleService::submitScrobble(const ScrobbleRecord& record) {
    QHash<QString, QString> params;
    params[QStringLiteral("method")]  = QStringLiteral("track.scrobble");
    params[QStringLiteral("api_key")] = apiKey();
    params[QStringLiteral("sk")]      = sessionKey();
    params[QStringLiteral("artist")]  = record.artist;
    params[QStringLiteral("track")]   = record.track;
    params[QStringLiteral("timestamp")] =
        QString::number(record.timestamp);
    params[QStringLiteral("format")]  = QStringLiteral("json");

    if (!record.album.isEmpty()) {
        params[QStringLiteral("album")] = record.album;
    }
    if (record.durationSec > 0) {
        params[QStringLiteral("duration")] =
            QString::number(record.durationSec);
    }

    params[QStringLiteral("api_sig")] = generateApiSignature(params);

    // Build URL-encoded body.
    QUrlQuery query;
    const QStringList paramKeys = params.keys();
    for (const QString& k : paramKeys) {
        query.addQueryItem(k, params.value(k));
    }

    mf::core::sources::HttpRequest req;
    req.url         = kApiBaseUrl;
    req.method      = "POST";
    req.body        = query.toString(QUrl::FullyEncoded).toUtf8();
    req.contentType = QStringLiteral("application/x-www-form-urlencoded");

    // We need to capture `record` by value for the lambda.
    ScrobbleRecord recCopy = record;
    http_->post(req, [this, recCopy](const mf::core::sources::HttpResponse& resp) {
        if (resp.ok()) {
            QJsonDocument doc = QJsonDocument::fromJson(resp.body);
            QJsonObject obj = doc.object();
            // Check for errors in the response.
            if (obj.contains(QStringLiteral("error"))) {
                emit scrobbleSubmitted(false,
                    obj.value(QStringLiteral("message")).toString());
            } else {
                emit scrobbleSubmitted(true, QString());
            }
            // On success, the caller (flushQueue) already removes from queue.
            Q_UNUSED(recCopy);
        } else {
            emit scrobbleSubmitted(false, resp.errorMessage);
            Q_UNUSED(recCopy);
        }
    });
}

void ScrobbleService::submitNowPlaying(const ScrobbleRecord& record) {
    QHash<QString, QString> params;
    params[QStringLiteral("method")]  = QStringLiteral("track.updateNowPlaying");
    params[QStringLiteral("api_key")] = apiKey();
    params[QStringLiteral("sk")]      = sessionKey();
    params[QStringLiteral("artist")]  = record.artist;
    params[QStringLiteral("track")]   = record.track;
    params[QStringLiteral("format")]  = QStringLiteral("json");

    if (!record.album.isEmpty()) {
        params[QStringLiteral("album")] = record.album;
    }
    if (record.durationSec > 0) {
        params[QStringLiteral("duration")] =
            QString::number(record.durationSec);
    }

    params[QStringLiteral("api_sig")] = generateApiSignature(params);

    QUrlQuery query;
    const QStringList paramKeys = params.keys();
    for (const QString& k : paramKeys) {
        query.addQueryItem(k, params.value(k));
    }

    mf::core::sources::HttpRequest req;
    req.url         = kApiBaseUrl;
    req.method      = "POST";
    req.body        = query.toString(QUrl::FullyEncoded).toUtf8();
    req.contentType = QStringLiteral("application/x-www-form-urlencoded");

    http_->post(req, [this](const mf::core::sources::HttpResponse& resp) {
        emit nowPlayingUpdated(resp.ok());
    });
}

// ── Queue flush ───────────────────────────────────────────────────────

void ScrobbleService::flushQueue() {
    if (!isAuthenticated()) return;

    // Submit all pending scrobbles. On success the record is removed
    // by the response callback. For now we submit the entire queue
    // and rely on the callback to dequeue on success.
    while (!pendingQueue_.isEmpty()) {
        const ScrobbleRecord record = pendingQueue_.head();

        QHash<QString, QString> params;
        params[QStringLiteral("method")]  = QStringLiteral("track.scrobble");
        params[QStringLiteral("api_key")] = apiKey();
        params[QStringLiteral("sk")]      = sessionKey();
        params[QStringLiteral("artist")]  = record.artist;
        params[QStringLiteral("track")]   = record.track;
        params[QStringLiteral("timestamp")] =
            QString::number(record.timestamp);
        params[QStringLiteral("format")]  = QStringLiteral("json");

        if (!record.album.isEmpty()) {
            params[QStringLiteral("album")] = record.album;
        }
        if (record.durationSec > 0) {
            params[QStringLiteral("duration")] =
                QString::number(record.durationSec);
        }

        params[QStringLiteral("api_sig")] = generateApiSignature(params);

        QUrlQuery query;
        const QStringList paramKeys = params.keys();
        for (const QString& k : paramKeys) {
            query.addQueryItem(k, params.value(k));
        }

        mf::core::sources::HttpRequest req;
        req.url         = kApiBaseUrl;
        req.method      = "POST";
        req.body        = query.toString(QUrl::FullyEncoded).toUtf8();
        req.contentType = QStringLiteral("application/x-www-form-urlencoded");

        http_->post(req, [this, record](const mf::core::sources::HttpResponse& resp) {
            if (resp.ok()) {
                QJsonDocument doc = QJsonDocument::fromJson(resp.body);
                QJsonObject obj = doc.object();
                if (obj.contains(QStringLiteral("error"))) {
                    // Auth or other error — leave in queue for retry.
                    emit scrobbleSubmitted(false,
                        obj.value(QStringLiteral("message")).toString());
                } else {
                    // Success — remove the first matching record from the queue.
                    if (!pendingQueue_.isEmpty()) {
                        pendingQueue_.dequeue();
                    }
                    emit scrobbleSubmitted(true, QString());
                }
            }
            // Network error: leave in queue; flush timer will retry.
        });
    }
}

} // namespace mf::core::services

// SubsonicProvider.cpp
// See SubsonicProvider.h for design notes.

#include "SubsonicProvider.h"

#include <QCryptographicHash>
#include <QJsonArray>
#include <QJsonDocument>
#include <QJsonObject>
#include <QJsonValue>
#include <QRandomGenerator>
#include <QString>
#include <QUrl>
#include <QUrlQuery>

#include <utility>

namespace mf::core::sources {

using mf::core::interfaces::IMusicSourceSession;
using mf::core::models::MusicFile;
using mf::core::models::Playlist;
using mf::core::models::SourceConfigField;
using mf::core::models::StreamingSource;

// ── SubsonicConfig ────────────────────────────────────────────────────

SubsonicConfig SubsonicConfig::fromSource(const StreamingSource& src) {
    SubsonicConfig c;
    const auto& cfg = src.configuration();
    c.serverUrl   = cfg.value(QStringLiteral("serverUrl"), src.url());
    c.username    = cfg.value(QStringLiteral("username"), src.username());
    c.password    = cfg.value(QStringLiteral("password"));
    c.useTokenAuth = cfg.value(QStringLiteral("useTokenAuth"), QStringLiteral("true")) == QStringLiteral("true");    c.apiVersion  = cfg.value(QStringLiteral("apiVersion"), QStringLiteral("1.16.1"));
    c.clientName  = cfg.value(QStringLiteral("clientName"), QStringLiteral("musicefy"));
    return c;
}

// ── SubsonicSession ───────────────────────────────────────────────────

SubsonicSession::SubsonicSession(SubsonicConfig cfg, QString sourceId, HttpClient* http)
    : cfg_(std::move(cfg))
    , sourceId_(std::move(sourceId))
    , http_(http)
{
}

SubsonicSession::~SubsonicSession() = default;

QString SubsonicSession::md5Hex(const QByteArray& input) {
    return QString::fromLatin1(QCryptographicHash::hash(input, QCryptographicHash::Md5).toHex());
}

QString SubsonicSession::freshSalt() const {
    return QString::number(QRandomGenerator::global()->generate64(), 16);
}

QString SubsonicSession::buildAuthedUrl(const QString& endpoint,
                                       const QHash<QString, QString>& extraParams) const {
    QString salt = freshSalt();
    QString token = cfg_.useTokenAuth ? md5Hex((cfg_.password + salt).toUtf8())
                                      : QString();

    QUrl url(cfg_.serverUrl);
    QString path = url.path();
    if (!path.endsWith(QStringLiteral("/"))) path += QStringLiteral("/");
    path += QStringLiteral("rest/") + endpoint;
    url.setPath(path);

    QUrlQuery q;
    q.addQueryItem(QStringLiteral("u"), cfg_.username);
    if (cfg_.useTokenAuth) {
        q.addQueryItem(QStringLiteral("t"), token);
        q.addQueryItem(QStringLiteral("s"), salt);
    } else {
        q.addQueryItem(QStringLiteral("p"), cfg_.password);
    }
    q.addQueryItem(QStringLiteral("v"), cfg_.apiVersion);
    q.addQueryItem(QStringLiteral("c"), cfg_.clientName);
    q.addQueryItem(QStringLiteral("f"), QStringLiteral("json"));
    for (auto it = extraParams.constBegin(); it != extraParams.constEnd(); ++it) {
        q.addQueryItem(it.key(), it.value());
    }
    url.setQuery(q);
    return url.toString();
}

void SubsonicSession::getJson(const QString& endpoint,
                              const QHash<QString, QString>& extraParams,
                              JsonCallback cb) {
    HttpRequest req;
    req.url = buildAuthedUrl(endpoint, extraParams);
    req.headers.insert(QStringLiteral("Accept"), QStringLiteral("application/json"));
    http_->get(req, [cb](HttpResponse resp) {
        if (!resp.ok()) {
            cb(QJsonObject{}, resp.errorMessage.isEmpty()
                                  ? QStringLiteral("HTTP %1").arg(resp.statusCode)
                                  : resp.errorMessage);
            return;
        }
        QJsonParseError err{};
        QJsonDocument doc = QJsonDocument::fromJson(resp.body, &err);
        if (err.error != QJsonParseError::NoError || !doc.isObject()) {
            cb(QJsonObject{}, QStringLiteral("JSON parse error: %1").arg(err.errorString()));
            return;
        }
        QJsonObject root = doc.object();
        QJsonObject sub = root.value(QStringLiteral("subsonic-response")).toObject();
        QString status = sub.value(QStringLiteral("status")).toString();
        if (status != QStringLiteral("ok")) {
            QJsonObject errObj = sub.value(QStringLiteral("error")).toObject();
            QString msg = errObj.value(QStringLiteral("message")).toString();
            cb(QJsonObject{}, msg.isEmpty() ? QStringLiteral("Subsonic error") : msg);
            return;
        }
        cb(sub, QString());
    });
}

void SubsonicSession::ping(BoolCallback onDone) {
    getJson(QStringLiteral("ping"), {}, [this, onDone](QJsonObject obj, QString err) {
        bool ok = err.isEmpty() && !obj.isEmpty();
        if (ok != healthy_) {
            healthy_ = ok;
            emit healthChanged(healthy_);
        }
        if (onDone) onDone(ok, err);
    });
}

void SubsonicSession::searchTracks(QString query, int limit,
                                   ResultCallback onDone,
                                   StringCallback onError) {
    QHash<QString, QString> params{
        {QStringLiteral("query"),  query},
        {QStringLiteral("artistCount"),  QStringLiteral("20")},
        {QStringLiteral("albumCount"),   QStringLiteral("20")},
        {QStringLiteral("songCount"),    QString::number(limit)},
    };
    getJson(QStringLiteral("search3"), params,
        [onDone, onError](QJsonObject obj, QString err) {
        if (!err.isEmpty()) {
            if (onDone)   onDone({});
            if (onError)  onError(err);
            return;
        }
        QJsonObject r = obj.value(QStringLiteral("searchResult3")).toObject();
        QJsonArray songs = r.value(QStringLiteral("song")).toArray();
        QList<MusicFile> out;
        for (const QJsonValue& v : songs) {
            QJsonObject s = v.toObject();
            MusicFile m;
            m.setId(s.value(QStringLiteral("id")).toString());
            m.setTitle(s.value(QStringLiteral("title")).toString());
            m.setArtist(s.value(QStringLiteral("artist")).toString());
            m.setAlbum(s.value(QStringLiteral("album")).toString());
            m.setYear(s.value(QStringLiteral("year")).toInt());
            m.setGenre(s.value(QStringLiteral("genre")).toString());
            m.setTrackNumber(s.value(QStringLiteral("track")).toInt());
            m.setDuration(std::chrono::seconds{ s.value(QStringLiteral("duration")).toInt() });
            m.setBitrate(s.value(QStringLiteral("bitRate")).toInt());
            m.setFileSize(s.value(QStringLiteral("size")).toVariant().toLongLong());
            m.setCoverPath(s.value(QStringLiteral("coverArt")).toString());
            m.setSourceUri(s.value(QStringLiteral("streamUrl")).toString());
            m.setSourceType(QStringLiteral("subsonic"));
            m.setAlbumArtist(s.value(QStringLiteral("albumArtist")).toString());
            out.append(m);
        }
        if (onDone) onDone(out);
    });
}

void SubsonicSession::fetchStreamUrl(QString trackId, StringCallback onDone, StringCallback /*onError*/) {
    // Subsonic stream URLs are stateless: anyone with credentials can fetch
    // the audio. We return a pre-signed URL the player can use directly.
    QString url = buildAuthedUrl(QStringLiteral("stream"), {{QStringLiteral("id"), trackId}});
    if (onDone) onDone(url);
}

void SubsonicSession::fetchLyrics(QString trackId, StringCallback onDone, StringCallback onError) {
    getJson(QStringLiteral("getLyrics"), {{QStringLiteral("id"), trackId}},
            [onDone, onError](QJsonObject obj, QString err) {
        if (!err.isEmpty()) {
            if (onError) onError(err);
            return;
        }
        QJsonObject lr = obj.value(QStringLiteral("lyrics")).toObject();
        QString text = lr.value(QStringLiteral("value")).toString();
        if (onDone) onDone(text);
    });
}

void SubsonicSession::fetchCover(QString trackId, BytesCallback onDone, StringCallback onError) {
    HttpRequest req;
    req.url = buildAuthedUrl(QStringLiteral("getCoverArt"),
                             {{QStringLiteral("id"), trackId},
                              {QStringLiteral("size"), QStringLiteral("500")}});
    http_->get(req, [onDone, onError](HttpResponse resp) {
        if (!resp.ok()) {
            if (onError) onError(resp.errorMessage);
            return;
        }
        if (onDone) onDone(resp.body);
    });
}

namespace {

QList<MusicFile> artistsFromJson(const QJsonArray& a) {
    QList<MusicFile> out;
    for (const QJsonValue& v : a) {
        QJsonObject o = v.toObject();
        MusicFile m;
        m.setId(o.value(QStringLiteral("id")).toString());
        m.setArtist(o.value(QStringLiteral("name")).toString());
        m.setCoverPath(o.value(QStringLiteral("coverArt")).toString());
        m.setSourceType(QStringLiteral("subsonic"));
        // albums() — not part of MusicFile, callers handle separately
        out.append(m);
    }
    return out;
}

QList<MusicFile> songsFromJson(const QJsonArray& a) {
    QList<MusicFile> out;
    for (const QJsonValue& v : a) {
        QJsonObject s = v.toObject();
        MusicFile m;
        m.setId(s.value(QStringLiteral("id")).toString());
        m.setTitle(s.value(QStringLiteral("title")).toString());
        m.setArtist(s.value(QStringLiteral("artist")).toString());
        m.setAlbum(s.value(QStringLiteral("album")).toString());
        m.setYear(s.value(QStringLiteral("year")).toInt());
        m.setGenre(s.value(QStringLiteral("genre")).toString());
        m.setTrackNumber(s.value(QStringLiteral("track")).toInt());
        m.setDuration(std::chrono::seconds{ s.value(QStringLiteral("duration")).toInt() });
        m.setBitrate(s.value(QStringLiteral("bitRate")).toInt());
        m.setFileSize(s.value(QStringLiteral("size")).toVariant().toLongLong());
        m.setCoverPath(s.value(QStringLiteral("coverArt")).toString());
        m.setSourceUri(s.value(QStringLiteral("streamUrl")).toString());
        m.setSourceType(QStringLiteral("subsonic"));
        out.append(m);
    }
    return out;
}

} // namespace

void SubsonicSession::listArtists(AsyncResultCallback onDone) {
    getJson(QStringLiteral("getArtists"), {}, [onDone](QJsonObject obj, QString err) {
        if (!err.isEmpty()) { if (onDone) onDone({}, err); return; }
        QJsonObject r = obj.value(QStringLiteral("artists")).toObject();
        QList<MusicFile> all;
        QJsonArray indexes = r.value(QStringLiteral("index")).toArray();
        for (const QJsonValue& idx : indexes) {
            all.append(artistsFromJson(idx.toObject().value(QStringLiteral("artist")).toArray()));
        }
        if (onDone) onDone(all, QString());
    });
}

void SubsonicSession::getArtist(QString artistId, AsyncResultCallback onDone) {
    getJson(QStringLiteral("getArtist"), {{QStringLiteral("id"), artistId}},
            [onDone](QJsonObject obj, QString err) {
        if (!err.isEmpty()) { if (onDone) onDone({}, err); return; }
        QList<MusicFile> out = songsFromJson(
            obj.value(QStringLiteral("artist")).toObject()
               .value(QStringLiteral("album")).toArray()
        );
        if (onDone) onDone(out, QString());
    });
}

void SubsonicSession::getAlbum(QString albumId, AsyncResultCallback onDone) {
    getJson(QStringLiteral("getAlbum"), {{QStringLiteral("id"), albumId}},
            [onDone](QJsonObject obj, QString err) {
        if (!err.isEmpty()) { if (onDone) onDone({}, err); return; }
        QList<MusicFile> out = songsFromJson(
            obj.value(QStringLiteral("album")).toObject()
               .value(QStringLiteral("song")).toArray()
        );
        if (onDone) onDone(out, QString());
    });
}

void SubsonicSession::getAlbumList(QString type, int size, AsyncResultCallback onDone) {
    getJson(QStringLiteral("getAlbumList2"),
            {{QStringLiteral("type"), type},
             {QStringLiteral("size"), QString::number(size)}},
            [onDone](QJsonObject obj, QString err) {
        if (!err.isEmpty()) { if (onDone) onDone({}, err); return; }
        QJsonArray a = obj.value(QStringLiteral("albumList2")).toObject()
                          .value(QStringLiteral("album")).toArray();
        QList<MusicFile> out;
        for (const QJsonValue& v : a) {
            QJsonObject o = v.toObject();
            MusicFile m;
            m.setId(o.value(QStringLiteral("id")).toString());
            m.setTitle(o.value(QStringLiteral("title")).toString());
            m.setArtist(o.value(QStringLiteral("artist")).toString());
            m.setYear(o.value(QStringLiteral("year")).toInt());
            m.setCoverPath(o.value(QStringLiteral("coverArt")).toString());
            m.setSourceType(QStringLiteral("subsonic"));
            out.append(m);
        }
        if (onDone) onDone(out, QString());
    });
}

// ── Subsonic playlists ────────────────────────────────────────────────

namespace {

Playlist playlistFromJson(const QJsonObject& o) {
    Playlist p;
    p.setId(o.value(QStringLiteral("id")).toString());
    p.setName(o.value(QStringLiteral("name")).toString());
    p.setSongCount(o.value(QStringLiteral("songCount")).toInt());
    p.setDuration(std::chrono::seconds{
        o.value(QStringLiteral("duration")).toInt()});
    p.setIsPublic(o.value(QStringLiteral("public")).toBool());
    p.setOwner(o.value(QStringLiteral("owner")).toString());
    p.setCoverArt(o.value(QStringLiteral("coverArt")).toString());
    QJsonArray entries = o.value(QStringLiteral("entry")).toArray();
    QStringList ids;
    ids.reserve(entries.size());
    for (const QJsonValue& v : entries) {
        ids.append(v.toObject().value(QStringLiteral("id")).toString());
    }
    p.setTrackIds(std::move(ids));
    return p;
}

} // namespace

void SubsonicSession::getPlaylists(PlaylistsCallback onDone) {
    getJson(QStringLiteral("getPlaylists"), {},
            [onDone](QJsonObject obj, QString err) {
        if (!err.isEmpty()) { if (onDone) onDone({}, err); return; }
        QJsonArray arr = obj.value(QStringLiteral("playlists"))
                            .toObject()
                            .value(QStringLiteral("playlist")).toArray();
        QList<Playlist> out;
        out.reserve(arr.size());
        for (const QJsonValue& v : arr) {
            out.append(playlistFromJson(v.toObject()));
        }
        if (onDone) onDone(out, QString());
    });
}

void SubsonicSession::createPlaylist(QString name, QStringList trackIds, bool isPublic,
                                     PlaylistsCallback onDone) {
    QHash<QString, QString> params{
        {QStringLiteral("name"),    name},
        {QStringLiteral("songIds"), trackIds.join(QLatin1Char(','))},
        {QStringLiteral("public"),  isPublic ? QStringLiteral("true")
                                              : QStringLiteral("false")},
    };
    getJson(QStringLiteral("createPlaylist"), params,
            [onDone](QJsonObject obj, QString err) {
        if (!err.isEmpty()) { if (onDone) onDone({}, err); return; }
        QJsonObject pl = obj.value(QStringLiteral("playlist")).toObject();
        QList<Playlist> out;
        if (!pl.isEmpty()) out.append(playlistFromJson(pl));
        if (onDone) onDone(out, QString());
    });
}

void SubsonicSession::updatePlaylist(QString id, QString name, QStringList trackIds,
                                     bool isPublic, PlaylistsCallback onDone) {
    QHash<QString, QString> params{
        {QStringLiteral("playlistId"), id},
        {QStringLiteral("name"),       name},
        {QStringLiteral("songIds"),    trackIds.join(QLatin1Char(','))},
        {QStringLiteral("public"),     isPublic ? QStringLiteral("true")
                                                 : QStringLiteral("false")},
    };
    getJson(QStringLiteral("updatePlaylist"), params,
            [onDone](QJsonObject /*obj*/, QString err) {
        // Subsonic returns an empty body on success. We pass the
        // (possibly-empty) Playlist list back so the caller can
        // distinguish success vs error.
        if (!err.isEmpty()) { if (onDone) onDone({}, err); return; }
        if (onDone) onDone({}, QString());
    });
}

void SubsonicSession::deletePlaylist(QString id, BoolCallback onDone) {
    QHash<QString, QString> params{
        {QStringLiteral("id"), id},
    };
    getJson(QStringLiteral("deletePlaylist"), params,
            [onDone](QJsonObject /*obj*/, QString err) {
        if (onDone) onDone(err.isEmpty(), err);
    });
}

// ── SubsonicProvider ──────────────────────────────────────────────────

SubsonicProvider::SubsonicProvider()
    : http_(new HttpClient())
    , ownsHttp_(true)
{
}

SubsonicProvider::SubsonicProvider(HttpClient* sharedHttp)
    : http_(sharedHttp)
    , ownsHttp_(false)
{
}

SubsonicProvider::~SubsonicProvider() {
    if (ownsHttp_) {
        delete http_;
    }
}

QList<SourceConfigField> SubsonicProvider::configFields() const {
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
        make(QStringLiteral("serverUrl"),
             QStringLiteral("Server URL"),
             QStringLiteral("https://navidrome.example.com"),
             QString(),
             QStringLiteral("url"), true),
        make(QStringLiteral("username"),
             QStringLiteral("Username"),
             QString(),
             QString(),
             QStringLiteral("text"), true),
        make(QStringLiteral("password"),
             QStringLiteral("Password / API key"),
             QString(),
             QString(),
             QStringLiteral("password"), true),
        make(QStringLiteral("useTokenAuth"),
             QStringLiteral("Use token authentication"),
             QString(),
             QStringLiteral("true"),
             QStringLiteral("checkbox"), false),
    };
}

std::unique_ptr<IMusicSourceSession> SubsonicProvider::createSession(
    const StreamingSource& source) const {
    SubsonicConfig cfg = SubsonicConfig::fromSource(source);
    return std::make_unique<SubsonicSession>(cfg, source.id(), http_);
}

} // namespace mf::core::sources
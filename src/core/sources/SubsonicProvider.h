// SubsonicProvider.h
// Subsonic / Navidrome / Funkwhale / Airsonic source. Implements the
// Subsonic REST API (v1.16.1) for streaming, library browsing, and
// search. Other Subsonic-compatible servers (Navidrome, Funkwhale, etc.)
// work without changes.
//
// Auth: md5(password + salt) where the salt is randomly generated per
// request. The full URL always includes:
//   ?u=<user>&t=<md5>&s=<salt>&v=1.16.1&c=musicefy&f=json
//
// Endpoints used:
//   /rest/ping                 — connectivity test
//   /rest/getMusicFolders      — top-level folders
//   /rest/getIndexes           — alphabetical index
//   /rest/getArtists           — all artists (replacement for getIndexes in newer servers)
//   /rest/getArtist            — albums for an artist
//   /rest/getAlbum             — tracks for an album
//   /rest/getAlbumList2        — newest/random/frequent/albums
//   /rest/search3              — search
//   /rest/getCoverArt          — cover art bytes
//   /rest/stream               — audio stream URL
//   /rest/getLyrics            — lyrics
//   /rest/scrobble             — last.fm-style scrobble
//   /rest/getPlaylists         — list playlists
//   /rest/createPlaylist / updatePlaylist / deletePlaylist

#pragma once

#include "../interfaces/IMusicSourceProvider.h"
#include "../interfaces/IMusicSourceSession.h"
#include "../models/MusicFile.h"
#include "../models/Playlist.h"
#include "../models/SourceConfigField.h"
#include "../models/StreamingSource.h"

#include <QHash>
#include <QJsonObject>
#include <QList>
#include <QString>
#include <QStringList>

#include <functional>
#include <memory>

#include "HttpClient.h"

namespace mf::core::sources {

struct SubsonicConfig {
    QString serverUrl;   // e.g. https://navidrome.example.com
    QString username;
    QString password;     // plain or token; the session hashes it with the salt
    bool    useTokenAuth = true; // true → token, false → plain password (legacy)
    QString apiVersion   = QStringLiteral("1.16.1");
    QString clientName    = QStringLiteral("musicefy");

    static SubsonicConfig fromSource(const mf::core::models::StreamingSource& src);
};

class SubsonicSession : public QObject, public mf::core::interfaces::IMusicSourceSession {
    Q_OBJECT
public:
    using AsyncResultCallback = std::function<void(QList<mf::core::models::MusicFile>, QString /*error*/)>;
    using JsonCallback         = std::function<void(QJsonObject, QString /*error*/)>;

    SubsonicSession(SubsonicConfig cfg, QString sourceId, HttpClient* http);
    ~SubsonicSession() override;

    QString sourceType() const override { return QStringLiteral("subsonic"); }
    QString sourceId()   const override { return sourceId_; }
    bool    isHealthy()  const override { return healthy_; }

    void searchTracks(QString query, int limit,
                      ResultCallback onDone,
                      StringCallback onError) override;
    void fetchStreamUrl(QString trackId, StringCallback onDone, StringCallback onError) override;
    void fetchLyrics(QString trackId, StringCallback onDone, StringCallback onError) override;
    void fetchCover(QString trackId, BytesCallback onDone, StringCallback onError) override;
    void ping(BoolCallback onDone) override;

    // Higher-level browsing methods (not on the interface, used by services).
    void listArtists(AsyncResultCallback onDone);
    void getArtist(QString artistId, AsyncResultCallback onDone);
    void getAlbum(QString albumId, AsyncResultCallback onDone);
    void getAlbumList(QString type, int size, AsyncResultCallback onDone);

    // Playlist CRUD (also not on the interface; used by PlaylistService).
    using PlaylistsCallback = std::function<void(QList<mf::core::models::Playlist>, QString /*err*/)>;
    void getPlaylists(PlaylistsCallback onDone);
    void createPlaylist(QString name, QStringList trackIds, bool isPublic,
                        PlaylistsCallback onDone);
    void updatePlaylist(QString id, QString name, QStringList trackIds, bool isPublic,
                        PlaylistsCallback onDone);
    void deletePlaylist(QString id, BoolCallback onDone);

signals:
    void healthChanged(bool isHealthy);

private:
    QString buildAuthedUrl(const QString& endpoint,
                           const QHash<QString, QString>& extraParams = {}) const;
    void    getJson(const QString& endpoint,
                    const QHash<QString, QString>& extraParams,
                    JsonCallback cb);
    static QString md5Hex(const QByteArray& input);
    QString freshSalt() const;

    SubsonicConfig  cfg_;
    QString         sourceId_;
    HttpClient*     http_;
    bool            healthy_ = false;
};

class SubsonicProvider : public QObject, public mf::core::interfaces::IMusicSourceProvider {
    Q_OBJECT
public:
    SubsonicProvider();
    explicit SubsonicProvider(HttpClient* sharedHttp);
    ~SubsonicProvider() override;

    QString sourceType() const override { return QStringLiteral("subsonic"); }
    QString displayName() const override { return QStringLiteral("Subsonic (Navidrome, Funkwhale, …)"); }
    QList<mf::core::models::SourceConfigField> configFields() const override;

    std::unique_ptr<mf::core::interfaces::IMusicSourceSession> createSession(
        const mf::core::models::StreamingSource& source) const override;

private:
    HttpClient* http_ = nullptr;        // owned if we created it
    bool        ownsHttp_ = false;
};

} // namespace mf::core::sources

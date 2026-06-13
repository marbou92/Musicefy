#include "LibraryRepository.h"
#include <QDebug>
#include <QDir>
#include <QVariant>

#include "Database.h"

#include <QSqlError>
#include <QSqlQuery>
#include <QSqlRecord>
#include <QUuid>
#include <QVariant>

#include <chrono>

namespace mf::core::database {

using namespace mf::core::models;

namespace {

QString getString(QSqlQuery& q, const QString& field) {
    return q.value(field).toString();
}

int getInt(QSqlQuery& q, const QString& field) {
    return q.value(field).toInt();
}

qint64 getInt64(QSqlQuery& q, const QString& field) {
    return q.value(field).toLongLong();
}

QDateTime getDateTime(QSqlQuery& q, const QString& field) {
    qint64 secs = q.value(field).toLongLong();
    if (secs <= 0) {
        return QDateTime::fromSecsSinceEpoch(0);
    }
    return QDateTime::fromSecsSinceEpoch(secs);
}

std::optional<double> getOptionalDouble(QSqlQuery& q, const QString& field) {
    QVariant v = q.value(field);
    if (v.isNull()) {
        return std::nullopt;
    }
    return v.toDouble();
}

MusicFile trackFromRow(QSqlQuery& q) {
    MusicFile m;
    m.setId(getString(q, "id"));
    m.setFilePath(getString(q, "file_path"));
    m.setTitle(getString(q, "title"));
    m.setArtist(getString(q, "artist"));
    m.setAlbum(getString(q, "album"));
    m.setYear(getInt(q, "year"));
    m.setGenre(getString(q, "genre"));
    int secs = getInt(q, "duration_secs");
    m.setDuration(std::chrono::seconds{ std::max(0, secs) });
    m.setTrackNumber(getInt(q, "track_number"));
    m.setBitrate(getInt(q, "bitrate"));
    m.setFileSize(getInt64(q, "file_size"));
    m.setLyrics(getString(q, "lyrics"));
    m.setCoverPath(getString(q, "cover_path"));
    m.setSourceUri(getString(q, "source_uri"));
    m.setSourceType(getString(q, "source_type"));
    m.setPlayCount(getInt(q, "play_count"));
    m.setLastPlayed(getDateTime(q, "last_played"));
    m.setIsFavourite(getInt(q, "is_favourite") != 0);
    m.setIsDownloaded(getInt(q, "is_downloaded") != 0);
    QString dateAddedStr = getString(q, "date_added");
    if (!dateAddedStr.isEmpty()) {
        bool ok;
        qint64 dateSecs = dateAddedStr.toLongLong(&ok);
        if (ok && dateSecs > 0) {
            m.setDateAdded(QDateTime::fromSecsSinceEpoch(dateSecs));
        }
    }
    m.setAlbumArtist(getString(q, "album_artist"));
    m.setYouTubeVideoId(getString(q, "you_tube_video_id"));
    m.setYouTubeBrowseId(getString(q, "you_tube_browse_id"));
    m.setYouTubePlaylistId(getString(q, "you_tube_playlist_id"));
    m.setYouTubeMusicVideoType(getString(q, "you_tube_music_video_type"));
    m.setLoudnessDb(getOptionalDouble(q, "loudness_db"));
    m.setAudioFormat(getString(q, "audio_format"));
    return m;
}

} // namespace

LibraryRepository::LibraryRepository(Database& db)
    : db_(db)
{
}

std::optional<MusicFile> LibraryRepository::trackByPath(QString filePath) const {
    QSqlQuery q(db_.connection());
    q.prepare(QStringLiteral("SELECT * FROM tracks WHERE file_path = ?"));
    q.addBindValue(filePath);
    if (!q.exec() || !q.next()) {
        return std::nullopt;
    }
    return trackFromRow(q);
}

std::optional<MusicFile> LibraryRepository::trackById(QString id) const {
    QSqlQuery q(db_.connection());
    q.prepare(QStringLiteral("SELECT * FROM tracks WHERE id = ?"));
    q.addBindValue(id);
    if (!q.exec() || !q.next()) {
        return std::nullopt;
    }
    return trackFromRow(q);
}

QList<MusicFile> LibraryRepository::allTracks(int limit, int offset) const {
    QList<MusicFile> out;
    QSqlQuery q(db_.connection());
    QString sql = QStringLiteral("SELECT * FROM tracks ORDER BY title COLLATE NOCASE");
    if (limit > 0) {
        sql += QStringLiteral(" LIMIT %1 OFFSET %2").arg(limit).arg(offset);
    }
    if (!q.exec(sql)) {
        return out;
    }
    while (q.next()) {
        out.append(trackFromRow(q));
    }
    return out;
}

QList<MusicFile> LibraryRepository::tracksForAlbum(QString albumId) const {
    QList<MusicFile> out;
    QSqlQuery q(db_.connection());
    q.prepare(QStringLiteral("SELECT * FROM tracks WHERE album_id = ? ORDER BY track_number"));
    q.addBindValue(albumId);
    if (!q.exec()) {
        return out;
    }
    while (q.next()) {
        out.append(trackFromRow(q));
    }
    return out;
}

QList<MusicFile> LibraryRepository::tracksForArtist(QString artistId) const {
    QList<MusicFile> out;
    QSqlQuery q(db_.connection());
    q.prepare(QStringLiteral("SELECT * FROM tracks WHERE artist_id = ? ORDER BY album, track_number"));
    q.addBindValue(artistId);
    if (!q.exec()) {
        return out;
    }
    while (q.next()) {
        out.append(trackFromRow(q));
    }
    return out;
}

QList<MusicFile> LibraryRepository::favouriteTracks() const {
    QList<MusicFile> out;
    QSqlQuery q(db_.connection());
    q.prepare(QStringLiteral("SELECT * FROM tracks WHERE is_favourite = 1 ORDER BY title"));
    if (!q.exec()) {
        return out;
    }
    while (q.next()) {
        out.append(trackFromRow(q));
    }
    return out;
}

QList<MusicFile> LibraryRepository::recentlyPlayedTracks(int limit) const {
    QList<MusicFile> out;
    QSqlQuery q(db_.connection());
    QString sql = QStringLiteral(
        "SELECT * FROM tracks"
        " WHERE last_played IS NOT NULL AND last_played > 0"
        " ORDER BY last_played DESC");
    if (limit > 0) {
        sql += QStringLiteral(" LIMIT %1").arg(limit);
    }
    if (!q.exec(sql)) {
        qWarning() << "recentlyPlayedTracks failed:" << q.lastError().text();
        return out;
    }
    while (q.next()) {
        out.append(trackFromRow(q));
    }
    return out;
}

void LibraryRepository::upsertTrack(const MusicFile& track) {
    QSqlQuery q(db_.connection());
    q.prepare(QStringLiteral(
        "INSERT INTO tracks ("
        "id, file_path, title, artist, album, year, genre,"
        "duration_secs, track_number, bitrate, file_size, lyrics, cover_path,"
        "source_uri, source_type, play_count, last_played,"
        "is_favourite, is_downloaded, date_added,"
        "album_artist, you_tube_video_id, you_tube_browse_id,"
        "you_tube_playlist_id, you_tube_music_video_type,"
        "loudness_db, audio_format"
        ") VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)"
        " ON CONFLICT(file_path) DO UPDATE SET"
        " title=excluded.title, artist=excluded.artist, album=excluded.album,"
        " year=excluded.year, genre=excluded.genre,"
        " duration_secs=excluded.duration_secs, track_number=excluded.track_number,"
        " bitrate=excluded.bitrate, file_size=excluded.file_size,"
        " lyrics=excluded.lyrics, cover_path=excluded.cover_path,"
        " source_uri=excluded.source_uri, source_type=excluded.source_type,"
        " play_count=excluded.play_count, last_played=excluded.last_played,"
        " is_favourite=excluded.is_favourite, is_downloaded=excluded.is_downloaded,"
        " date_added=excluded.date_added, album_artist=excluded.album_artist,"
        " you_tube_video_id=excluded.you_tube_video_id,"
        " you_tube_browse_id=excluded.you_tube_browse_id,"
        " you_tube_playlist_id=excluded.you_tube_playlist_id,"
        " you_tube_music_video_type=excluded.you_tube_music_video_type,"
        " loudness_db=excluded.loudness_db, audio_format=excluded.audio_format"
    ));
    q.addBindValue(track.id().isEmpty() ? QVariant(QUuid::createUuid().toString(QUuid::WithoutBraces)) : QVariant(track.id()));
    q.addBindValue(track.filePath());
    q.addBindValue(track.title());
    q.addBindValue(track.artist());
    q.addBindValue(track.album());
    q.addBindValue(track.year());
    q.addBindValue(track.genre());
    q.addBindValue(static_cast<qint64>(track.duration().count()));
    q.addBindValue(track.trackNumber());
    q.addBindValue(track.bitrate());
    q.addBindValue(track.fileSize());
    q.addBindValue(track.lyrics());
    q.addBindValue(track.coverPath());
    q.addBindValue(track.sourceUri());
    q.addBindValue(track.sourceType());
    q.addBindValue(track.playCount());
    q.addBindValue(track.lastPlayed().isValid() ? track.lastPlayed().toSecsSinceEpoch() : QVariant());
    q.addBindValue(track.isFavourite() ? 1 : 0);
    q.addBindValue(track.isDownloaded() ? 1 : 0);
    q.addBindValue(track.dateAdded().has_value() ? QVariant::fromValue(track.dateAdded()->toSecsSinceEpoch()) : QVariant());
    q.addBindValue(track.albumArtist());
    q.addBindValue(track.youTubeVideoId());
    q.addBindValue(track.youTubeBrowseId());
    q.addBindValue(track.youTubePlaylistId());
    q.addBindValue(track.youTubeMusicVideoType());
    q.addBindValue(track.loudnessDb().has_value() ? QVariant(track.loudnessDb().value()) : QVariant());
    q.addBindValue(track.audioFormat());
    if (!q.exec()) {
        qWarning() << "upsertTrack failed:" << q.lastError().text();
    }
}

void LibraryRepository::deleteTrack(QString filePath) {
    QSqlQuery q(db_.connection());
    q.prepare(QStringLiteral("DELETE FROM tracks WHERE file_path = ?"));
    q.addBindValue(filePath);
    if (!q.exec()) {
        qWarning() << "deleteTrack failed:" << q.lastError().text();
    }
}

int LibraryRepository::deleteTracksByPathPrefix(QString pathPrefix) {
    // Normalize to a clean absolute path with a trailing separator so
    // "C:/Music" doesn't also delete "C:/Musical Chairs/track.mp3".
    QString clean = QDir::cleanPath(pathPrefix);
    if (clean.isEmpty()) return 0;
    if (!clean.endsWith(QLatin1Char('/'))) {
        clean.append(QLatin1Char('/'));
    }
    QSqlQuery q(db_.connection());
    q.prepare(QStringLiteral("DELETE FROM tracks WHERE file_path LIKE ?"));
    q.addBindValue(clean + QStringLiteral("%"));
    if (!q.exec()) {
        qWarning() << "deleteTracksByPathPrefix failed:" << q.lastError().text();
        return 0;
    }
    return q.numRowsAffected();
}

void LibraryRepository::incrementPlayCount(QString filePath) {
    QSqlQuery q(db_.connection());
    q.prepare(QStringLiteral("UPDATE tracks SET play_count = play_count + 1, last_played = ? WHERE file_path = ?"));
    q.addBindValue(QDateTime::currentSecsSinceEpoch());
    q.addBindValue(filePath);
    if (!q.exec()) {
        qWarning() << "incrementPlayCount failed:" << q.lastError().text();
    }
}

void LibraryRepository::toggleFavourite(QString filePath) {
    QSqlQuery q(db_.connection());
    q.prepare(QStringLiteral("UPDATE tracks SET is_favourite = 1 - is_favourite WHERE file_path = ?"));
    q.addBindValue(filePath);
    if (!q.exec()) {
        qWarning() << "toggleFavourite failed:" << q.lastError().text();
    }
}

int LibraryRepository::trackCount() const {
    QSqlQuery q(db_.connection());
    if (!q.exec(QStringLiteral("SELECT COUNT(*) FROM tracks")) || !q.next()) {
        return 0;
    }
    return q.value(0).toInt();
}

QList<ArtistInfo> LibraryRepository::allArtists() const {
    QList<ArtistInfo> out;
    QSqlQuery q(db_.connection());
    q.prepare(QStringLiteral("SELECT * FROM artists ORDER BY name COLLATE NOCASE"));
    if (!q.exec()) {
        return out;
    }
    while (q.next()) {
        ArtistInfo a;
        a.setId(getString(q, "id"));
        a.setName(getString(q, "name"));
        a.setCoverPath(getString(q, "cover_path"));
        a.setSourceType(getString(q, "source_type"));
        a.setYouTubeChannelId(getString(q, "you_tube_channel_id"));
        a.setDescription(getString(q, "description"));
        a.setIsFollowed(getInt(q, "is_followed") != 0);
        out.append(a);
    }
    return out;
}

void LibraryRepository::upsertArtist(const ArtistInfo& artist) {
    QSqlQuery q(db_.connection());
    q.prepare(QStringLiteral(
        "INSERT INTO artists (id, name, cover_path, source_type, you_tube_channel_id,"
        "description, is_followed, last_browsed_at) VALUES (?, ?, ?, ?, ?, ?, ?, ?)"
        " ON CONFLICT(id) DO UPDATE SET"
        " name=excluded.name, cover_path=excluded.cover_path,"
        " description=excluded.description, is_followed=excluded.is_followed"
    ));
    q.addBindValue(artist.id().isEmpty() ? QVariant(QUuid::createUuid().toString(QUuid::WithoutBraces)) : QVariant(artist.id()));
    q.addBindValue(artist.name());
    q.addBindValue(artist.coverPath());
    q.addBindValue(artist.sourceType());
    q.addBindValue(artist.youTubeChannelId());
    q.addBindValue(artist.description());
    q.addBindValue(artist.isFollowed() ? 1 : 0);
    if (!q.exec()) {
        qWarning() << "upsertArtist failed:" << q.lastError().text();
    }
}

QList<AlbumInfo> LibraryRepository::allAlbums() const {
    QList<AlbumInfo> out;
    QSqlQuery q(db_.connection());
    q.prepare(QStringLiteral("SELECT * FROM albums ORDER BY name COLLATE NOCASE"));
    if (!q.exec()) {
        return out;
    }
    while (q.next()) {
        AlbumInfo a;
        a.setId(getString(q, "id"));
        a.setName(getString(q, "name"));
        a.setArtist(getString(q, "artist"));
        a.setArtistId(getString(q, "artist_id"));
        a.setYear(getInt(q, "year"));
        a.setCoverPath(getString(q, "cover_path"));
        a.setSourceType(getString(q, "source_type"));
        a.setYouTubeAlbumId(getString(q, "you_tube_album_id"));
        a.setDescription(getString(q, "description"));
        a.setGenre(getString(q, "genre"));
        a.setIsSaved(getInt(q, "is_saved") != 0);
        a.setTrackCount(getInt(q, "track_count"));
        out.append(a);
    }
    return out;
}

QList<AlbumInfo> LibraryRepository::albumsForArtist(QString artistId) const {
    QList<AlbumInfo> out;
    QSqlQuery q(db_.connection());
    q.prepare(QStringLiteral("SELECT * FROM albums WHERE artist_id = ? ORDER BY year DESC"));
    q.addBindValue(artistId);
    if (!q.exec()) {
        return out;
    }
    while (q.next()) {
        AlbumInfo a;
        a.setId(getString(q, "id"));
        a.setName(getString(q, "name"));
        a.setArtist(getString(q, "artist"));
        a.setYear(getInt(q, "year"));
        a.setCoverPath(getString(q, "cover_path"));
        a.setIsSaved(getInt(q, "is_saved") != 0);
        out.append(a);
    }
    return out;
}

void LibraryRepository::upsertAlbum(const AlbumInfo& album) {
    QSqlQuery q(db_.connection());
    q.prepare(QStringLiteral(
        "INSERT INTO albums (id, name, artist, artist_id, year, cover_path, source_type,"
        "you_tube_album_id, description, genre, is_saved, last_browsed_at, track_count)"
        " VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)"
        " ON CONFLICT(id) DO UPDATE SET"
        " name=excluded.name, year=excluded.year, cover_path=excluded.cover_path,"
        " track_count=excluded.track_count, is_saved=excluded.is_saved"
    ));
    q.addBindValue(album.id().isEmpty() ? QVariant(QUuid::createUuid().toString(QUuid::WithoutBraces)) : QVariant(album.id()));
    q.addBindValue(album.name());
    q.addBindValue(album.artist());
    q.addBindValue(album.artistId());
    q.addBindValue(album.year());
    q.addBindValue(album.coverPath());
    q.addBindValue(album.sourceType());
    q.addBindValue(album.youTubeAlbumId());
    q.addBindValue(album.description());
    q.addBindValue(album.genre());
    q.addBindValue(album.isSaved() ? 1 : 0);
    q.addBindValue(QVariant());
    q.addBindValue(album.trackCount());
    if (!q.exec()) {
        qWarning() << "upsertAlbum failed:" << q.lastError().text();
    }
}

QList<PlaylistInfo> LibraryRepository::allPlaylists() const {
    QList<PlaylistInfo> out;
    QSqlQuery q(db_.connection());
    q.prepare(QStringLiteral("SELECT * FROM playlists ORDER BY name COLLATE NOCASE"));
    if (!q.exec()) {
        return out;
    }
    while (q.next()) {
        PlaylistInfo p;
        p.setId(getString(q, "id"));
        p.setName(getString(q, "name"));
        p.setCreatedAt(getDateTime(q, "created_at"));
        QVariant lmVar = q.value("last_modified_at");
        if (!lmVar.isNull()) {
            qint64 secs = lmVar.toLongLong();
            if (secs > 0) {
                p.setLastModifiedAt(QDateTime::fromSecsSinceEpoch(secs));
            }
        }
        p.setDescription(getString(q, "description"));
        p.setCoverPath(getString(q, "cover_path"));
        p.setYouTubePlaylistId(getString(q, "you_tube_playlist_id"));
        p.setSourceType(getString(q, "source_type"));
        p.setTrackCount(getInt(q, "track_count"));
        int secs = getInt(q, "total_duration_secs");
        p.setTotalDuration(std::chrono::seconds{ std::max(0, secs) });
        out.append(p);
    }
    return out;
}

void LibraryRepository::upsertPlaylist(const PlaylistInfo& playlist) {
    QSqlQuery q(db_.connection());
    q.prepare(QStringLiteral(
        "INSERT INTO playlists (id, name, created_at, last_modified_at, description,"
        "cover_path, you_tube_playlist_id, source_type, track_count, total_duration_secs)"
        " VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?)"
        " ON CONFLICT(id) DO UPDATE SET"
        " name=excluded.name, last_modified_at=excluded.last_modified_at,"
        " description=excluded.description, cover_path=excluded.cover_path,"
        " track_count=excluded.track_count, total_duration_secs=excluded.total_duration_secs"
    ));
    q.addBindValue(playlist.id().isEmpty() ? QVariant(QUuid::createUuid().toString(QUuid::WithoutBraces)) : QVariant(playlist.id()));
    q.addBindValue(playlist.name());
    q.addBindValue(playlist.createdAt().isValid() ? playlist.createdAt().toSecsSinceEpoch() : QVariant(QDateTime::currentSecsSinceEpoch()));
    q.addBindValue(playlist.lastModifiedAt().has_value() ? QVariant(playlist.lastModifiedAt()->toSecsSinceEpoch()) : QVariant());
    q.addBindValue(playlist.description());
    q.addBindValue(playlist.coverPath());
    q.addBindValue(playlist.youTubePlaylistId());
    q.addBindValue(playlist.sourceType());
    q.addBindValue(playlist.trackCount());
    q.addBindValue(static_cast<qint64>(playlist.totalDuration().count()));
    if (!q.exec()) {
        qWarning() << "upsertPlaylist failed:" << q.lastError().text();
    }
}

void LibraryRepository::deletePlaylist(QString id) {
    QSqlQuery q(db_.connection());
    q.prepare(QStringLiteral("DELETE FROM playlists WHERE id = ?"));
    q.addBindValue(id);
    if (!q.exec()) {
        qWarning() << "deletePlaylist failed:" << q.lastError().text();
    }
}

void LibraryRepository::addTrackToPlaylist(QString playlistId, QString trackId, int position) {
    QSqlQuery q(db_.connection());
    q.prepare(QStringLiteral("INSERT INTO playlist_tracks (playlist_id, position, track_id) VALUES (?, ?, ?)"));
    q.addBindValue(playlistId);
    q.addBindValue(position);
    q.addBindValue(trackId);
    if (!q.exec()) {
        qWarning() << "addTrackToPlaylist failed:" << q.lastError().text();
    }
}

void LibraryRepository::removeTrackFromPlaylist(QString playlistId, int position) {
    QSqlQuery q(db_.connection());
    q.prepare(QStringLiteral("DELETE FROM playlist_tracks WHERE playlist_id = ? AND position = ?"));
    q.addBindValue(playlistId);
    q.addBindValue(position);
    if (!q.exec()) {
        qWarning() << "removeTrackFromPlaylist failed:" << q.lastError().text();
    }
}

void LibraryRepository::recordSearch(QString query, QString sourceType, int resultCount) {
    QSqlQuery q(db_.connection());
    q.prepare(QStringLiteral(
        "INSERT INTO search_history (id, query, source_type, last_searched_at, result_count, click_count)"
        " VALUES (?, ?, ?, ?, ?, 0)"
        " ON CONFLICT(query, source_type) DO UPDATE SET"
        " last_searched_at=excluded.last_searched_at, result_count=excluded.result_count"
    ));
    q.addBindValue(QUuid::createUuid().toString(QUuid::WithoutBraces));
    q.addBindValue(query);
    q.addBindValue(sourceType);
    q.addBindValue(QDateTime::currentSecsSinceEpoch());
    q.addBindValue(resultCount);
    if (!q.exec()) {
        qWarning() << "recordSearch failed:" << q.lastError().text();
    }
}

QList<QString> LibraryRepository::recentSearchQueries(int limit) const {
    QList<QString> out;
    QSqlQuery q(db_.connection());
    q.prepare(QStringLiteral("SELECT DISTINCT query FROM search_history ORDER BY last_searched_at DESC LIMIT ?"));
    q.addBindValue(limit);
    if (!q.exec()) {
        return out;
    }
    while (q.next()) {
        out.append(q.value(0).toString());
    }
    return out;
}

void LibraryRepository::bumpSearchClickCount(QString query, QString sourceType) {
    QSqlQuery q(db_.connection());
    q.prepare(QStringLiteral(
        "UPDATE search_history SET click_count = click_count + 1"
        " WHERE query = ? AND source_type = ?"
    ));
    q.addBindValue(query);
    q.addBindValue(sourceType);
    if (!q.exec()) {
        qWarning() << "bumpSearchClickCount failed:" << q.lastError().text();
    }
}

void LibraryRepository::clearAllSearchHistory() {
    QSqlQuery q(db_.connection());
    if (!q.exec(QStringLiteral("DELETE FROM search_history"))) {
        qWarning() << "clearAllSearchHistory failed:" << q.lastError().text();
    }
}

void LibraryRepository::clearSearchHistoryForSource(QString sourceType) {
    QSqlQuery q(db_.connection());
    q.prepare(QStringLiteral("DELETE FROM search_history WHERE source_type = ?"));
    q.addBindValue(sourceType);
    if (!q.exec()) {
        qWarning() << "clearSearchHistoryForSource failed:" << q.lastError().text();
    }
}

QList<mf::core::models::SearchHistory> LibraryRepository::recentSearchHistory(int limit) const {
    QList<mf::core::models::SearchHistory> out;
    QSqlQuery q(db_.connection());
    q.prepare(QStringLiteral(
        "SELECT id, query, source_type, last_searched_at, result_count, click_count, is_suggestion"
        " FROM search_history"
        " ORDER BY last_searched_at DESC, rowid DESC"
        " LIMIT ?"
    ));
    q.addBindValue(limit);
    if (!q.exec()) {
        qWarning() << "recentSearchHistory failed:" << q.lastError().text();
        return out;
    }
    while (q.next()) {
        mf::core::models::SearchHistory h;
        h.setId(q.value(0).toString());
        h.setQuery(q.value(1).toString());
        h.setSourceType(q.value(2).toString());
        qint64 secs = q.value(3).toLongLong();
        if (secs > 0) {
            h.setLastSearchedAt(QDateTime::fromSecsSinceEpoch(secs));
        }
        h.setResultCount(q.value(4).toInt());
        h.setClickCount(q.value(5).toInt());
        h.setIsSuggestion(q.value(6).toInt() != 0);
        out.append(h);
    }
    return out;
}

QList<mf::core::models::SearchHistory> LibraryRepository::searchHistorySuggestions(
    QString prefix, int limit) const {
    QList<mf::core::models::SearchHistory> out;
    QSqlQuery q(db_.connection());
    q.prepare(QStringLiteral(
        "SELECT query, source_type, MAX(last_searched_at) AS last_at,"
        "       SUM(click_count) AS total_clicks"
        " FROM search_history"
        " WHERE query LIKE ? COLLATE NOCASE"
        " GROUP BY query"
        " ORDER BY total_clicks DESC, last_at DESC, query COLLATE NOCASE"
        " LIMIT ?"
    ));
    q.addBindValue(prefix + QStringLiteral("%"));
    q.addBindValue(limit);
    if (!q.exec()) {
        qWarning() << "searchHistorySuggestions failed:" << q.lastError().text();
        return out;
    }
    while (q.next()) {
        mf::core::models::SearchHistory h;
        h.setQuery(q.value(0).toString());
        h.setSourceType(q.value(1).toString());
        qint64 secs = q.value(2).toLongLong();
        if (secs > 0) {
            h.setLastSearchedAt(QDateTime::fromSecsSinceEpoch(secs));
        }
        h.setClickCount(q.value(3).toInt());
        h.setIsSuggestion(true);
        out.append(h);
    }
    return out;
}

void LibraryRepository::setAppState(QString key, QString value) {
    QSqlQuery q(db_.connection());
    q.prepare(QStringLiteral(
        "INSERT INTO app_state (key, value, updated_at) VALUES (?, ?, ?)"
        " ON CONFLICT(key) DO UPDATE SET value=excluded.value, updated_at=excluded.updated_at"
    ));
    q.addBindValue(key);
    q.addBindValue(value);
    q.addBindValue(QDateTime::currentSecsSinceEpoch());
    if (!q.exec()) {
        qWarning() << "setAppState failed:" << q.lastError().text();
    }
}

std::optional<QString> LibraryRepository::appState(QString key) const {
    QSqlQuery q(db_.connection());
    q.prepare(QStringLiteral("SELECT value FROM app_state WHERE key = ?"));
    q.addBindValue(key);
    if (!q.exec() || !q.next()) {
        return std::nullopt;
    }
    return q.value(0).toString();
}

} // namespace mf::core::database
// LyricsService.h
// Fetches synchronized lyrics (LRC) and plain lyrics from LRCLIB.
// Falls back to embedded lyrics in MusicFile if the API returns nothing.

#pragma once

#include <QObject>
#include <QString>

#include <functional>

namespace mf::core::models { class MusicFile; }
namespace mf::core::sources { class HttpClient; }

namespace mf::core::services {

class LyricsService : public QObject {
    Q_OBJECT
public:
    explicit LyricsService(mf::core::sources::HttpClient* http,
                           QObject* parent = nullptr);
    ~LyricsService() override = default;

    /// Fetch lyrics for a track. Calls back with plain text lyrics
    /// (synced LRC stripped to plain text for display).
    /// Returns empty string on miss/error.
    using LyricsCallback = std::function<void(QString lyrics)>;
    void fetchLyrics(const mf::core::models::MusicFile& track,
                     LyricsCallback callback);

    /// Quick synchronous lookup: embedded lyrics only (no network).
    QString embeddedLyrics(const mf::core::models::MusicFile& track) const;

signals:
    void lyricsFetched(const QString& title, const QString& lyrics);

private:
    /// Strip LRC timestamps (e.g. "[01:23.45]") from a synced lyrics string,
    /// returning the plain text.
    static QString stripTimestamps(const QString& syncedLyrics);

    mf::core::sources::HttpClient* http_ = nullptr;
};

} // namespace mf::core::services

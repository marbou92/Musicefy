// YouTubeUrlParser.h
// Static utility for detecting and parsing YouTube URLs.
// Supports video, playlist, artist (channel), and album (browse) URLs
// from both youtube.com and music.youtube.com domains.

#pragma once

#include <QString>

namespace mf::core::sources::youtube {

enum class UrlType { Video, Playlist, Artist, Album, Unknown };

struct ParsedUrl {
    UrlType type = UrlType::Unknown;
    QString videoId;      // set for Video type (11-char ID)
    QString playlistId;   // set for Playlist type
    QString browseId;     // set for Artist/Album types (UC... or MPRE...)
};

class YouTubeUrlParser {
public:
    /// Parse a URL string and return the detected type + extracted IDs.
    /// Returns UrlType::Unknown if the URL does not match any known pattern.
    static ParsedUrl parse(const QString& url);

    /// Returns true if the URL is a recognized YouTube URL.
    static bool isYouTubeUrl(const QString& url);

    /// Create a standard YouTube Music watch URL from a video ID.
    static QString createWatchUrl(const QString& videoId);

private:
    static bool matchVideoId(const QString& url, QString& out);
    static bool matchPlaylistId(const QString& url, QString& out);
    static bool matchArtistId(const QString& url, QString& out);
    static bool matchAlbumId(const QString& url, QString& out);
};

} // namespace mf::core::sources::youtube

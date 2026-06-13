// YouTubeUrlParser.cpp

#include "YouTubeUrlParser.h"

#include <QRegularExpression>

namespace mf::core::sources::youtube {

// ── Video ID patterns ─────────────────────────────────────────────
// All capture exactly 11-char video IDs: [a-zA-Z0-9_-]{11}
static const QRegularExpression kVideoPatterns[] = {
    // youtube.com/watch?...v=XX
    QRegularExpression(R"(youtube\.com/.*[?&]v=([a-zA-Z0-9_-]{11}))"),
    // youtu.be/XX
    QRegularExpression(R"(youtu\.be/([a-zA-Z0-9_-]{11}))"),
    // youtube.com/shorts/XX
    QRegularExpression(R"(youtube\.com/shorts/([a-zA-Z0-9_-]{11}))"),
    // youtube.com/embed/XX
    QRegularExpression(R"(youtube\.com/embed/([a-zA-Z0-9_-]{11}))"),
    // music.youtube.com/watch?...v=XX
    QRegularExpression(R"(music\.youtube\.com/.*[?&]v=([a-zA-Z0-9_-]{11}))"),
};

// ── Playlist ID patterns ──────────────────────────────────────────
static const QRegularExpression kPlaylistPatterns[] = {
    // youtube.com/playlist?...list=XX
    QRegularExpression(R"(youtube\.com/playlist\?.*[?&]list=([a-zA-Z0-9_-]+))"),
    // youtube.com/watch?...list=XX  (video URL with playlist parameter)
    QRegularExpression(R"(youtube\.com/.*[?&]list=([a-zA-Z0-9_-]+))"),
};

// ── Artist (channel) pattern ──────────────────────────────────────
// music.youtube.com/channel/UCXX
static const QRegularExpression kArtistPattern(
    R"(music\.youtube\.com/channel/(UC[a-zA-Z0-9_-]+))");

// ── Album (browse) pattern ────────────────────────────────────────
// music.youtube.com/browse/MPREXX
static const QRegularExpression kAlbumPattern(
    R"(music\.youtube\.com/browse/(MPRE[a-zA-Z0-9_-]+))");

// ──────────────────────────────────────────────────────────────────
ParsedUrl YouTubeUrlParser::parse(const QString& url)
{
    const QString trimmed = url.trimmed();
    ParsedUrl result;

    // Try artist/album first (music.youtube.com specific)
    if (matchArtistId(trimmed, result.browseId)) {
        result.type = UrlType::Artist;
        return result;
    }
    if (matchAlbumId(trimmed, result.browseId)) {
        result.type = UrlType::Album;
        return result;
    }

    // Try video patterns
    if (matchVideoId(trimmed, result.videoId)) {
        result.type = UrlType::Video;
        // Check if the URL also contains a playlist parameter
        for (const auto& pat : kPlaylistPatterns) {
            auto m = pat.match(trimmed);
            if (m.hasMatch()) {
                result.playlistId = m.captured(1);
                break;
            }
        }
        return result;
    }

    // Try playlist patterns
    if (matchPlaylistId(trimmed, result.playlistId)) {
        result.type = UrlType::Playlist;
        return result;
    }

    return result; // UrlType::Unknown
}

// ──────────────────────────────────────────────────────────────────
bool YouTubeUrlParser::isYouTubeUrl(const QString& url)
{
    return parse(url).type != UrlType::Unknown;
}

// ──────────────────────────────────────────────────────────────────
QString YouTubeUrlParser::createWatchUrl(const QString& videoId)
{
    return QStringLiteral("https://music.youtube.com/watch?v=%1").arg(videoId);
}

// ──────────────────────────────────────────────────────────────────
bool YouTubeUrlParser::matchVideoId(const QString& url, QString& out)
{
    for (const auto& pat : kVideoPatterns) {
        auto m = pat.match(url);
        if (m.hasMatch()) {
            out = m.captured(1);
            return true;
        }
    }
    return false;
}

// ──────────────────────────────────────────────────────────────────
bool YouTubeUrlParser::matchPlaylistId(const QString& url, QString& out)
{
    for (const auto& pat : kPlaylistPatterns) {
        auto m = pat.match(url);
        if (m.hasMatch()) {
            out = m.captured(1);
            return true;
        }
    }
    return false;
}

// ──────────────────────────────────────────────────────────────────
bool YouTubeUrlParser::matchArtistId(const QString& url, QString& out)
{
    auto m = kArtistPattern.match(url);
    if (m.hasMatch()) {
        out = m.captured(1);
        return true;
    }
    return false;
}

// ──────────────────────────────────────────────────────────────────
bool YouTubeUrlParser::matchAlbumId(const QString& url, QString& out)
{
    auto m = kAlbumPattern.match(url);
    if (m.hasMatch()) {
        out = m.captured(1);
        return true;
    }
    return false;
}

} // namespace mf::core::sources::youtube

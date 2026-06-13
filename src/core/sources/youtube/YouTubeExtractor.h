// YouTubeExtractor.h
// Picks the best audio format from a PlayerResponse and deobfuscates
// its signature cipher. Encapsulates the "given a player response,
// give me a stream URL" decision in a single testable place.
//
// The signature cipher operations (`sc` array) come from the player's
// JavaScript, not from the player response. The session is expected
// to have already parsed them before calling pickBestAudio. The
// DecipherFn callback is bound to those operations and only takes
// the per-format signature:
//
//   auto decipher = [&](const QString& sig) {
//       return Cipher::decipher(sig, operationsString_);
//   };
//   auto url = YouTubeExtractor::pickBestAudio(player, decipher);
//
// This indirection lets us swap in a yt-dlp fallback (Block 5.2.C)
// without touching the rest of the pipeline.

#pragma once

#include "PlayerResponse.h"

#include <QJsonArray>
#include <QString>

#include <functional>

namespace mf::core::sources::youtube {

// Thumbnail size categories matching YouTube's CDN scaling.
enum class ThumbnailSize {
    Small  = 96,   // List view (96×96)
    Medium = 320,  // Grid view (320×320)
    Large  = 480,  // Hero / detail (480×480)
    Full   = 1280  // Full resolution (1280×720)
};

class YouTubeExtractor {
public:
    // Deobfuscate a per-format signature. The operations string is
    // already known to the caller (the session). Returns the
    // deciphered signature; empty string on failure (the caller
    // should treat that as "skip this format").
    using DecipherFn = std::function<QString(const QString& sig)>;

    // Pick the best audio-only format from `response` and return its
    // fully-resolved stream URL (deciphered signature appended).
    //
    // Selection policy:
    //   1. AudioOnly formats first.
    //   2. Progressive (audio + video) as fallback.
    //   3. Among AudioOnly: prefer higher bitrate, then higher sample
    //      rate, then lower itag (smaller files).
    //
    // If no audio format is playable, returns an empty string and
    // sets `errorOut` to a human-readable reason.
    static QString pickBestAudio(const PlayerResponse& response,
                                 DecipherFn decipher,
                                 QString* errorOut = nullptr);

    // Build a final stream URL by appending `sig=<deciphered>` (or
    // whatever the player's `sp` was) to the format's base URL.
    static QString buildUrl(const PlayerFormat& fmt, const QString& decipheredSig);

    // Pick the best thumbnail URL from a YouTube thumbnails array.
    // YouTube provides thumbnails at multiple resolutions; this
    // function selects the one closest to the requested size without
    // going under. Falls back to the largest available if the
    // requested size exceeds all entries.
    //
    // The thumbnails array is the JSON "thumbnails" field from a
    // PlayerResponse or search result. Each entry has "url", "width",
    // and "height" fields.
    static QString bestThumbnailUrl(const QJsonArray& thumbnails,
                                    ThumbnailSize size = ThumbnailSize::Medium);
};

} // namespace mf::core::sources::youtube

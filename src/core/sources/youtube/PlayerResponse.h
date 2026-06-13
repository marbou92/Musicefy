// PlayerResponse.h
// Lightweight DTO for the InnerTube `/player` response. The full
// response is ~100 KB; we extract only the streaming fields we need
// (title, author, lengthSeconds, thumbnails, streamingData.formats[],
// streamingData.adaptiveFormats[]). The streamingData is kept as a
// raw QJsonValue so we don't have to keep up with every new field
// YouTube adds (microformat, captions, storyboards, …).
//
// Pure header / QObject, no GUI dependencies.

#pragma once

#include <QJsonArray>
#include <QJsonObject>
#include <QList>
#include <QString>

namespace mf::core::sources::youtube {

// One streaming format entry (progressive or adaptive).
struct PlayerFormat {
    enum class Kind {
        Unknown,
        AudioOnly,   // itag in {140, 141, 171, 249, 250, 251, 139}
        VideoOnly,   // itag >= 18 with video stream but no audio
        Progressive, // both audio and video (typically 18)
    };

    int     itag = 0;
    QString url;          // populated when format is unciphered
    QString signature;    // raw `s` query param (when ciphered)
    QString signatureParam; // raw `sp` query param (typically "sig")
    QString mimeType;     // "audio/webm; codecs=\"opus\""
    int     bitrate = 0;
    int     contentLength = 0;
    int     audioSampleRate = 0;
    QString qualityLabel; // e.g. "medium" or "720p"
    int     width = 0;
    int     height = 0;
    int     fps = 0;
    Kind    kind = Kind::Unknown;

    bool isCiphered() const { return url.isEmpty() && !signature.isEmpty(); }
};

struct PlayerResponse {
    enum class PlayabilityStatus {
        OK,
        Error,
        LoginRequired,
        Unplayable,
    };

    QString  videoId;
    QString  title;
    QString  author;          // channel name
    QString  channelId;       // UCxxx
    qint64   lengthSeconds = 0;
    QList<PlayerFormat> formats;       // combined: streamingData.formats + adaptiveFormats
    QString  hlsManifestUrl; // non-empty if HLS-only playback
    QString  dashManifestUrl;
    QString  playerJsUrl;    // URL of the `base.js` player script. Empty if
                             // YouTube did not return the field (older
                             // player builds, or an error response).
    QString  errorMessage;
    PlayabilityStatus status = PlayabilityStatus::OK;

    bool isPlayable() const { return status == PlayabilityStatus::OK && !formats.isEmpty(); }
    bool hasPlayerJsUrl() const { return !playerJsUrl.isEmpty(); }
};

// Parser. Returns a PlayerResponse with isPlayable() reflecting the
// playabilityStatus. The full response is also kept in `raw()` for
// callers that want to look up additional fields (e.g. captions
// tracks) without us having to mirror every field.
class PlayerResponseParser {
public:
    static PlayerResponse parse(const QJsonObject& root);

    // Classify a format's `itag` into Kind. We only need to recognise
    // the audio-only itags (the rest fall back to Unknown / progressive
    // depending on whether a video stream is present).
    static PlayerFormat::Kind classifyItag(int itag, const QString& mimeType);
};

} // namespace mf::core::sources::youtube

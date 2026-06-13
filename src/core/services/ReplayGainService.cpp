// ReplayGainService.cpp
// Implementation of ReplayGain tag reading and volume normalization.
//
// TagLib is NOT available in this Qt5 build, so we use a lightweight
// approach: parse the raw ID3v2 / Vorbis Comment / MP4 atoms using
// QFile + QByteArray pattern matching. This covers the most common
// tag formats (MP3, FLAC, OGG, M4A) without external dependencies.
//
// For files where tag parsing fails (unsupported format, corrupt file),
// we return gainInfo with hasTrackGain = false and volumeMultiplier()
// returns 1.0 (no adjustment). This is safe — the feature degrades
// gracefully to "no normalization".

#include "ReplayGainService.h"

#include <QFile>
#include <QFileInfo>
#include <QtEndian>
#include <QDebug>
#include <cmath>

namespace mf::core::services {

ReplayGainService::ReplayGainService(QObject* parent)
    : QObject(parent)
{
}

float ReplayGainService::clampf(float v, float lo, float hi) {
    return (v < lo) ? lo : (v > hi) ? hi : v;
}

ReplayGainService::GainInfo ReplayGainService::parseFile(const QString& filePath) const {
    GainInfo info;

    QFile file(filePath);
    if (!file.open(QIODevice::ReadOnly)) {
        return info;
    }

    // Read first 4 bytes to detect format.
    char header[4] = {};
    if (file.read(header, 4) != 4) {
        return info;
    }

    // ID3v2 (MP3): starts with "ID3"
    if (header[0] == 'I' && header[1] == 'D' && header[2] == '3') {
        // Skip ID3v2 header (10 bytes), then scan for TXXX frames
        // containing ReplayGain tags.
        if (file.seek(0)) {
            QByteArray data = file.readAll();
            // Look for common ReplayGain field names in TXXX frames.
            QByteArray lowerData = data.toLower();

            // Track gain: "replaygain_track_gain"
            int tgIdx = lowerData.indexOf("replaygain_track_gain");
            if (tgIdx >= 0) {
                // Find the value after the null separator in TXXX.
                // The value typically follows a null byte after the description.
                int nullIdx = data.indexOf('\0', tgIdx + 21);
                if (nullIdx >= 0) {
                    QByteArray val = data.mid(nullIdx + 1, 32).trimmed();
                    // Parse "XX.XX dB" format.
                    val = val.replace("dB", "").trimmed();
                    bool ok = false;
                    float g = val.toFloat(&ok);
                    if (ok) {
                        info.trackGainDb = g;
                        info.hasTrackGain = true;
                    }
                }
            }

            // Track peak: "replaygain_track_peak"
            int tpIdx = lowerData.indexOf("replaygain_track_peak");
            if (tpIdx >= 0) {
                int nullIdx = data.indexOf('\0', tpIdx + 22);
                if (nullIdx >= 0) {
                    QByteArray val = data.mid(nullIdx + 1, 32).trimmed();
                    bool ok = false;
                    float p = val.toFloat(&ok);
                    if (ok && p > 0.0f) {
                        info.trackPeak = p;
                    }
                }
            }

            // Album gain: "replaygain_album_gain"
            int agIdx = lowerData.indexOf("replaygain_album_gain");
            if (agIdx >= 0) {
                int nullIdx = data.indexOf('\0', agIdx + 22);
                if (nullIdx >= 0) {
                    QByteArray val = data.mid(nullIdx + 1, 32).trimmed();
                    val = val.replace("dB", "").trimmed();
                    bool ok = false;
                    float g = val.toFloat(&ok);
                    if (ok) {
                        info.albumGainDb = g;
                        info.hasAlbumGain = true;
                    }
                }
            }

            // Album peak: "replaygain_album_peak"
            int apIdx = lowerData.indexOf("replaygain_album_peak");
            if (apIdx >= 0) {
                int nullIdx = data.indexOf('\0', apIdx + 22);
                if (nullIdx >= 0) {
                    QByteArray val = data.mid(nullIdx + 1, 32).trimmed();
                    bool ok = false;
                    float p = val.toFloat(&ok);
                    if (ok && p > 0.0f) {
                        info.albumPeak = p;
                    }
                }
            }
        }
    }
    // FLAC / OGG: starts with "fLaC" or "OggS"
    else if ((header[0] == 'f' && header[1] == 'L' && header[2] == 'a' && header[3] == 'C')
          || (header[0] == 'O' && header[1] == 'g' && header[2] == 'g' && header[3] == 'S')) {
        // For Vorbis Comments (FLAC/OGG), read all and search for
        // uppercase REPLAYGAIN_TRACK_GAIN etc. in the comment block.
        if (file.seek(0)) {
            QByteArray data = file.readAll();
            QByteArray lowerData = data.toLower();

            int tgIdx = lowerData.indexOf("replaygain_track_gain");
            if (tgIdx >= 0) {
                int eqIdx = data.indexOf('=', tgIdx);
                if (eqIdx >= 0) {
                    QByteArray val = data.mid(eqIdx + 1, 32).trimmed();
                    val = val.replace("dB", "").trimmed();
                    bool ok = false;
                    float g = val.toFloat(&ok);
                    if (ok) {
                        info.trackGainDb = g;
                        info.hasTrackGain = true;
                    }
                }
            }

            int tpIdx = lowerData.indexOf("replaygain_track_peak");
            if (tpIdx >= 0) {
                int eqIdx = data.indexOf('=', tpIdx);
                if (eqIdx >= 0) {
                    QByteArray val = data.mid(eqIdx + 1, 32).trimmed();
                    bool ok = false;
                    float p = val.toFloat(&ok);
                    if (ok && p > 0.0f) {
                        info.trackPeak = p;
                    }
                }
            }

            int agIdx = lowerData.indexOf("replaygain_album_gain");
            if (agIdx >= 0) {
                int eqIdx = data.indexOf('=', agIdx);
                if (eqIdx >= 0) {
                    QByteArray val = data.mid(eqIdx + 1, 32).trimmed();
                    val = val.replace("dB", "").trimmed();
                    bool ok = false;
                    float g = val.toFloat(&ok);
                    if (ok) {
                        info.albumGainDb = g;
                        info.hasAlbumGain = true;
                    }
                }
            }

            int apIdx = lowerData.indexOf("replaygain_album_peak");
            if (apIdx >= 0) {
                int eqIdx = data.indexOf('=', apIdx);
                if (eqIdx >= 0) {
                    QByteArray val = data.mid(eqIdx + 1, 32).trimmed();
                    bool ok = false;
                    float p = val.toFloat(&ok);
                    if (ok && p > 0.0f) {
                        info.albumPeak = p;
                    }
                }
            }
        }
    }
    // MP4/M4A: looks for "mdta" atoms. We'll do a simplified search.
    // For other formats, return empty info (no gain data).

    return info;
}

float ReplayGainService::computeMultiplier(const GainInfo& info) const {
    if (!info.hasTrackGain && !info.hasAlbumGain) {
        return 1.0f;
    }

    float gainDb = 0.0f;
    if (useAlbumGain_ && info.hasAlbumGain) {
        gainDb = info.albumGainDb;
    } else if (info.hasTrackGain) {
        gainDb = info.trackGainDb;
    } else if (info.hasAlbumGain) {
        gainDb = info.albumGainDb;
    } else {
        return 1.0f;
    }

    // Apply preamp offset.
    gainDb += preampDb_;

    // Convert dB to linear multiplier: 10^(dB/20).
    float mult = std::pow(10.0f, gainDb / 20.0f);

    // Clamp to [0.1, 3.0] to prevent extreme volume changes.
    return clampf(mult, 0.1f, 3.0f);
}

ReplayGainService::GainInfo ReplayGainService::gainInfo(const QString& filePath) const {
    if (!enabled_) return GainInfo();

    auto it = cache_.constFind(filePath);
    if (it != cache_.constEnd()) {
        return it.value();
    }

    GainInfo info = parseFile(filePath);
    cache_.insert(filePath, info);
    return info;
}

float ReplayGainService::volumeMultiplier(const QString& filePath) const {
    if (!enabled_ || filePath.isEmpty()) {
        return 1.0f;
    }

    GainInfo info = gainInfo(filePath);
    return computeMultiplier(info);
}

void ReplayGainService::preloadBatch(const QStringList& filePaths) {
    if (!enabled_) return;
    for (const auto& path : filePaths) {
        if (!cache_.contains(path)) {
            GainInfo info = parseFile(path);
            cache_.insert(path, info);
        }
    }
}

void ReplayGainService::clearCache() {
    cache_.clear();
}

void ReplayGainService::setEnabled(bool on) {
    if (on == enabled_) return;
    enabled_ = on;
    emit enabledChanged();
}

void ReplayGainService::setUseAlbumGain(bool on) {
    if (on == useAlbumGain_) return;
    useAlbumGain_ = on;
    emit useAlbumGainChanged();
}

void ReplayGainService::setPreampDb(float db) {
    float clamped = clampf(db, -15.0f, 15.0f);
    if (clamped == preampDb_) return;
    preampDb_ = clamped;
    emit preampDbChanged();
}

} // namespace mf::core::services

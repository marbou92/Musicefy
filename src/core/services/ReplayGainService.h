// ReplayGainService.h
// Reads ReplayGain tags from audio files via TagLib and provides
// gain/peak values for volume normalization. The service exposes
// a simple API: give it a file path, get back the recommended
// volume multiplier (0.0–2.0 range, 1.0 = no change).
//
// ReplayGain tags checked (in priority order):
//   1. track gain / track peak
//   2. album gain / album peak
//   3. track gain only (fallback)
//
// The preamp offset is configurable (default +6 dB) and stored
// in QSettings under "playback/replaygain_preamp_db".
//
// Qt5 / MSVC compatible (v142 toolset, /WX).

#pragma once

#include <QObject>
#include <QString>
#include <QHash>
#include <QColor>

namespace mf::core::services {

class ReplayGainService : public QObject {
    Q_OBJECT
    Q_PROPERTY(bool   enabled    READ isEnabled   WRITE setEnabled   NOTIFY enabledChanged)
    Q_PROPERTY(bool   useAlbumGain READ useAlbumGain WRITE setUseAlbumGain NOTIFY useAlbumGainChanged)
    Q_PROPERTY(float  preampDb   READ preampDb     WRITE setPreampDb  NOTIFY preampDbChanged)
public:
    explicit ReplayGainService(QObject* parent = nullptr);
    ~ReplayGainService() override = default;

    // Core API: returns the volume multiplier for a given file.
    // 1.0 = no adjustment, <1.0 = reduce volume, >1.0 = boost.
    // Returns 1.0 if tags are missing or service is disabled.
    float volumeMultiplier(const QString& filePath) const;

    // Retrieve raw tag values (for display in Now Playing / Settings).
    struct GainInfo {
        float trackGainDb = 0.0f;
        float trackPeak   = 1.0f;
        float albumGainDb = 0.0f;
        float albumPeak   = 1.0f;
        bool  hasTrackGain = false;
        bool  hasAlbumGain = false;
    };
    GainInfo gainInfo(const QString& filePath) const;

    bool   isEnabled()     const { return enabled_; }
    bool   useAlbumGain()  const { return useAlbumGain_; }
    float  preampDb()      const { return preampDb_; }

    // Pre-compute gain for a batch of files (e.g., when loading queue).
    Q_INVOKABLE void preloadBatch(const QStringList& filePaths);

    // Clear the cache (e.g., on library rescan).
    Q_INVOKABLE void clearCache();

public slots:
    void setEnabled(bool on);
    void setUseAlbumGain(bool on);
    void setPreampDb(float db);

signals:
    void enabledChanged();
    void useAlbumGainChanged();
    void preampDbChanged();

private:
    // Parse ReplayGain tags from file using TagLib.
    GainInfo parseFile(const QString& filePath) const;

    // Compute the volume multiplier from raw gain/peak values.
    float computeMultiplier(const GainInfo& info) const;

    // Clamp helper.
    static float clampf(float v, float lo, float hi);

    bool   enabled_       = true;
    bool   useAlbumGain_  = false;
    float  preampDb_      = 6.0f;
    float  referenceLoudness_ = 89.0f; // ReplayGain reference (dB SPL)

    mutable QHash<QString, GainInfo> cache_;
};

} // namespace mf::core::services

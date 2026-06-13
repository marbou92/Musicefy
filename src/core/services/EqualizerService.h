// EqualizerService.h
// Preset-based audio equalizer. Qt5's QMediaPlayer does not expose a
// hardware equalizer, so this service provides:
//   1. Named presets (Flat, Bass Boost, Treble Boost, Vocal, etc.)
//   2. Per-band gain values (10 bands: 31Hz–16kHz)
//   3. A preamp multiplier that PlayerViewModel applies to volume
//   4. Custom preset save/load via QSettings
//
// Band layout (10 bands, ISO standard):
//   31, 62, 125, 250, 500, 1k, 2k, 4k, 8k, 16k (Hz)
//
// Each band value is in dB: -12.0 to +12.0 (0.0 = flat).
// The preamp is also in dB and is converted to a linear volume
// multiplier by the caller (same formula as ReplayGainService).
//
// Qt5 / MSVC v142 compatible, /WX clean.

#pragma once

#include <QObject>
#include <QString>
#include <QStringList>
#include <QHash>
#include <QVariantList>

namespace mf::core::services {

class EqualizerService : public QObject {
    Q_OBJECT
    Q_PROPERTY(bool     enabled       READ isEnabled      WRITE setEnabled      NOTIFY enabledChanged)
    Q_PROPERTY(QString  presetName    READ presetName     WRITE setPreset       NOTIFY presetChanged)
    Q_PROPERTY(float    preampDb      READ preampDb       WRITE setPreampDb     NOTIFY preampChanged)
    Q_PROPERTY(QVariantList bandValues READ bandValues     NOTIFY bandValuesChanged)
    Q_PROPERTY(QStringList  presetNames READ presetNames   NOTIFY presetsChanged)
public:
    static constexpr int kBandCount = 10;
    static constexpr float kMinDb = -12.0f;
    static constexpr float kMaxDb = 12.0f;
    static constexpr float kMinPreamp = -12.0f;
    static constexpr float kMaxPreamp = 12.0f;

    explicit EqualizerService(QObject* parent = nullptr);
    ~EqualizerService() override = default;

    bool    isEnabled()  const { return enabled_; }
    QString presetName() const { return currentPreset_; }
    float   preampDb()   const { return preampDb_; }

    /// Band index (0–9) → gain in dB.
    float bandGain(int index) const;
    /// Get all band gains as a QVariantList (for QML binding).
    QVariantList bandValues() const;

    /// Get the preamp as a linear volume multiplier (10^(preampDb/20)).
    Q_INVOKABLE float preampMultiplier() const;

    /// Get all available preset names.
    QStringList presetNames() const;

    /// Check if a named preset exists.
    Q_INVOKABLE bool hasPreset(const QString& name) const;

    /// Get band values for a specific preset.
    Q_INVOKABLE QVariantList presetBands(const QString& name) const;

public slots:
    void setEnabled(bool on);
    void setPreset(const QString& name);
    void setBandGain(int index, float db);
    void setPreampDb(float db);
    void saveCustomPreset(const QString& name);
    void deleteCustomPreset(const QString& name);
    void resetToFlat();

signals:
    void enabledChanged();
    void presetChanged();
    void preampChanged();
    void bandValuesChanged();
    void presetsChanged();

private:
    struct Preset {
        QString name;
        float bands[kBandCount];
        float preampDb;
    };

    void loadBuiltInPresets();
    void loadCustomPresets();
    void saveCustomPresets() const;
    void applyPreset(const Preset& p);
    static float clampf(float v, float lo, float hi);

    bool    enabled_ = false;
    QString currentPreset_;
    float   preampDb_ = 0.0f;
    float   bands_[kBandCount] = {};

    QList<Preset> builtInPresets_;
    QList<Preset> customPresets_;
};

} // namespace mf::core::services

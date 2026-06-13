// EqualizerService.cpp
// See header. Provides built-in presets and custom preset persistence.

#include "EqualizerService.h"

#include <QSettings>
#include <QJsonDocument>
#include <QJsonObject>
#include <QJsonArray>
#include <QDebug>
#include <cmath>

namespace mf::core::services {

float EqualizerService::clampf(float v, float lo, float hi) {
    return (v < lo) ? lo : (v > hi) ? hi : v;
}

EqualizerService::EqualizerService(QObject* parent)
    : QObject(parent)
{
    loadBuiltInPresets();
    loadCustomPresets();
    // Start with Flat.
    resetToFlat();
}

void EqualizerService::loadBuiltInPresets() {
    // Each preset: { name, 10 band gains (dB), preamp (dB) }
    auto addPreset = [this](const QString& name,
                            float b0, float b1, float b2, float b3, float b4,
                            float b5, float b6, float b7, float b8, float b9,
                            float preamp) {
        Preset p;
        p.name = name;
        p.bands[0] = b0;  p.bands[1] = b1;  p.bands[2] = b2;
        p.bands[3] = b3;  p.bands[4] = b4;  p.bands[5] = b5;
        p.bands[6] = b6;  p.bands[7] = b7;  p.bands[8] = b8;
        p.bands[9] = b9;
        p.preampDb = preamp;
        builtInPresets_.append(p);
    };

    //          31   62  125  250  500   1k   2k   4k   8k  16k  pre
    addPreset("Flat",        0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0);
    addPreset("Bass Boost",  8,  6,  5,  3,  1,  0,  0,  0,  0,  0,  0);
    addPreset("Bass Cut",   -8, -6, -5, -3, -1,  0,  0,  0,  0,  0,  0);
    addPreset("Treble Boost", 0, 0,  0,  0,  0,  0,  1,  3,  5,  7,  0);
    addPreset("Treble Cut",  0,  0,  0,  0,  0,  0, -1, -3, -5, -7,  0);
    addPreset("Vocal",      -2, -1,  0,  3,  5,  5,  3,  0, -1, -2,  0);
    addPreset("Rock",        6,  4, -2, -4, -2,  2,  4,  5,  5,  4,  0);
    addPreset("Pop",        -1,  2,  4,  5,  3,  0, -1, -1, -1, -1,  0);
    addPreset("Jazz",        4,  3,  1,  2, -1, -1,  0,  1,  3,  4,  0);
    addPreset("Classical",   5,  4,  3,  2, -1, -1,  0,  2,  3,  4,  0);
    addPreset("Electronic",  6,  5,  1,  0, -2,  2,  0,  1,  5,  6,  0);
    addPreset("Hip-Hop",     6,  5,  3,  0, -1, -1,  2,  0,  1,  3,  0);
    addPreset("Loudness",    6,  4,  0,  0, -2,  0, -1,  0,  5,  1,  3);
    addPreset("Night Mode", -3, -1,  0,  2,  4,  4,  2,  0, -1, -3,  0);
}

void EqualizerService::loadCustomPresets() {
    QSettings s;
    QByteArray data = s.value(QStringLiteral("equalizer/custom_presets")).toByteArray();
    if (data.isEmpty()) return;

    QJsonDocument doc = QJsonDocument::fromJson(data);
    if (!doc.isArray()) return;

    QJsonArray arr = doc.array();
    for (const auto& val : arr) {
        QJsonObject obj = val.toObject();
        Preset p;
        p.name = obj[QStringLiteral("name")].toString();
        if (p.name.isEmpty()) continue;
        QJsonArray bands = obj[QStringLiteral("bands")].toArray();
        for (int i = 0; i < kBandCount && i < bands.size(); ++i) {
            p.bands[i] = static_cast<float>(bands[i].toDouble());
        }
        p.preampDb = static_cast<float>(obj[QStringLiteral("preamp")].toDouble());
        customPresets_.append(p);
    }
}

void EqualizerService::saveCustomPresets() const {
    QJsonArray arr;
    for (const auto& p : customPresets_) {
        QJsonObject obj;
        obj[QStringLiteral("name")] = p.name;
        QJsonArray bands;
        for (int i = 0; i < kBandCount; ++i) {
            bands.append(static_cast<double>(p.bands[i]));
        }
        obj[QStringLiteral("bands")] = bands;
        obj[QStringLiteral("preamp")] = static_cast<double>(p.preampDb);
        arr.append(obj);
    }
    QSettings s;
    s.setValue(QStringLiteral("equalizer/custom_presets"),
              QJsonDocument(arr).toJson(QJsonDocument::Compact));
}

void EqualizerService::applyPreset(const Preset& p) {
    currentPreset_ = p.name;
    preampDb_ = p.preampDb;
    for (int i = 0; i < kBandCount; ++i) {
        bands_[i] = p.bands[i];
    }
    emit presetChanged();
    emit preampChanged();
    emit bandValuesChanged();
}

float EqualizerService::bandGain(int index) const {
    if (index < 0 || index >= kBandCount) return 0.0f;
    return bands_[index];
}

QVariantList EqualizerService::bandValues() const {
    QVariantList list;
    list.reserve(kBandCount);
    for (int i = 0; i < kBandCount; ++i) {
        list.append(static_cast<double>(bands_[i]));
    }
    return list;
}

float EqualizerService::preampMultiplier() const {
    if (!enabled_) return 1.0f;
    return std::pow(10.0f, preampDb_ / 20.0f);
}

QStringList EqualizerService::presetNames() const {
    QStringList names;
    for (const auto& p : builtInPresets_) {
        names.append(p.name);
    }
    for (const auto& p : customPresets_) {
        names.append(p.name);
    }
    return names;
}

bool EqualizerService::hasPreset(const QString& name) const {
    for (const auto& p : builtInPresets_) {
        if (p.name == name) return true;
    }
    for (const auto& p : customPresets_) {
        if (p.name == name) return true;
    }
    return false;
}

QVariantList EqualizerService::presetBands(const QString& name) const {
    // Search built-in first, then custom.
    for (const auto& p : builtInPresets_) {
        if (p.name == name) {
            QVariantList list;
            for (int i = 0; i < kBandCount; ++i) {
                list.append(static_cast<double>(p.bands[i]));
            }
            return list;
        }
    }
    for (const auto& p : customPresets_) {
        if (p.name == name) {
            QVariantList list;
            for (int i = 0; i < kBandCount; ++i) {
                list.append(static_cast<double>(p.bands[i]));
            }
            return list;
        }
    }
    return QVariantList();
}

void EqualizerService::setEnabled(bool on) {
    if (on == enabled_) return;
    enabled_ = on;
    emit enabledChanged();
}

void EqualizerService::setPreset(const QString& name) {
    // Search built-in first.
    for (const auto& p : builtInPresets_) {
        if (p.name == name) {
            applyPreset(p);
            return;
        }
    }
    // Then custom.
    for (const auto& p : customPresets_) {
        if (p.name == name) {
            applyPreset(p);
            return;
        }
    }
    qDebug() << "EqualizerService: unknown preset" << name;
}

void EqualizerService::setBandGain(int index, float db) {
    if (index < 0 || index >= kBandCount) return;
    float clamped = clampf(db, kMinDb, kMaxDb);
    if (clamped == bands_[index]) return;
    bands_[index] = clamped;
    // Switch to "Custom" preset name if currently on a built-in.
    currentPreset_ = QStringLiteral("Custom");
    emit presetChanged();
    emit bandValuesChanged();
}

void EqualizerService::setPreampDb(float db) {
    float clamped = clampf(db, kMinPreamp, kMaxPreamp);
    if (clamped == preampDb_) return;
    preampDb_ = clamped;
    emit preampChanged();
}

void EqualizerService::saveCustomPreset(const QString& name) {
    if (name.isEmpty()) return;
    // Remove existing with same name.
    for (int i = customPresets_.size() - 1; i >= 0; --i) {
        if (customPresets_[i].name == name) {
            customPresets_.removeAt(i);
        }
    }
    Preset p;
    p.name = name;
    for (int i = 0; i < kBandCount; ++i) {
        p.bands[i] = bands_[i];
    }
    p.preampDb = preampDb_;
    customPresets_.append(p);
    currentPreset_ = name;
    saveCustomPresets();
    emit presetChanged();
    emit presetsChanged();
}

void EqualizerService::deleteCustomPreset(const QString& name) {
    for (int i = customPresets_.size() - 1; i >= 0; --i) {
        if (customPresets_[i].name == name) {
            customPresets_.removeAt(i);
            saveCustomPresets();
            emit presetsChanged();
            return;
        }
    }
}

void EqualizerService::resetToFlat() {
    if (!builtInPresets_.isEmpty()) {
        applyPreset(builtInPresets_.first());
    }
}

} // namespace mf::core::services

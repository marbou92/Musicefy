// SettingsControl.h
// QSettings-backed implementation of ISettingsControl. Settings are
// persisted per-user in the standard Windows location (registry under
// HKCU\Software\Musicefy) for native look-and-feel.

#pragma once

#include "../interfaces/ISettingsControl.h"

#include <QObject>
#include <QSettings>

namespace mf::core::services {

class SettingsControl : public QObject, public mf::core::interfaces::ISettingsControl {
    Q_OBJECT
public:
    explicit SettingsControl(QObject* parent = nullptr);
    explicit SettingsControl(QString organization, QString application, QObject* parent = nullptr);
    ~SettingsControl() override;

    QVariant get(QString key) const override;
    void     set(QString key, QVariant value) override;
    bool     contains(QString key) const override;
    void     remove(QString key) override;
    void     sync() override;

signals:
    // Emitted after a successful set(). The SettingsPage listens
    // to this to surface a "Saved" toast on each persisted change.
    void settingChanged(QString key, QVariant value);

private:
    QSettings settings_;
};

} // namespace mf::core::services

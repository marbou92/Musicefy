// SettingsControl.cpp
// QSettings-backed key/value store. The org/app names default to
// "Musicefy" / "Musicefy" but can be overridden in the constructor for
// tests or alternate builds.

#include "SettingsControl.h"

namespace mf::core::services {

using mf::core::interfaces::ISettingsControl;

SettingsControl::SettingsControl(QObject* parent)
    : QObject(parent)
    , settings_(QSettings::IniFormat,
                QSettings::UserScope,
                QStringLiteral("Musicefy"),
                QStringLiteral("Musicefy"))
{
}

SettingsControl::SettingsControl(QString organization, QString application, QObject* parent)
    : QObject(parent)
    , settings_(QSettings::IniFormat, QSettings::UserScope, std::move(organization), std::move(application))
{
}

SettingsControl::~SettingsControl() = default;

QVariant SettingsControl::get(QString key) const {
    return settings_.value(key);
}

void SettingsControl::set(QString key, QVariant value) {
    settings_.setValue(key, value);
    emit settingChanged(key, value);
}

bool SettingsControl::contains(QString key) const {
    return settings_.contains(key);
}

void SettingsControl::remove(QString key) {
    settings_.remove(key);
}

void SettingsControl::sync() {
    settings_.sync();
}

} // namespace mf::core::services

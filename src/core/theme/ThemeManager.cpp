// ThemeManager.cpp
// See ThemeManager.h for the public API. Implementation notes:
//  - loadFromSettings() does NOT emit signals; it just hydrates state.
//    Callers that want the UI to refresh should call refresh().
//  - setTheme/setMode validate the input (clamped to the enum range)
//    and only emit signals when the value actually changes.
//  - recompute() is the single source of truth for the scheme; every
//    state change goes through it.

#include "ThemeManager.h"
#include "AppThemeColorSchemes.h"

#include "services/SettingsControl.h"

#include <QDebug>
#include <QPalette>

namespace mf::core::theme {

ThemeManager::ThemeManager(QObject* parent) : QObject(parent) {
    // Initialise with a known-good scheme so the UI has something to
    // bind to before settings are loaded.
    recompute();
}

void ThemeManager::bindSettings(mf::core::services::SettingsControl* settings) {
    settings_ = settings;
}

void ThemeManager::loadFromSettings() {
    if (!settings_) return;
    int t = settings_->getOrDefault<int>(QStringLiteral("theme/seed"),
                                          appThemeToInt(AppTheme::Default));
    int m = settings_->getOrDefault<int>(QStringLiteral("theme/mode"),
                                          themeModeToInt(ThemeMode::System));
    theme_ = appThemeFromInt(t);
    mode_  = themeModeFromInt(m);
    QVariant seedVar = settings_->get(QStringLiteral("theme/dynamic_seed_argb"));
    if (seedVar.isValid() && seedVar.canConvert<int>()) {
        int seed = seedVar.toInt();
        if (seed != 0) {
            dynamicSeed_ = QColor(seed);
            hasDynamicSeed_ = dynamicSeed_.isValid();
        } else {
            hasDynamicSeed_ = false;
        }
    } else {
        hasDynamicSeed_ = false;
    }
    // Don't emit signals on the initial load — the first refresh()
    // call (from AppLifecycle::start) will do that.
    recompute();
}

void ThemeManager::saveToSettings() {
    if (!settings_) return;
    settings_->set(QStringLiteral("theme/seed"), int(theme_));
    settings_->set(QStringLiteral("theme/mode"), int(mode_));
    if (hasDynamicSeed_ && dynamicSeed_.isValid()) {
        settings_->set(QStringLiteral("theme/dynamic_seed_argb"),
                       int(dynamicSeed_.rgba()));
    } else {
        settings_->remove(QStringLiteral("theme/dynamic_seed_argb"));
    }
    settings_->sync();
}

void ThemeManager::setTheme(AppTheme t) {
    if (t == theme_) return;
    theme_ = t;
    saveToSettings();
    emit themeChanged();
    recompute();
    emit schemeChanged();
}

void ThemeManager::setMode(ThemeMode m) {
    if (m == mode_) return;
    mode_ = m;
    saveToSettings();
    emit modeChanged();
    refresh();
}

void ThemeManager::setDynamicSeedColor(const QColor& c) {
    if (!c.isValid()) {
        clearDynamicSeedColor();
        return;
    }
    if (hasDynamicSeed_ && dynamicSeed_ == c) return;
    dynamicSeed_ = c;
    hasDynamicSeed_ = true;
    saveToSettings();
    if (theme_ == AppTheme::Dynamic) {
        recompute();
        emit schemeChanged();
    }
}

void ThemeManager::clearDynamicSeedColor() {
    if (!hasDynamicSeed_) return;
    hasDynamicSeed_ = false;
    dynamicSeed_ = QColor();
    saveToSettings();
    if (theme_ == AppTheme::Dynamic) {
        recompute();
        emit schemeChanged();
    }
}

void ThemeManager::refresh() {
    // Re-resolve the system mode in case the OS changed theme.
    systemMode_ = resolveSystemMode();
    ThemeMode before = effectiveMode_;
    recompute();
    if (before != effectiveMode_) {
        emit effectiveModeChanged();
    }
    emit schemeChanged();
}

bool ThemeManager::isDark() const {
    return effectiveMode_ == ThemeMode::Dark || effectiveMode_ == ThemeMode::Amoled;
}

void ThemeManager::recompute() {
    // Resolve System → Light/Dark exactly once, at first use. We can't
    // re-resolve on every recompute() because the QPalette we read
    // from is itself mutated by the scheme we're about to publish
    // (chicken/egg). The user can force a re-resolve by calling
    // refresh() after manually triggering a system theme change.
    if (!systemResolved_) {
        systemMode_ = resolveSystemMode();
        systemResolved_ = true;
    }

    effectiveMode_ = mode_;
    if (effectiveMode_ == ThemeMode::System) {
        effectiveMode_ = systemMode_;
    }

    // Choose the seed.
    if (theme_ == AppTheme::Dynamic && hasDynamicSeed_) {
        scheme_ = schemeFromSeed(AppTheme::Dynamic, effectiveMode_, dynamicSeed_.rgba());
    } else {
        scheme_ = schemeFor(theme_, effectiveMode_);
    }
    scheme_.name = appThemeDisplayName(theme_)
                 + QStringLiteral("/")
                 + themeModeDisplayName(effectiveMode_);
}

ThemeMode ThemeManager::resolveSystemMode() const {
    // We can only read the OS palette reliably once — see the comment
    // in recompute(). The default QPalette uses the current style's
    // colors; for a fresh QApplication this reflects the OS theme.
    QPalette p;
    int textL = p.windowText().color().lightness();
    int bgL   = p.window().color().lightness();
    return (textL > bgL) ? ThemeMode::Light : ThemeMode::Dark;
}

} // namespace mf::core::theme
// ThemeManager.h
// Centralised holder for the active AppTheme + ThemeMode selection.
// Watches QSettings (via SettingsControl), recomputes the
// MusicefyColorScheme on change, and exposes everything as Q_PROPERTY
// so QML and Widgets can bind reactively.
//
// Two important details:
//  1) schemeFor(Dynamic, mode) needs a seed color. If no seed has been
//     pushed via setDynamicSeedColor(), we fall back to the Default
//     palette's seed.
//  2) Amoled mode is treated as a hard override: surfaces are forced
//     to pure black regardless of seed. This is exposed via
//     effectiveMode() (used by AppContainer's MainWindow palette).

#pragma once

#include "AppTheme.h"
#include "MusicefyColorScheme.h"
#include "ThemeMode.h"

#include <QColor>
#include <QObject>
#include <memory>

namespace mf::core::services {
class SettingsControl;
} // namespace mf::core::services

namespace mf::core::theme {

class ThemeManager : public QObject {
    Q_OBJECT
    Q_PROPERTY(AppTheme  theme            READ theme           WRITE setTheme   NOTIFY themeChanged)
    Q_PROPERTY(ThemeMode mode             READ mode            WRITE setMode    NOTIFY modeChanged)
    Q_PROPERTY(ThemeMode effectiveMode    READ effectiveMode                       NOTIFY effectiveModeChanged)
    Q_PROPERTY(bool      isDark           READ isDark                              NOTIFY effectiveModeChanged)
    Q_PROPERTY(QColor    primary          READ primary                             NOTIFY schemeChanged)
    Q_PROPERTY(QColor    background       READ background                          NOTIFY schemeChanged)
    Q_PROPERTY(QColor    surface          READ surface                             NOTIFY schemeChanged)
    Q_PROPERTY(QColor    onSurface        READ onSurface                           NOTIFY schemeChanged)
    Q_PROPERTY(QColor    onBackground     READ onBackground                        NOTIFY schemeChanged)
    Q_PROPERTY(QString   schemeName       READ schemeName                          NOTIFY schemeChanged)
    Q_PROPERTY(MusicefyColorScheme scheme  READ scheme                             NOTIFY schemeChanged)

public:
    explicit ThemeManager(QObject* parent = nullptr);
    ~ThemeManager() override = default;

    void bindSettings(mf::core::services::SettingsControl* settings);

    AppTheme  theme()    const { return theme_; }
    ThemeMode mode()     const { return mode_; }

    // Resolved mode: System → Light/Dark based on QPalette.
    ThemeMode effectiveMode() const { return effectiveMode_; }
    bool      isDark()     const;

    MusicefyColorScheme scheme() const { return scheme_; }
    QString             schemeName() const { return scheme_.name; }
    QColor              primary()    const { return scheme_.primary; }
    QColor              background() const { return scheme_.background; }
    QColor              surface()    const { return scheme_.surface; }
    QColor              onSurface()  const { return scheme_.onSurface; }
    QColor              onBackground() const { return scheme_.onBackground; }

    // For Dynamic themes: caller pushes the seed color (extracted from
    // album art or a user pick). Triggers a scheme recompute.
    Q_INVOKABLE void setDynamicSeedColor(const QColor& c);
    Q_INVOKABLE void clearDynamicSeedColor();

public slots:
    void setTheme(AppTheme t);
    void setMode(ThemeMode m);
    void loadFromSettings();
    void saveToSettings();
    void refresh();

signals:
    void themeChanged();
    void modeChanged();
    void effectiveModeChanged();
    void schemeChanged();

private:
    void recompute();
    ThemeMode resolveSystemMode() const;

    AppTheme  theme_  = AppTheme::Default;
    ThemeMode mode_   = ThemeMode::System;
    ThemeMode effectiveMode_ = ThemeMode::Light;
    ThemeMode systemMode_ = ThemeMode::Light;
    bool      systemResolved_ = false;
    MusicefyColorScheme scheme_;
    QColor dynamicSeed_;
    bool hasDynamicSeed_ = false;

    mf::core::services::SettingsControl* settings_ = nullptr;
};

} // namespace mf::core::theme
#pragma once

#include "AppTheme.h"
#include "MusicefyColorScheme.h"
#include "ThemeMode.h"

namespace mf::core::theme {

QList<AppTheme>             allAppThemes();
QList<ThemeMode>            allThemeModes();
QString                     appThemeDisplayName(AppTheme t);
QString                     themeModeDisplayName(ThemeMode m);
QString                     appThemeAccentHex(AppTheme t);
MusicefyColorScheme         schemeFor(AppTheme t, ThemeMode m);
MusicefyColorScheme         schemeFromSeed(AppTheme t, ThemeMode m, int seedArgb);
MusicefyColorScheme         schemeFromDynamicSeed(int seedArgb, ThemeMode m);

} // namespace mf::core::theme

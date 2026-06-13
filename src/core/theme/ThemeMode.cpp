#include "AppTheme.h"
#include "ThemeMode.h"

namespace mf::core::theme {

int appThemeToInt(AppTheme t) {
    return static_cast<int>(t);
}
AppTheme appThemeFromInt(int v) {
    if (v < 0 || v > static_cast<int>(AppTheme::Dynamic)) {
        return AppTheme::Default;
    }
    return static_cast<AppTheme>(v);
}

int themeModeToInt(ThemeMode m) {
    return static_cast<int>(m);
}
ThemeMode themeModeFromInt(int v) {
    if (v < 0 || v > static_cast<int>(ThemeMode::Amoled)) {
        return ThemeMode::System;
    }
    return static_cast<ThemeMode>(v);
}

} // namespace mf::core::theme

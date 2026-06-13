#pragma once

namespace mf::core::theme {

enum class ThemeMode {
    System,
    Light,
    Dark,
    Amoled,
};

int  themeModeToInt(ThemeMode m);
ThemeMode themeModeFromInt(int v);

} // namespace mf::core::theme

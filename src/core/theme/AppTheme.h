#pragma once

namespace mf::core::theme {

enum class AppTheme {
    Default,
    GreenApple,
    Lavender,
    StrawberryDaiquiri,
    MidnightDusk,
    Tako,
    TealTurquoise,
    TidalWave,
    CottonCandy,
    Cloudflare,
    Doom,
    Mocha,
    Catppuccin,
    Sapphire,
    Nord,
    YinAndYang,
    Yotsuba,
    Monochrome,
    Dynamic,
};

int  appThemeToInt(AppTheme t);
AppTheme appThemeFromInt(int v);

} // namespace mf::core::theme

#include "AppThemeColorSchemes.h"

#include <QHash>
#include <QList>
#include <QString>

#include "cpp/dynamiccolor/dynamic_scheme.h"

namespace mf::core::theme {

namespace {

struct PaletteEntry {
    const char* name;
    const char* lightSeed;
    const char* darkSeed;
};

constexpr PaletteEntry kPalettes[] = {
    {"Default",             "#FFB2C2", "#9B0046"},
    {"GreenApple",          "#9DDC2C", "#3B6F00"},
    {"Lavender",            "#C8B6FF", "#4F378B"},
    {"StrawberryDaiquiri",  "#FF8A80", "#BA1A1A"},
    {"MidnightDusk",        "#7B9CFF", "#1A237E"},
    {"Tako",                "#FFB4A2", "#7F0000"},
    {"TealTurquoise",       "#3DDBC0", "#00504A"},
    {"TidalWave",           "#80D8FF", "#006590"},
    {"CottonCandy",         "#FFB3D9", "#880E4F"},
    {"Cloudflare",          "#FFB300", "#FF6F00"},
    {"Doom",                "#FF5252", "#3700B3"},
    {"Mocha",               "#BCAAA4", "#3E2723"},
    {"Catppuccin",          "#F5C2E7", "#CBA6F7"},
    {"Sapphire",            "#82B1FF", "#0D47A1"},
    {"Nord",                "#88C0D0", "#2E3440"},
    {"YinAndYang",          "#FFFFFF", "#000000"},
    {"Yotsuba",             "#FFCDD2", "#B71C1C"},
    {"Monochrome",          "#9E9E9E", "#212121"},
    {"Dynamic",             "#FFB2C2", "#9B0046"},
};

QColor pickSeed(AppTheme t, ThemeMode m) {
    int i = appThemeToInt(t);
    if (i < 0 || i >= static_cast<int>(sizeof(kPalettes) / sizeof(kPalettes[0]))) {
        return QColor(QStringLiteral("#FFB2C2"));
    }
    // System resolves to Light or Dark by the caller's environment.
    // For schemeFor() we always pass the resolved mode, so a "System"
    // here would be a bug — fall back to the light seed.
    bool darkish = (m == ThemeMode::Dark || m == ThemeMode::Amoled);
    const char* hex = darkish ? kPalettes[i].darkSeed : kPalettes[i].lightSeed;
    return QColor(QString::fromLatin1(hex));
}

bool isLightMode(ThemeMode m) {
    return m == ThemeMode::Light;
}

MusicefyColorScheme buildScheme(AppTheme t, ThemeMode m, QColor seed) {
    MusicefyColorScheme s = lightDefault();
    s.name = appThemeDisplayName(t) + QStringLiteral("/") + themeModeDisplayName(m);
    s.primary = seed;

    // onPrimary: high-contrast text on primary.
    qreal lum = 0.2126 * seed.redF() + 0.7152 * seed.greenF() + 0.0722 * seed.blueF();
    s.onPrimary = (lum < 0.5) ? QColor(QStringLiteral("#FFFFFF"))
                              : QColor(QStringLiteral("#000000"));

    // Containers: lighter/darker variant of primary (cheap approximation —
    // the full Material You pipeline gives a real tone-locked result).
    if (isLightMode(m)) {
        s.primaryContainer   = seed.lighter(140);
        s.onPrimaryContainer = seed.darker(150);
        s.secondary          = seed;
        s.tertiary           = QColor(QStringLiteral("#7C5635"));
        s.background         = QColor(QStringLiteral("#FFFBFF"));
        s.onBackground       = QColor(QStringLiteral("#201A1B"));
        s.surface            = QColor(QStringLiteral("#FFFBFF"));
        s.onSurface          = QColor(QStringLiteral("#201A1B"));
        s.surfaceVariant     = QColor(QStringLiteral("#F2DDE1"));
        s.outline            = QColor(QStringLiteral("#837377"));
        s.surfaceContainer       = QColor(QStringLiteral("#F1ECEE"));
        s.surfaceContainerHigh   = QColor(QStringLiteral("#EBE6E8"));
        s.surfaceContainerHighest= QColor(QStringLiteral("#E5E1E3"));
        s.surfaceContainerLow    = QColor(QStringLiteral("#F7F1F3"));
        s.surfaceContainerLowest = QColor(QStringLiteral("#FFFFFF"));
        s.inverseSurface         = QColor(QStringLiteral("#362F30"));
        s.inverseOnSurface       = QColor(QStringLiteral("#F9EEEF"));
        s.inversePrimary         = seed.lighter(120);
    } else if (m == ThemeMode::Amoled) {
        s.primaryContainer   = seed.darker(140);
        s.onPrimaryContainer = seed.lighter(150);
        s.secondary          = seed;
        s.tertiary           = QColor(QStringLiteral("#FFDCBE"));
        s.background         = QColor(QStringLiteral("#000000"));
        s.onBackground       = QColor(QStringLiteral("#E6E1E2"));
        s.surface            = QColor(QStringLiteral("#000000"));
        s.onSurface          = QColor(QStringLiteral("#E6E1E2"));
        s.surfaceVariant     = QColor(QStringLiteral("#2B2A2B"));
        s.outline            = QColor(QStringLiteral("#9E9395"));
        s.surfaceContainer       = QColor(QStringLiteral("#0A0A0A"));
        s.surfaceContainerHigh   = QColor(QStringLiteral("#121212"));
        s.surfaceContainerHighest= QColor(QStringLiteral("#1A1A1A"));
        s.surfaceContainerLow    = QColor(QStringLiteral("#050505"));
        s.surfaceContainerLowest = QColor(QStringLiteral("#000000"));
        s.inverseSurface         = QColor(QStringLiteral("#E6E1E2"));
        s.inverseOnSurface       = QColor(QStringLiteral("#362F30"));
        s.inversePrimary         = seed.darker(120);
    } else {
        // Dark
        s.primaryContainer   = seed.darker(140);
        s.onPrimaryContainer = seed.lighter(150);
        s.secondary          = seed;
        s.tertiary           = QColor(QStringLiteral("#FFDCBE"));
        s.background         = QColor(QStringLiteral("#1A1718"));
        s.onBackground       = QColor(QStringLiteral("#EDE8E9"));
        s.surface            = QColor(QStringLiteral("#1A1718"));
        s.onSurface          = QColor(QStringLiteral("#EDE8E9"));
        s.surfaceVariant     = QColor(QStringLiteral("#524345"));
        s.outline            = QColor(QStringLiteral("#9E9395"));
        s.surfaceContainer       = QColor(QStringLiteral("#231F20"));
        s.surfaceContainerHigh   = QColor(QStringLiteral("#2B2728"));
        s.surfaceContainerHighest= QColor(QStringLiteral("#363132"));
        s.surfaceContainerLow    = QColor(QStringLiteral("#181415"));
        s.surfaceContainerLowest = QColor(QStringLiteral("#0E0B0C"));
        s.inverseSurface         = QColor(QStringLiteral("#EDE8E9"));
        s.inverseOnSurface       = QColor(QStringLiteral("#362F30"));
        s.inversePrimary         = seed.darker(120);
    }

    // Containers that don't depend on mode.
    s.secondaryContainer   = s.primaryContainer;
    s.onSecondaryContainer = s.onPrimaryContainer;
    s.tertiaryContainer    = s.primaryContainer;
    s.onTertiaryContainer  = s.onPrimaryContainer;
    s.onSecondary          = s.onPrimary;
    s.onTertiary           = s.onPrimary;
    s.onSurfaceVariant     = s.outline;
    s.outlineVariant       = s.surfaceVariant;
    return s;
}

} // namespace

QList<AppTheme> allAppThemes() {
    return QList<AppTheme>{
        AppTheme::Default, AppTheme::GreenApple, AppTheme::Lavender,
        AppTheme::StrawberryDaiquiri, AppTheme::MidnightDusk, AppTheme::Tako,
        AppTheme::TealTurquoise, AppTheme::TidalWave, AppTheme::CottonCandy,
        AppTheme::Cloudflare, AppTheme::Doom, AppTheme::Mocha,
        AppTheme::Sapphire, AppTheme::Nord, AppTheme::YinAndYang,
        AppTheme::Yotsuba, AppTheme::Monochrome, AppTheme::Dynamic,
    };
}

QList<ThemeMode> allThemeModes() {
    return QList<ThemeMode>{
        ThemeMode::System, ThemeMode::Light, ThemeMode::Dark, ThemeMode::Amoled,
    };
}

QString appThemeDisplayName(AppTheme t) {
    int i = appThemeToInt(t);
    if (i < 0 || i >= static_cast<int>(sizeof(kPalettes) / sizeof(kPalettes[0]))) {
        return QStringLiteral("Default");
    }
    return QString::fromLatin1(kPalettes[i].name);
}

QString themeModeDisplayName(ThemeMode m) {
    switch (m) {
    case ThemeMode::System: return QStringLiteral("System");
    case ThemeMode::Light:  return QStringLiteral("Light");
    case ThemeMode::Dark:   return QStringLiteral("Dark");
    case ThemeMode::Amoled: return QStringLiteral("Amoled");
    }
    return QStringLiteral("System");
}

QString appThemeAccentHex(AppTheme t) {
    int i = appThemeToInt(t);
    if (i < 0 || i >= static_cast<int>(sizeof(kPalettes) / sizeof(kPalettes[0]))) {
        return QStringLiteral("#FFB2C2");
    }
    return QString::fromLatin1(kPalettes[i].lightSeed);
}

MusicefyColorScheme schemeFor(AppTheme t, ThemeMode m) {
    QColor seed = pickSeed(t, m);
    return buildScheme(t, m, seed);
}

MusicefyColorScheme schemeFromSeed(AppTheme t, ThemeMode m, int seedArgb) {
    QColor seed(seedArgb);
    if (!seed.isValid()) {
        seed = pickSeed(t, m);
    }
    return buildScheme(t, m, seed);
}

MusicefyColorScheme schemeFromDynamicSeed(int seedArgb, ThemeMode m) {
    using namespace material_color_utilities;
    using namespace material_color_utilities::dynamiccolor;

    bool isDark = (m == ThemeMode::Dark || m == ThemeMode::Amoled);
    DynamicScheme ds = DynamicScheme::fromSeed(static_cast<Argb>(seedArgb), isDark);

    MusicefyColorScheme s = lightDefault();

    auto toQColor = [](Argb argb) -> QColor {
        int a = (argb >> 24) & 0xFF;
        int r = (argb >> 16) & 0xFF;
        int g = (argb >> 8) & 0xFF;
        int b = argb & 0xFF;
        return QColor(r, g, b, a);
    };

    s.primary            = toQColor(ds.get(ColorRole::Primary));
    s.onPrimary          = toQColor(ds.get(ColorRole::OnPrimary));
    s.primaryContainer   = toQColor(ds.get(ColorRole::PrimaryContainer));
    s.onPrimaryContainer = toQColor(ds.get(ColorRole::OnPrimaryContainer));
    s.secondary          = toQColor(ds.get(ColorRole::Secondary));
    s.onSecondary        = toQColor(ds.get(ColorRole::OnSecondary));
    s.secondaryContainer   = toQColor(ds.get(ColorRole::SecondaryContainer));
    s.onSecondaryContainer = toQColor(ds.get(ColorRole::OnSecondaryContainer));
    s.tertiary           = toQColor(ds.get(ColorRole::Tertiary));
    s.onTertiary         = toQColor(ds.get(ColorRole::OnTertiary));
    s.tertiaryContainer    = toQColor(ds.get(ColorRole::TertiaryContainer));
    s.onTertiaryContainer  = toQColor(ds.get(ColorRole::OnTertiaryContainer));
    s.background         = toQColor(ds.get(ColorRole::Background));
    s.onBackground       = toQColor(ds.get(ColorRole::OnBackground));
    s.surface            = toQColor(ds.get(ColorRole::Surface));
    s.onSurface          = toQColor(ds.get(ColorRole::OnSurface));
    s.surfaceVariant     = toQColor(ds.get(ColorRole::SurfaceVariant));
    s.onSurfaceVariant   = toQColor(ds.get(ColorRole::OnSurfaceVariant));
    s.outline            = toQColor(ds.get(ColorRole::Outline));
    s.outlineVariant     = toQColor(ds.get(ColorRole::OutlineVariant));
    s.inverseSurface     = toQColor(ds.get(ColorRole::InverseSurface));
    s.inverseOnSurface   = toQColor(ds.get(ColorRole::InverseOnSurface));
    s.inversePrimary     = toQColor(ds.get(ColorRole::InversePrimary));
    s.surfaceContainer       = toQColor(ds.get(ColorRole::SurfaceContainer));
    s.surfaceContainerHigh   = toQColor(ds.get(ColorRole::SurfaceContainerHigh));
    s.surfaceContainerHighest= toQColor(ds.get(ColorRole::SurfaceContainerHighest));
    s.surfaceContainerLow    = toQColor(ds.get(ColorRole::SurfaceContainerLow));
    s.surfaceContainerLowest = toQColor(ds.get(ColorRole::SurfaceContainerLowest));

    s.name = QStringLiteral("Dynamic/")
           + themeModeDisplayName(m);
    return s;
}

} // namespace mf::core::theme

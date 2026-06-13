#include "MusicefyColorScheme.h"

#include <QHash>
#include <QString>

namespace mf::core::theme {

QHash<QString, QString> lightPalette() {
    return QHash<QString, QString>{
        {"primary",                "#9B0046"},
        {"onPrimary",              "#FFFFFF"},
        {"primaryContainer",       "#FFD9E2"},
        {"onPrimaryContainer",     "#3F0017"},
        {"secondary",              "#74565F"},
        {"onSecondary",            "#FFFFFF"},
        {"secondaryContainer",     "#FFD9E2"},
        {"onSecondaryContainer",   "#2B151C"},
        {"tertiary",               "#7C5635"},
        {"onTertiary",             "#FFFFFF"},
        {"tertiaryContainer",      "#FFDCBE"},
        {"onTertiaryContainer",    "#2E1500"},
        {"error",                  "#BA1A1A"},
        {"onError",                "#FFFFFF"},
        {"errorContainer",         "#FFDAD6"},
        {"onErrorContainer",       "#410002"},
        {"background",             "#FFFBFF"},
        {"onBackground",           "#201A1B"},
        {"surface",                "#FFFBFF"},
        {"onSurface",              "#201A1B"},
        {"surfaceVariant",         "#F2DDE1"},
        {"onSurfaceVariant",       "#524345"},
        {"outline",                "#837377"},
        {"outlineVariant",         "#D4C2C6"},
        {"surfaceContainer",       "#F1ECEE"},
        {"surfaceContainerHigh",   "#EBE6E8"},
        {"surfaceContainerHighest","#E5E1E3"},
        {"surfaceContainerLow",    "#F7F1F3"},
        {"surfaceContainerLowest", "#FFFFFF"},
        {"inverseSurface",         "#362F30"},
        {"inverseOnSurface",       "#F9EEEF"},
        {"inversePrimary",         "#FFB2C2"},
    };
}

MusicefyColorScheme lightDefault() {
    MusicefyColorScheme s;
    s.name = "Default/Light";
    auto p = lightPalette();
    for (auto it = p.constBegin(); it != p.constEnd(); ++it) {
        QColor c(it.value());
        if (it.key() == "primary")                   s.primary = c;
        else if (it.key() == "onPrimary")            s.onPrimary = c;
        else if (it.key() == "primaryContainer")     s.primaryContainer = c;
        else if (it.key() == "onPrimaryContainer")   s.onPrimaryContainer = c;
        else if (it.key() == "secondary")            s.secondary = c;
        else if (it.key() == "onSecondary")          s.onSecondary = c;
        else if (it.key() == "secondaryContainer")   s.secondaryContainer = c;
        else if (it.key() == "onSecondaryContainer") s.onSecondaryContainer = c;
        else if (it.key() == "tertiary")             s.tertiary = c;
        else if (it.key() == "onTertiary")           s.onTertiary = c;
        else if (it.key() == "tertiaryContainer")    s.tertiaryContainer = c;
        else if (it.key() == "onTertiaryContainer")  s.onTertiaryContainer = c;
        else if (it.key() == "error")                s.error = c;
        else if (it.key() == "onError")              s.onError = c;
        else if (it.key() == "errorContainer")       s.errorContainer = c;
        else if (it.key() == "onErrorContainer")     s.onErrorContainer = c;
        else if (it.key() == "background")           s.background = c;
        else if (it.key() == "onBackground")         s.onBackground = c;
        else if (it.key() == "surface")              s.surface = c;
        else if (it.key() == "onSurface")            s.onSurface = c;
        else if (it.key() == "surfaceVariant")       s.surfaceVariant = c;
        else if (it.key() == "onSurfaceVariant")     s.onSurfaceVariant = c;
        else if (it.key() == "outline")              s.outline = c;
        else if (it.key() == "outlineVariant")       s.outlineVariant = c;
        else if (it.key() == "surfaceContainer")     s.surfaceContainer = c;
        else if (it.key() == "surfaceContainerHigh") s.surfaceContainerHigh = c;
        else if (it.key() == "surfaceContainerHighest") s.surfaceContainerHighest = c;
        else if (it.key() == "surfaceContainerLow")  s.surfaceContainerLow = c;
        else if (it.key() == "surfaceContainerLowest") s.surfaceContainerLowest = c;
        else if (it.key() == "inverseSurface")       s.inverseSurface = c;
        else if (it.key() == "inverseOnSurface")     s.inverseOnSurface = c;
        else if (it.key() == "inversePrimary")       s.inversePrimary = c;
    }
    return s;
}

} // namespace mf::core::theme

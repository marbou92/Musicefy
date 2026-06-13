// DynamicScheme.cpp
// See header.

#include "dynamic_scheme.h"

#include <cmath>

namespace material_color_utilities {
namespace dynamiccolor {

namespace {

double hueRotate(double hue, double shift) {
    double result = hue + shift;
    while (result >= 360.0) result -= 360.0;
    while (result < 0.0) result += 360.0;
    return result;
}

}  // namespace

DynamicScheme::DynamicScheme(const DynamicSchemeConfig& config)
    : config_(config)
    , primary_(config.hue, config.chroma)
    , secondary_(hueRotate(config.hue, config.secondaryHueShift), config.secondaryChroma)
    , tertiary_(hueRotate(config.hue, config.tertiaryHueShift), config.tertiaryChroma)
    , neutral_(hueRotate(config.hue, config.neutralHueShift), config.neutralChroma)
    , neutralVariant_(hueRotate(config.hue, config.neutralVariantHueShift),
                      config.neutralVariantChroma)
{
}

DynamicScheme DynamicScheme::fromSeed(Argb seed, bool isDark) {
    Hct h(seed);
    DynamicSchemeConfig cfg;
    cfg.hue = h.get_hue();
    cfg.chroma = std::max(h.get_chroma(), 48.0);  // ensure minimum vibrancy
    cfg.isDark = isDark;
    return DynamicScheme(cfg);
}

DynamicScheme DynamicScheme::fromSeed(Argb primarySeed, Argb secondarySeed,
                                       Argb tertiarySeed, bool isDark) {
    Hct hp(primarySeed);
    Hct hs(secondarySeed);
    Hct ht(tertiarySeed);

    DynamicSchemeConfig cfg;
    cfg.hue = hp.get_hue();
    cfg.chroma = std::max(hp.get_chroma(), 48.0);
    cfg.secondaryHueShift = hs.get_hue() - hp.get_hue();
    cfg.secondaryChroma = std::max(hs.get_chroma(), 16.0);
    cfg.tertiaryHueShift = ht.get_hue() - hp.get_hue();
    cfg.tertiaryChroma = std::max(ht.get_chroma(), 24.0);
    cfg.isDark = isDark;
    return DynamicScheme(cfg);
}

Argb DynamicScheme::get(ColorRole role) const {
    return getTonal(role, defaultTone(role));
}

Argb DynamicScheme::getTonal(ColorRole role, double tone) const {
    switch (role) {
    case ColorRole::Primary:
    case ColorRole::InversePrimary:
        return primary_.tone(tone);
    case ColorRole::OnPrimary:
        return primary_.tone(config_.isDark ? 20 : 100);
    case ColorRole::PrimaryContainer:
        return primary_.tone(config_.isDark ? 30 : 90);
    case ColorRole::OnPrimaryContainer:
        return primary_.tone(config_.isDark ? 90 : 10);

    case ColorRole::Secondary:
        return secondary_.tone(tone);
    case ColorRole::OnSecondary:
        return secondary_.tone(config_.isDark ? 20 : 100);
    case ColorRole::SecondaryContainer:
        return secondary_.tone(config_.isDark ? 30 : 90);
    case ColorRole::OnSecondaryContainer:
        return secondary_.tone(config_.isDark ? 90 : 10);

    case ColorRole::Tertiary:
        return tertiary_.tone(tone);
    case ColorRole::OnTertiary:
        return tertiary_.tone(config_.isDark ? 20 : 100);
    case ColorRole::TertiaryContainer:
        return tertiary_.tone(config_.isDark ? 30 : 90);
    case ColorRole::OnTertiaryContainer:
        return tertiary_.tone(config_.isDark ? 90 : 10);

    case ColorRole::Error:
        // Use a fixed red error palette.
        return Hct(25.0, 84.0, tone).ToInt();
    case ColorRole::OnError:
        return Hct(359.0, 79.0, config_.isDark ? 20 : 100).ToInt();
    case ColorRole::ErrorContainer:
        return Hct(353.0, 92.0, config_.isDark ? 30 : 90).ToInt();
    case ColorRole::OnErrorContainer:
        return Hct(357.0, 76.0, config_.isDark ? 90 : 10).ToInt();

    case ColorRole::Background:
    case ColorRole::Surface:
        return neutral_.tone(config_.isDark ? 6 : 98);
    case ColorRole::OnBackground:
    case ColorRole::OnSurface:
        return neutral_.tone(config_.isDark ? 90 : 10);
    case ColorRole::SurfaceVariant:
        return neutralVariant_.tone(config_.isDark ? 30 : 90);
    case ColorRole::OnSurfaceVariant:
        return neutralVariant_.tone(config_.isDark ? 80 : 30);
    case ColorRole::Outline:
        return neutralVariant_.tone(config_.isDark ? 60 : 50);
    case ColorRole::OutlineVariant:
        return neutralVariant_.tone(config_.isDark ? 30 : 80);
    case ColorRole::Shadow:
        return 0xFF000000;
    case ColorRole::Scrim:
        return 0xFF000000;
    case ColorRole::InverseSurface:
        return neutral_.tone(config_.isDark ? 90 : 20);
    case ColorRole::InverseOnSurface:
        return neutral_.tone(config_.isDark ? 20 : 95);
    case ColorRole::SurfaceDim:
        return neutral_.tone(config_.isDark ? 6 : 87);
    case ColorRole::SurfaceBright:
        return neutral_.tone(config_.isDark ? 24 : 98);
    case ColorRole::SurfaceContainerLowest:
        return neutral_.tone(config_.isDark ? 4 : 100);
    case ColorRole::SurfaceContainerLow:
        return neutral_.tone(config_.isDark ? 10 : 96);
    case ColorRole::SurfaceContainer:
        return neutral_.tone(config_.isDark ? 12 : 94);
    case ColorRole::SurfaceContainerHigh:
        return neutral_.tone(config_.isDark ? 17 : 92);
    case ColorRole::SurfaceContainerHighest:
        return neutral_.tone(config_.isDark ? 22 : 90);
    }
    // Should never happen.
    return neutral_.tone(50);
}

double DynamicScheme::defaultTone(ColorRole role) const {
    bool dark = config_.isDark;
    switch (role) {
    case ColorRole::Primary:
    case ColorRole::Secondary:
    case ColorRole::Tertiary:
        return dark ? 80 : 40;
    case ColorRole::OnPrimary:
    case ColorRole::OnSecondary:
    case ColorRole::OnTertiary:
        return dark ? 20 : 100;
    case ColorRole::PrimaryContainer:
    case ColorRole::SecondaryContainer:
    case ColorRole::TertiaryContainer:
        return dark ? 30 : 90;
    case ColorRole::OnPrimaryContainer:
    case ColorRole::OnSecondaryContainer:
    case ColorRole::OnTertiaryContainer:
        return dark ? 90 : 10;
    default:
        return 50;
    }
}

}  // namespace dynamiccolor
}  // namespace material_color_utilities

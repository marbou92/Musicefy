// DynamicScheme.h
// A dynamic color scheme generated from a seed color using the
// Material You color system. Generates tonal palettes for each
// color role (primary, secondary, tertiary, neutral, etc.) and
// provides tone-locked access to any role at any tone value.
//
// Simplified port of Google's material_color_utilities
// DynamicScheme class for C++17 / Qt 5.15.

#ifndef CPP_DYNAMICCOLOR_DYNAMIC_SCHEME_H_
#define CPP_DYNAMICCOLOR_DYNAMIC_SCHEME_H_

#include "cpp/cam/hct.h"
#include "cpp/palettes/tonal_palette.h"
#include "cpp/utils/utils.h"

#include <functional>

namespace material_color_utilities {
namespace dynamiccolor {

// Material Design 3 color roles.
enum class ColorRole {
    Primary,
    OnPrimary,
    PrimaryContainer,
    OnPrimaryContainer,
    Secondary,
    OnSecondary,
    SecondaryContainer,
    OnSecondaryContainer,
    Tertiary,
    OnTertiary,
    TertiaryContainer,
    OnTertiaryContainer,
    Error,
    OnError,
    ErrorContainer,
    OnErrorContainer,
    Background,
    OnBackground,
    Surface,
    OnSurface,
    SurfaceVariant,
    OnSurfaceVariant,
    Outline,
    OutlineVariant,
    Shadow,
    Scrim,
    InverseSurface,
    InverseOnSurface,
    InversePrimary,
    SurfaceDim,
    SurfaceBright,
    SurfaceContainerLowest,
    SurfaceContainerLow,
    SurfaceContainer,
    SurfaceContainerHigh,
    SurfaceContainerHighest,
};

struct DynamicSchemeConfig {
    double hue = 0.0;        // Primary hue in degrees (0-360).
    double chroma = 48.0;    // Primary chroma.
    double secondaryHueShift = 0.0;
    double secondaryChroma = 16.0;
    double tertiaryHueShift = 60.0;
    double tertiaryChroma = 24.0;
    double neutralHueShift = 0.0;
    double neutralChroma = 4.0;
    double neutralVariantHueShift = 0.0;
    double neutralVariantChroma = 8.0;
    bool isDark = true;
};

class DynamicScheme {
public:
    explicit DynamicScheme(const DynamicSchemeConfig& config);

    // Build from a seed ARGB color (extracts hue/chroma automatically).
    static DynamicScheme fromSeed(Argb seed, bool isDark = true);
    static DynamicScheme fromSeed(Argb primarySeed, Argb secondarySeed,
                                   Argb tertiarySeed, bool isDark = true);

    // Get the ARGB color for a given role at its default tone.
    Argb get(ColorRole role) const;

    // Get the ARGB color for a given role at a custom tone.
    Argb getTonal(ColorRole role, double tone) const;

    // Access the underlying tonal palettes.
    const palettes::TonalPalette& primaryPalette() const { return primary_; }
    const palettes::TonalPalette& secondaryPalette() const { return secondary_; }
    const palettes::TonalPalette& tertiaryPalette() const { return tertiary_; }
    const palettes::TonalPalette& neutralPalette() const { return neutral_; }
    const palettes::TonalPalette& neutralVariantPalette() const { return neutralVariant_; }

    bool isDark() const { return config_.isDark; }

private:
    double defaultTone(ColorRole role) const;

    DynamicSchemeConfig config_;
    palettes::TonalPalette primary_;
    palettes::TonalPalette secondary_;
    palettes::TonalPalette tertiary_;
    palettes::TonalPalette neutral_;
    palettes::TonalPalette neutralVariant_;
};

}  // namespace dynamiccolor
}  // namespace material_color_utilities

#endif  // CPP_DYNAMICCOLOR_DYNAMIC_SCHEME_H_

// TonalPalette.h
// A tonal palette is a set of colors at specific tone values (0-100)
// for a given hue and chroma. Generated using the HCT color space
// to ensure perceptual uniformity across all tones.
//
// This is a simplified port of Google's material_color_utilities
// TonalPalette class, adapted for C++17 / Qt 5.15.

#ifndef CPP_PALETTES_TONAL_PALETTE_H_
#define CPP_PALETTES_TONAL_PALETTE_H_

#include "cpp/cam/hct.h"
#include "cpp/utils/utils.h"

#include <array>

namespace material_color_utilities {
namespace palettes {

// Standard Material You tone values.
constexpr int kToneCount = 13;
constexpr double kTones[kToneCount] = {0, 10, 20, 30, 40, 50, 60, 70, 80, 90, 95, 99, 100};

class TonalPalette {
public:
    // Construct from hue and chroma (the palette will regenerate tones
    // lazily on first access).
    TonalPalette(double hue, double chroma);

    // Construct from an ARGB seed color — extracts hue and chroma
    // automatically.
    explicit TonalPalette(Argb seed);

    // Get the ARGB color at the given tone (0-100). Clamps to valid range.
    Argb tone(double tone) const;

    // Get the hue (in degrees, 0-360).
    double hue() const { return hue_; }

    // Get the chroma.
    double chroma() const { return chroma_; }

private:
    double hue_ = 0.0;
    double chroma_ = 0.0;

    // Cached ARGB values for the standard tones.
    mutable bool cached_ = false;
    mutable std::array<Argb, kToneCount> cache_{};

    void ensureCache() const;
};

}  // namespace palettes
}  // namespace material_color_utilities

#endif  // CPP_PALETTES_TONAL_PALETTE_H_

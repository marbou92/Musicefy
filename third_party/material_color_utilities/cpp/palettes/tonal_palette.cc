// TonalPalette.cpp
// See header.

#include "tonal_palette.h"

#include <algorithm>
#include <cmath>

namespace material_color_utilities {
namespace palettes {

TonalPalette::TonalPalette(double hue, double chroma)
    : hue_(hue), chroma_(chroma) {}

TonalPalette::TonalPalette(Argb seed)
    : hue_(Hct(seed).get_hue()), chroma_(Hct(seed).get_chroma()) {}

Argb TonalPalette::tone(double tone) const {
    tone = std::max(0.0, std::min(100.0, tone));
    ensureCache();

    // Find the two bracketing standard tones.
    for (int i = 0; i < kToneCount - 1; ++i) {
        if (tone >= kTones[i] && tone <= kTones[i + 1]) {
            double t0 = kTones[i];
            double t1 = kTones[i + 1];
            double fraction = (t1 == t0) ? 0.0 : (tone - t0) / (t1 - t0);

            // Interpolate between the two cached ARGB values.
            Argb a0 = cache_[i];
            Argb a1 = cache_[i + 1];

            int r = RedFromInt(a0) + static_cast<int>(
                std::round(fraction * (RedFromInt(a1) - RedFromInt(a0))));
            int g = GreenFromInt(a0) + static_cast<int>(
                std::round(fraction * (GreenFromInt(a1) - GreenFromInt(a0))));
            int b = BlueFromInt(a0) + static_cast<int>(
                std::round(fraction * (BlueFromInt(a1) - BlueFromInt(a0))));
            return ArgbFromRgb(
                std::max(0, std::min(255, r)),
                std::max(0, std::min(255, g)),
                std::max(0, std::min(255, b)));
        }
    }
    // tone == 100 exactly
    return cache_[kToneCount - 1];
}

void TonalPalette::ensureCache() const {
    if (cached_) return;
    Hct h(hue_, chroma_, 0); // Start at tone 0.
    for (int i = 0; i < kToneCount; ++i) {
        h.set_tone(kTones[i]);
        cache_[i] = h.ToInt();
    }
    cached_ = true;
}

}  // namespace palettes
}  // namespace material_color_utilities

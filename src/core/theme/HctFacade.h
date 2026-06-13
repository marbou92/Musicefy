#pragma once

#include <QColor>
#include <QList>

namespace mf::core::theme {

class HctFacade {
public:
    /// Extract hue from a color in degrees.
    static double hueFromArgb(int argb);
    /// Extract chroma (perceptual colorfulness).
    static double chromaFromArgb(int argb);
    /// Extract tone (L* in Lab).
    static double toneFromArgb(int argb);

    /// Convert HCT -> sRGB ARGB. Out-of-gamut colors are clipped.
    static int argbFromHct(double hue, double chroma, double tone);

    /// Generate a 13-tone palette from a seed color, indexed 0..12 (lightest..darkest).
    static QList<int> paletteFromSeed(int seedArgb, int toneCount = 13);
    /// Generate a Material You tonal palette (the 13 standard tones 0/10/20/.../100).
    static QList<int> materialYouPalette(int seedArgb);

    /// Pick the "on" color (text) for a given background, choosing
    /// black or white based on contrast.
    static int onColorFor(int backgroundArgb);
};

} // namespace mf::core::theme

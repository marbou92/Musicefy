#include "HctFacade.h"

#include "cpp/cam/cam.h"
#include "cpp/cam/hct.h"
#include "cpp/cam/hct_solver.h"
#include "cpp/utils/utils.h"

#include <cmath>

namespace mf::core::theme {

using material_color_utilities::Argb;
using material_color_utilities::Cam;
using material_color_utilities::Hct;
using material_color_utilities::SolveToInt;
using material_color_utilities::LstarFromArgb;
using material_color_utilities::RedFromInt;
using material_color_utilities::GreenFromInt;
using material_color_utilities::BlueFromInt;
using material_color_utilities::IntFromLstar;

double HctFacade::hueFromArgb(int argb) {
    Cam cam = material_color_utilities::CamFromInt(static_cast<Argb>(argb));
    return cam.hue;
}

double HctFacade::chromaFromArgb(int argb) {
    Cam cam = material_color_utilities::CamFromInt(static_cast<Argb>(argb));
    return cam.chroma;
}

double HctFacade::toneFromArgb(int argb) {
    return LstarFromArgb(static_cast<Argb>(argb));
}

int HctFacade::argbFromHct(double hue, double chroma, double tone) {
    return static_cast<int>(SolveToInt(hue, chroma, tone));
}

QList<int> HctFacade::paletteFromSeed(int seedArgb, int toneCount) {
    QList<int> out;
    out.reserve(toneCount);
    Hct h(static_cast<Argb>(seedArgb));
    for (int i = 0; i < toneCount; ++i) {
        double t = (100.0 * i) / std::max(1, toneCount - 1);
        Hct t2(h.get_hue(), h.get_chroma(), t);
        out.append(static_cast<int>(t2.ToInt()));
    }
    return out;
}

QList<int> HctFacade::materialYouPalette(int seedArgb) {
    QList<int> tones = {0, 10, 20, 30, 40, 50, 60, 70, 80, 90, 95, 99, 100};
    QList<int> out;
    out.reserve(tones.size());
    Hct h(static_cast<Argb>(seedArgb));
    for (double t : tones) {
        Hct t2(h.get_hue(), h.get_chroma(), t);
        out.append(static_cast<int>(t2.ToInt()));
    }
    return out;
}

namespace {

double channelLuminance(int c) {
    double v = c / 255.0;
    return v <= 0.03928 ? v / 12.92 : std::pow((v + 0.055) / 1.055, 2.4);
}

double relativeLuminance(int argb) {
    double r = channelLuminance(RedFromInt(static_cast<Argb>(argb)));
    double g = channelLuminance(GreenFromInt(static_cast<Argb>(argb)));
    double b = channelLuminance(BlueFromInt(static_cast<Argb>(argb)));
    return 0.2126 * r + 0.7152 * g + 0.0722 * b;
}

double contrastRatio(int a, int b) {
    double la = relativeLuminance(a);
    double lb = relativeLuminance(b);
    double lighter = std::max(la, lb);
    double darker  = std::min(la, lb);
    return (lighter + 0.05) / (darker + 0.05);
}

} // namespace

int HctFacade::onColorFor(int backgroundArgb) {
    constexpr int kBlack = 0xFF000000;
    constexpr int kWhite = 0xFFFFFFFF;
    double contrastWithWhite = contrastRatio(backgroundArgb, kWhite);
    double contrastWithBlack = contrastRatio(backgroundArgb, kBlack);
    return contrastWithWhite >= contrastWithBlack ? kWhite : kBlack;
}

} // namespace mf::core::theme

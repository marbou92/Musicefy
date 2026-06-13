// testhct.cpp
// Smoke tests for the HCT foundation (Material Color Utilities C++ port).
// Validates that Hue/Chroma/Tone extraction is internally consistent.

#include "HctFacade.h"
#include "cpp/cam/cam.h"
#include "cpp/cam/hct.h"
#include "cpp/utils/utils.h"

#include <QTest>
#include <cmath>

using mf::core::theme::HctFacade;
using material_color_utilities::Argb;
using material_color_utilities::Cam;
using material_color_utilities::Hct;
using material_color_utilities::RedFromInt;
using material_color_utilities::GreenFromInt;
using material_color_utilities::BlueFromInt;

namespace {

constexpr double kEpsilon = 0.5;

bool near(double a, double b) {
    return std::fabs(a - b) <= kEpsilon;
}

} // namespace

class TestHct : public QObject {
    Q_OBJECT

private slots:
    void blueHasConsistentHue();
    void greenHasConsistentHue();
    void redHasConsistentHue();
    void whiteHasZeroChroma();
    void blackHasZeroChroma();
    void solveAndExtractRoundtrips();
    void paletteReturnsThirteenTones();
    void onColorPicksWhiteForDarkBackground();
    void onColorPicksBlackForLightBackground();
};

void TestHct::blueHasConsistentHue() {
    constexpr int kBlue = 0xFF0000FF;
    Hct h(kBlue);
    QVERIFY(h.get_hue() >= 250.0 && h.get_hue() <= 280.0);
    QVERIFY(h.get_chroma() > 0.0);
    QVERIFY(h.get_tone() < 50.0);
}

void TestHct::greenHasConsistentHue() {
    constexpr int kGreen = 0xFF00FF00;
    Hct h(kGreen);
    QVERIFY(h.get_hue() >= 100.0 && h.get_hue() <= 160.0);
}

void TestHct::redHasConsistentHue() {
    constexpr int kRed = 0xFFFF0000;
    Hct h(kRed);
    QVERIFY((h.get_hue() >= 0.0 && h.get_hue() <= 40.0) ||
            (h.get_hue() >= 340.0 && h.get_hue() <= 360.0));
}

void TestHct::whiteHasZeroChroma() {
    constexpr int kWhite = 0xFFFFFFFF;
    Hct h(kWhite);
    QCOMPARE(h.get_chroma(), 0.0);
    QVERIFY(near(h.get_tone(), 100.0));
}

void TestHct::blackHasZeroChroma() {
    constexpr int kBlack = 0xFF000000;
    Hct h(kBlack);
    QCOMPARE(h.get_chroma(), 0.0);
    QVERIFY(near(h.get_tone(), 0.0));
}

void TestHct::solveAndExtractRoundtrips() {
    constexpr int kSeed = 0xFF6750A4;
    Hct h(static_cast<Argb>(kSeed));
    double hue = h.get_hue();
    double chroma = h.get_chroma();
    double tone = h.get_tone();
    Argb roundtrip = static_cast<Argb>(HctFacade::argbFromHct(hue, chroma, tone));
    QCOMPARE(static_cast<int>(roundtrip), kSeed);
}

void TestHct::paletteReturnsThirteenTones() {
    QList<int> p = HctFacade::materialYouPalette(0xFF6750A4);
    QCOMPARE(p.size(), 13);
    int first = p.first();
    int last  = p.last();
    QVERIFY(RedFromInt(first) > RedFromInt(last) ||
            GreenFromInt(first) > GreenFromInt(last) ||
            BlueFromInt(first) > BlueFromInt(last));
}

void TestHct::onColorPicksWhiteForDarkBackground() {
    int on = HctFacade::onColorFor(0xFF000000);
    QVERIFY(on == 0xFFFFFFFF || on == 0xFF000000);
    QCOMPARE(on, 0xFFFFFFFF);
}

void TestHct::onColorPicksBlackForLightBackground() {
    int on = HctFacade::onColorFor(0xFFFFFFFF);
    QCOMPARE(on, 0xFF000000);
}

QTEST_GUILESS_MAIN(TestHct)
#include "testhct.moc"

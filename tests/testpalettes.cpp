// testpalettes.cpp
// Smoke tests for the AppTheme and ThemeMode enums and palette lookup.

#include "AppTheme.h"
#include "ThemeMode.h"
#include "AppThemeColorSchemes.h"
#include "MusicefyColorScheme.h"
#include "HctFacade.h"

#include <QTest>

using namespace mf::core::theme;

class TestPalettes : public QObject {
    Q_OBJECT

private slots:
    void allThemesAreListed();
    void allModesAreListed();
    void themeNamesAreNonEmpty();
    void modeNamesAreNonEmpty();
    void defaultThemeIsRecognized();
    void schemeForKnownTheme();
    void dynamicThemeAccentExtracts();
};

void TestPalettes::allThemesAreListed() {
    QList<AppTheme> themes = allAppThemes();
    QVERIFY(themes.size() >= 18);
    QVERIFY(themes.contains(AppTheme::Default));
    QVERIFY(themes.contains(AppTheme::Dynamic));
    QVERIFY(themes.contains(AppTheme::Monochrome));
}

void TestPalettes::allModesAreListed() {
    QList<ThemeMode> modes = allThemeModes();
    QCOMPARE(modes.size(), 4);
    QVERIFY(modes.contains(ThemeMode::System));
    QVERIFY(modes.contains(ThemeMode::Light));
    QVERIFY(modes.contains(ThemeMode::Dark));
    QVERIFY(modes.contains(ThemeMode::Amoled));
}

void TestPalettes::themeNamesAreNonEmpty() {
    for (AppTheme t : allAppThemes()) {
        QVERIFY(!appThemeDisplayName(t).isEmpty());
    }
}

void TestPalettes::modeNamesAreNonEmpty() {
    for (ThemeMode m : allThemeModes()) {
        QVERIFY(!themeModeDisplayName(m).isEmpty());
    }
}

void TestPalettes::defaultThemeIsRecognized() {
    int code = appThemeToInt(AppTheme::Default);
    QCOMPARE(appThemeFromInt(code), AppTheme::Default);
}

void TestPalettes::schemeForKnownTheme() {
    MusicefyColorScheme s = schemeFor(AppTheme::Default, ThemeMode::Light);
    QVERIFY(!s.name.isEmpty());
    QVERIFY(s.primary.isValid());
    QVERIFY(s.background.isValid());
}

void TestPalettes::dynamicThemeAccentExtracts() {
    int seed = 0xFF4285F4;
    double hue = HctFacade::hueFromArgb(seed);
    QVERIFY(hue >= 200.0 && hue <= 240.0);
}

QTEST_GUILESS_MAIN(TestPalettes)
#include "testpalettes.moc"

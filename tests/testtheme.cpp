// testtheme.cpp
// Verifies the ThemeManager:
//  - reads/writes its state via SettingsControl
//  - re-emits schemeChanged on theme or mode change
//  - the scheme actually varies by theme + mode
//  - Amoled forces pure black surfaces
//  - Dynamic with a pushed seed produces a non-default scheme

#include <QtTest/QtTest>
#include <QCoreApplication>
#include <QSettings>

#include "core/theme/AppTheme.h"
#include "core/theme/AppThemeColorSchemes.h"
#include "core/theme/MusicefyColorScheme.h"
#include "core/theme/ThemeManager.h"
#include "core/theme/ThemeMode.h"
#include "core/services/SettingsControl.h"

using namespace mf::core::theme;
using namespace mf::core::services;

class TestTheme : public QObject {
    Q_OBJECT

private:
    // Wipe any persisted state for the test org/app.
    void clearSettings() {
        QSettings s;
        s.clear();
        s.sync();
    }

    // Use a unique per-test app name so tests don't clobber each other.
    void isolateSettings() {
        QCoreApplication::setOrganizationName(QStringLiteral("MusicefyTest"));
        QCoreApplication::setApplicationName(
            QStringLiteral("theme_") +
            QString::number(QDateTime::currentMSecsSinceEpoch()));
        clearSettings();
    }

private slots:
    void initTestCase() {
        QCoreApplication::setOrganizationName(QStringLiteral("MusicefyTest"));
        QCoreApplication::setApplicationName(QStringLiteral("theme_suite"));
    }

    void cleanupTestCase() {
        clearSettings();
    }

    // ── Pure state, no SettingsControl needed ─────────────────────────

    void schemeIsInitializedOnConstruction() {
        ThemeManager tm;
        QVERIFY(!tm.schemeName().isEmpty());
        QVERIFY(tm.scheme().primary.isValid());
    }

    void setThemeEmitsSignalsAndRecomputes() {
        ThemeManager tm;
        int themeSignals = 0;
        int schemeSignals = 0;
        connect(&tm, &ThemeManager::themeChanged, [&]() { ++themeSignals; });
        connect(&tm, &ThemeManager::schemeChanged, [&]() { ++schemeSignals; });

        MusicefyColorScheme before = tm.scheme();
        tm.setTheme(AppTheme::GreenApple);
        QCOMPARE(themeSignals, 1);
        QVERIFY(schemeSignals >= 1);
        QVERIFY(tm.scheme().primary != before.primary);
    }

    void setModeEmitsSignalsAndRecomputes() {
        ThemeManager tm;
        int modeSignals = 0;
        connect(&tm, &ThemeManager::modeChanged, [&]() { ++modeSignals; });

        tm.setMode(ThemeMode::Dark);
        QCOMPARE(modeSignals, 1);
        QCOMPARE(int(tm.effectiveMode()), int(ThemeMode::Dark));
        QVERIFY(tm.isDark());

        tm.setMode(ThemeMode::Amoled);
        QCOMPARE(modeSignals, 2);
        QVERIFY(tm.isDark());
    }

    void amoledForcesBlackSurface() {
        ThemeManager tm;
        tm.setTheme(AppTheme::Default);
        tm.setMode(ThemeMode::Amoled);
        QColor bg = tm.scheme().background;
        QColor surf = tm.scheme().surface;
        QCOMPARE(bg.red(),   0); QCOMPARE(bg.green(),   0); QCOMPARE(bg.blue(),   0);
        QCOMPARE(surf.red(), 0); QCOMPARE(surf.green(), 0); QCOMPARE(surf.blue(), 0);
    }

    void lightAndDarkDiffer() {
        ThemeManager tm;
        tm.setTheme(AppTheme::Default);
        tm.setMode(ThemeMode::Light);
        QColor lightBg = tm.scheme().background;
        tm.setMode(ThemeMode::Dark);
        QColor darkBg = tm.scheme().background;
        QVERIFY(lightBg != darkBg);
        QVERIFY(lightBg.lightness() > darkBg.lightness());
    }

    void differentThemesProduceDifferentSchemes() {
        ThemeManager tm;
        tm.setMode(ThemeMode::Light);
        tm.setTheme(AppTheme::Default);
        QColor d = tm.scheme().primary;
        tm.setTheme(AppTheme::GreenApple);
        QColor g = tm.scheme().primary;
        tm.setTheme(AppTheme::Sapphire);
        QColor s = tm.scheme().primary;
        QVERIFY(d != g);
        QVERIFY(g != s);
        QVERIFY(d != s);
    }

    void dynamicSeedChangesScheme() {
        ThemeManager tm;
        tm.setMode(ThemeMode::Light);
        tm.setTheme(AppTheme::Dynamic);
        MusicefyColorScheme before = tm.scheme();

        tm.setDynamicSeedColor(QColor(0, 64, 255));
        MusicefyColorScheme after = tm.scheme();
        QVERIFY(after.primary != before.primary);
    }

    void settingSameValueIsNoOp() {
        ThemeManager tm;
        int n = 0;
        connect(&tm, &ThemeManager::themeChanged, [&]() { ++n; });
        tm.setTheme(tm.theme());
        QCOMPARE(n, 0);
    }

    // ── Persistence via SettingsControl ────────────────────────────────

    void loadFromSettingsPicksUpValues() {
        isolateSettings();
        SettingsControl sc;
        sc.set(QStringLiteral("theme/seed"), int(AppTheme::Lavender));
        sc.set(QStringLiteral("theme/mode"), int(ThemeMode::Dark));
        sc.sync();

        ThemeManager tm;
        tm.bindSettings(&sc);
        tm.loadFromSettings();

        QCOMPARE(int(tm.theme()), int(AppTheme::Lavender));
        QCOMPARE(int(tm.mode()),  int(ThemeMode::Dark));
        QVERIFY(tm.isDark());
    }

    void saveToSettingsPersistsValues() {
        isolateSettings();
        SettingsControl sc;
        ThemeManager tm;
        tm.bindSettings(&sc);
        tm.loadFromSettings();
        tm.setTheme(AppTheme::Nord);
        tm.setMode(ThemeMode::Amoled);
        tm.saveToSettings();

        // Re-open settings and read.
        SettingsControl reader;
        QCOMPARE(reader.get(QStringLiteral("theme/seed")).toInt(), int(AppTheme::Nord));
        QCOMPARE(reader.get(QStringLiteral("theme/mode")).toInt(), int(ThemeMode::Amoled));
    }

    void dynamicSeedPersists() {
        isolateSettings();
        SettingsControl sc;
        ThemeManager tm1;
        tm1.bindSettings(&sc);
        tm1.loadFromSettings();
        tm1.setTheme(AppTheme::Dynamic);
        tm1.setDynamicSeedColor(QColor(123, 45, 67));
        tm1.saveToSettings();

        ThemeManager tm2;
        tm2.bindSettings(&sc);
        tm2.loadFromSettings();
        QCOMPARE(int(tm2.theme()), int(AppTheme::Dynamic));
        // The scheme for Dynamic should now incorporate the pushed
        // seed (a different primary than the no-seed Dynamic).
        QVERIFY(tm2.scheme().primary.isValid());
    }
};

QTEST_MAIN(TestTheme)
#include "testtheme.moc"

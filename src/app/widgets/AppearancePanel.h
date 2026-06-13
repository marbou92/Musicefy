// AppearancePanel.h
// Real replacement for the Appearance settings stub. Three sections:
//   * Mode       — System / Light / Dark / Amoled radio group
//   * Theme      — grid of color swatches for all AppThemes
//   * Accent     — visible only when Dynamic theme is selected
//                  (8 preset colours + custom-pick button)
//
// All changes go straight to ThemeManager, which persists via
// SettingsControl. The panel re-renders on theme/mode/scheme changes
// so it stays in sync with the rest of the app.

#pragma once

#include <QColor>
#include <QList>
#include <QWidget>

class QButtonGroup;
class QLabel;
class QToolButton;
class QRadioButton;

namespace mf::core::theme { class ThemeManager; }

namespace mf::app::widgets {

class AppearancePanel : public QWidget {
    Q_OBJECT
public:
    explicit AppearancePanel(mf::core::theme::ThemeManager* theme,
                             QWidget* parent = nullptr);
    ~AppearancePanel() override = default;

private slots:
    void onThemeChanged();
    void onModeChanged();
    void onSchemeChanged();
    void onSwatchActivated(int themeId);
    void onAccentPresetClicked();
    void onPickCustomClicked();
    void onAccentCleared();

private:
    void buildUi();
    void applyTheme();
    void syncModeSelection();
    void syncSwatchSelection();
    void syncAccentVisibility();

    mf::core::theme::ThemeManager* theme_ = nullptr;

    // Mode section.
    QRadioButton* modeSystem_ = nullptr;
    QRadioButton* modeLight_  = nullptr;
    QRadioButton* modeDark_   = nullptr;
    QRadioButton* modeAmoled_ = nullptr;
    QButtonGroup* modeGroup_  = nullptr;

    // Accent section.
    QWidget*      accentContainer_ = nullptr;
    QLabel*       accentSwatch_    = nullptr; // current seed visual
    QToolButton*  accentClear_     = nullptr;

    // Cached accent preset colours (8), indexed by sender->property("accentId").
    QList<QColor> accentPresets_;
};

} // namespace mf::app::widgets

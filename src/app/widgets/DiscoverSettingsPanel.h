// DiscoverSettingsPanel.h
// Settings sub-panel for the Discover / browse feed. Replaces the
// Discover stub.
//
//   ┌──────────────────────────────────────────────────────────┐
//   │ Discover                                               │
//   │ Choose which home-feed sections to show and how often   │
//   │ they refresh.                                          │
//   │                                                         │
//   │ Home feed sections                                      │
//   │  ☑ Charts                                              │
//   │  ☑ Moods & Genres                                      │
//   │  ☑ New Releases                                        │
//   │  ☑ Playlists                                           │
//   │                                                         │
//   │ Auto-refresh                                            │
//   │  ☑ Refresh every [ 6 ] hours                            │
//   └──────────────────────────────────────────────────────────┘
//
// Settings are persisted via SettingsControl. The BrowseService
// itself does not yet read these keys; that hookup lands when the
// Home overlay view (Block 2.5) is wired up.

#pragma once

#include <QWidget>

class QCheckBox;
class QLabel;
class QSpinBox;

namespace mf::core::services { class SettingsControl; }
namespace mf::core::theme     { class ThemeManager; }

namespace mf::app::widgets {

class DiscoverSettingsPanel : public QWidget {
    Q_OBJECT
public:
    DiscoverSettingsPanel(mf::core::services::SettingsControl* settings,
                          mf::core::theme::ThemeManager*       theme,
                          QWidget* parent = nullptr);
    ~DiscoverSettingsPanel() override = default;

private slots:
    void onSectionToggled();
    void onAutoRefreshToggled(int state);
    void onIntervalChanged(int v);

private:
    void buildUi();
    void applyTheme();
    void loadFromSettings();
    void persistSection(const QString& key, bool on);

    mf::core::services::SettingsControl* settings_ = nullptr;
    mf::core::theme::ThemeManager*       theme_    = nullptr;

    QCheckBox* charts_    = nullptr;
    QCheckBox* moods_     = nullptr;
    QCheckBox* newRel_    = nullptr;
    QCheckBox* playlists_ = nullptr;
    QCheckBox* autoRefresh_ = nullptr;
    QSpinBox*  interval_  = nullptr;
};

} // namespace mf::app::widgets

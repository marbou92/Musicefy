// ExtensionsSettingsPanel.h
// Settings sub-panel for installed extensions. Replaces the Extensions
// stub.
//
//   ┌──────────────────────────────────────────────────────────┐
//   │ Extensions                                             │
//   │ Manage third-party music source plugins.                │
//   │                                                         │
//   │ Extensions folder                                       │
//   │  <current dir>  [Change…]  [Reload]                    │
//   │                                                         │
//   │ Loaded extensions                                       │
//   │  ┌─────────────────────────────────────────────────┐   │
//   │  │ Name           Version  Author       Status      │   │
//   │  │ Spotify-X      1.2.0    Alice        Enabled ▣  │   │
//   │  │ SoundCloud-Y   0.9.3    Bob          Disabled ▢  │   │
//   │  │ …                                               │   │
//   │  └─────────────────────────────────────────────────┘   │
//   │  0 loaded                                              │
//   └──────────────────────────────────────────────────────────┘
//
// Each row is a clickable toggle. Toggling calls
// ExtensionManager::enable/disableExtension.

#pragma once

#include <QWidget>

class QLabel;
class QListView;
class QPushButton;
class QStandardItem;
class QStandardItemModel;

namespace mf::core::services { class ExtensionManager;
                               class SettingsControl; }
namespace mf::core::theme     { class ThemeManager; }

namespace mf::app::widgets {

class ExtensionsSettingsPanel : public QWidget {
    Q_OBJECT
public:
    ExtensionsSettingsPanel(mf::core::services::ExtensionManager* extMgr,
                             mf::core::services::SettingsControl*  settings,
                             mf::core::theme::ThemeManager*        theme,
                             QWidget* parent = nullptr);
    ~ExtensionsSettingsPanel() override = default;

private slots:
    void onChangeDirClicked();
    void onReloadClicked();
    void onItemChanged(QStandardItem* item);
    void onExtensionsLoaded();
    void onExtensionToggled(const QString& id);

private:
    void buildUi();
    void applyTheme();
    void loadFromSettings();
    void refreshList();

    mf::core::services::ExtensionManager* extMgr_   = nullptr;
    mf::core::services::SettingsControl*  settings_ = nullptr;
    mf::core::theme::ThemeManager*        theme_    = nullptr;

    QLabel*             folderPath_   = nullptr;
    QPushButton*        reloadBtn_    = nullptr;
    QListView*          list_         = nullptr;
    QStandardItemModel* model_        = nullptr;
    QLabel*             statusLabel_  = nullptr;
};

} // namespace mf::app::widgets

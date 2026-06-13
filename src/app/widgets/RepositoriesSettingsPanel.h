// RepositoriesSettingsPanel.h
// Settings sub-panel for extension repository URLs and refresh
// schedule. Replaces the Repositories stub.
//
//   ┌──────────────────────────────────────────────────────────┐
//   │ Repositories                                           │
//   │ Manage extension plugin repository URLs.                │
//   │                                                         │
//   │ Repository URLs                                         │
//   │  ┌──────────────────────────────────────────────┐      │
//   │  │ https://github.com/Alice/musicefy-extensions │      │
//   │  │ https://github.com/Bob/musicefy-plugins      │      │
//   │  └──────────────────────────────────────────────┘      │
//   │  [Add…]  [Remove selected]                             │
//   │                                                         │
//   │ Refresh                                                 │
//   │  ☑ Refresh every [ 12 ] hours                           │
//   │                                                         │
//   │ Security                                                │
//   │  ☑ Require signed extensions                           │
//   └──────────────────────────────────────────────────────────┘
//
// All values are persisted via SettingsControl. Actual fetching
// and signature verification is wired in Block 5.x (extension
// distribution service).

#pragma once

#include <QWidget>

class QCheckBox;
class QInputDialog;
class QLabel;
class QListWidget;
class QPushButton;
class QSpinBox;

namespace mf::core::services { class SettingsControl; }
namespace mf::core::theme     { class ThemeManager; }

namespace mf::app::widgets {

class RepositoriesSettingsPanel : public QWidget {
    Q_OBJECT
public:
    RepositoriesSettingsPanel(mf::core::services::SettingsControl* settings,
                              mf::core::theme::ThemeManager*       theme,
                              QWidget* parent = nullptr);
    ~RepositoriesSettingsPanel() override = default;

private slots:
    void onAddClicked();
    void onRemoveClicked();
    void onAutoRefreshToggled(int state);
    void onIntervalChanged(int v);
    void onRequireSignedToggled(int state);

private:
    void buildUi();
    void applyTheme();
    void loadFromSettings();
    void persistRepos();
    void persistSignatureRequired();

    mf::core::services::SettingsControl* settings_ = nullptr;
    mf::core::theme::ThemeManager*       theme_    = nullptr;

    QListWidget*  urlList_     = nullptr;
    QPushButton*  addBtn_      = nullptr;
    QPushButton*  removeBtn_   = nullptr;
    QCheckBox*    autoRefresh_ = nullptr;
    QSpinBox*     interval_    = nullptr;
    QCheckBox*    requireSigned_ = nullptr;
};

} // namespace mf::app::widgets

// DownloadsSettingsPanel.h
// Settings sub-panel for downloads. Replaces the Downloads stub.
//
//   ┌──────────────────────────────────────────────────────────┐
//   │ Downloads                                               │
//   │ Configure where cached tracks are stored and how many   │
//   │ concurrent downloads are allowed.                       │
//   │                                                         │
//   │ Download directory                                      │
//   │  C:\Users\you\Music\Musicefy\Downloads  [Change…] [⌫]   │
//   │                                                         │
//   │ Parallel downloads                                      │
//   │  [ 3 ] (1–8)                                            │
//   │                                                         │
//   │ Status                                                  │
//   │  Active downloads: 0                                    │
//   │  Completed downloads: 0                                 │
//   │  Default location: C:\Users\you\Music\Musicefy\Downloads│
//   └──────────────────────────────────────────────────────────┘
//
// All values are persisted via SettingsControl. The download dir
// is *also* applied to the DownloadService so newly started
// downloads honour it. Existing files are not moved (deferred to
// Block 5.x — track-level move is a non-trivial operation).

#pragma once

#include <QWidget>

class QLabel;
class QPushButton;
class QSpinBox;
class QTimer;

namespace mf::core::services { class DownloadService;
                               class SettingsControl; }
namespace mf::core::theme     { class ThemeManager; }

namespace mf::app::widgets {

class DownloadsSettingsPanel : public QWidget {
    Q_OBJECT
public:
    DownloadsSettingsPanel(mf::core::services::DownloadService* downloads,
                           mf::core::services::SettingsControl*  settings,
                           mf::core::theme::ThemeManager*        theme,
                           QWidget* parent = nullptr);
    ~DownloadsSettingsPanel() override = default;

private slots:
    void onChangeDirClicked();
    void onResetDirClicked();
    void onParallelChanged(int v);
    void onStatusPoll();

private:
    void buildUi();
    void applyTheme();
    void loadFromSettings();
    void persistParallel();

    mf::core::services::DownloadService* downloads_ = nullptr;
    mf::core::services::SettingsControl*  settings_  = nullptr;
    mf::core::theme::ThemeManager*        theme_     = nullptr;

    QLabel*     dirPath_     = nullptr;
    QPushButton* dirChange_  = nullptr;
    QPushButton* dirReset_   = nullptr;
    QSpinBox*   parallelBox_ = nullptr;
    QLabel*     activeLabel_ = nullptr;
    QLabel*     completedLabel_ = nullptr;
    QLabel*     defaultLabel_   = nullptr;
    QTimer*     pollTimer_   = nullptr;
};

} // namespace mf::app::widgets

// SettingsPage.h
// Settings page. Hosts a left-hand sub-navigation list and a right-
// hand QStackedWidget that swaps between settings sub-panels.
//
// Currently ships one sub-panel — Library/Folders — with the other
// five (Appearance, Sources, Downloads, Discover, Extensions,
// Repositories) stubbed. The selector list still includes them so
// the user can see the structure of the settings area, but clicking
// a stub shows a "lands in Block X" placeholder.
//
// Block 4 wires the real Appearance/Sources/Downloads/Discover/
// Extensions/Repositories panels in order.

#pragma once

#include <QHash>
#include <QString>
#include <QTimer>
#include <QVariant>
#include <QWidget>

class QLabel;
class QListWidget;
class QStackedWidget;

namespace mf::core::services { class LibraryService; class ToastService;
                                  class DownloadService; class ExtensionManager;
                                  class SettingsControl; }
namespace mf::core::sources   { class StreamingSourceManager; }
namespace mf::core::theme     { class ThemeManager; }

namespace mf::app::widgets { class LibrarySettingsPanel; }
namespace mf::app::widgets { class AppearancePanel;    }
namespace mf::app::widgets { class SourcesSettingsPanel; }
namespace mf::app::widgets { class DownloadsSettingsPanel; }
namespace mf::app::widgets { class DiscoverSettingsPanel;   }
namespace mf::app::widgets { class ExtensionsSettingsPanel; }
namespace mf::app::widgets { class RepositoriesSettingsPanel; }

namespace mf::app::widgets {

class SettingsPage : public QWidget {
    Q_OBJECT
public:
    explicit SettingsPage(mf::core::services::LibraryService*       libSvc,
                          mf::core::services::ToastService*         toasts,
                          mf::core::services::DownloadService*      downloads,
                          mf::core::services::ExtensionManager*     extMgr,
                          mf::core::sources::StreamingSourceManager* sourceMgr,
                          mf::core::services::SettingsControl*      settings,
                          mf::core::theme::ThemeManager*            theme,
                          QWidget* parent = nullptr);
    ~SettingsPage() override = default;

private:
    void buildUi();
    void applyTheme();
    void buildStubPanel(const QString& name, const QString& blurb, int index);

    // Toast-on-save plumbing (5.5.I).
    QString prettyKey(const QString& key) const;
    void    onSettingChanged(QString key, QVariant value);
    void    flushPendingToast();

    mf::core::services::LibraryService*       libSvc_     = nullptr;
    mf::core::services::ToastService*         toasts_     = nullptr;
    mf::core::services::DownloadService*      downloads_  = nullptr;
    mf::core::services::ExtensionManager*     extMgr_     = nullptr;
    mf::core::sources::StreamingSourceManager* sourceMgr_  = nullptr;
    mf::core::services::SettingsControl*      settings_   = nullptr;
    mf::core::theme::ThemeManager*            theme_      = nullptr;

    QListWidget*      nav_    = nullptr;
    QStackedWidget*   pages_  = nullptr;

    // Debounce + dedup for the saved-toast feedback. The timer
    // coalesces bursts of set() calls into a single toast; the
    // hash suppresses repeats of the same value.
    QTimer                  toastTimer_;
    QHash<QString, QString> lastShownValue_;  // key → rendered value
    QString                 pendingKey_;

    LibrarySettingsPanel*    foldersPanel_    = nullptr;
    AppearancePanel*         appearancePanel_ = nullptr;
    SourcesSettingsPanel*    sourcesPanel_    = nullptr;
    DownloadsSettingsPanel*  downloadsPanel_  = nullptr;
    DiscoverSettingsPanel*   discoverPanel_   = nullptr;
    ExtensionsSettingsPanel* extensionsPanel_ = nullptr;
    RepositoriesSettingsPanel* repositoriesPanel_ = nullptr;
};

} // namespace mf::app::widgets

// LibrarySettingsPanel.h
// One of the six settings sub-panels. Lets the user manage the
// root folders the library scanner watches, and force a rescan.
//
// Lays out as a vertical stack:
//   * Title
//   * QListView bound to the LibraryService::folders list
//   * "Add folder…", "Remove selected", "Rescan all" buttons
//
// All mutating actions go through LibraryService, so the persistence
// + scan lifecycle is in one place. The panel listens to the
// service's signals to keep its list in sync with reality (e.g. the
// folder list is pruned on construction if a path no longer exists).

#pragma once

#include <QWidget>
#include <QStringList>

class QLabel;
class QListView;
class QPushButton;
class QStandardItemModel;

namespace mf::core::services { class LibraryService; }
namespace mf::core::services { class ToastService; }
namespace mf::core::theme     { class ThemeManager; }

namespace mf::app::widgets {

class LibrarySettingsPanel : public QWidget {
    Q_OBJECT
public:
    LibrarySettingsPanel(mf::core::services::LibraryService* libSvc,
                         mf::core::services::ToastService*   toasts,
                         mf::core::theme::ThemeManager*      theme,
                         QWidget* parent = nullptr);
    ~LibrarySettingsPanel() override = default;

private slots:
    void onFoldersChanged();
    void onAddClicked();
    void onRemoveClicked();
    void onRescanClicked();
    void onSelectionChanged();
    void onScanStarted();
    void onScanFinished(int added, int updated);

private:
    void buildUi();
    void applyTheme();
    void syncModelFromService();

    mf::core::services::LibraryService* libSvc_  = nullptr;
    mf::core::services::ToastService*   toasts_  = nullptr;
    mf::core::theme::ThemeManager*      theme_   = nullptr;

    QLabel*              title_       = nullptr;
    QLabel*              subtitle_    = nullptr;
    QListView*           folderList_  = nullptr;
    QStandardItemModel*  folderModel_ = nullptr;
    QPushButton*         addBtn_      = nullptr;
    QPushButton*         removeBtn_   = nullptr;
    QPushButton*         rescanBtn_   = nullptr;
};

} // namespace mf::app::widgets

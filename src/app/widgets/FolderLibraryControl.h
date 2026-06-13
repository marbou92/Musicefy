// FolderLibraryControl.h
// Full folder browser UI for the music library. Shows a tree view of
// scanned folders with file counts, a breadcrumb path bar for
// navigation, and grid/list toggle for viewing music files within
// a selected folder. Integrates with LibraryService for scanning.

#pragma once

#include <QWidget>

class QListView;
class QTreeView;
class QStandardItemModel;
class QStandardItem;
class QSplitter;
class QLabel;
class QPushButton;

namespace mf::core::services { class LibraryService; class NavigationService; class ToastService; }
namespace mf::core::theme   { class ThemeManager; }

namespace mf::app::widgets {

class FolderLibraryControl : public QWidget {
    Q_OBJECT
public:
    FolderLibraryControl(mf::core::services::LibraryService*  libSvc,
                         mf::core::services::NavigationService* nav,
                         mf::core::services::ToastService*    toasts,
                         mf::core::theme::ThemeManager*       theme,
                         QWidget* parent = nullptr);
    ~FolderLibraryControl() override = default;

    /// Refresh the folder tree from the database.
    void refresh();

private slots:
    void onFolderSelected(const QModelIndex& index);
    void onFileDoubleClicked(const QModelIndex& index);
    void onToggleViewMode();
    void onRescanFolder();
    void onAddFolder();
    void onFoldersChanged();
    void onThemeChanged();

private:
    void buildUi();
    void applyTheme();
    void populateFolderTree();
    void populateFileList(const QString& folderPath);
    QString formatSize(qint64 bytes) const;
    QStandardItem* findFolderItem(const QString& path) const;

    mf::core::services::LibraryService*    libSvc_   = nullptr;
    mf::core::services::NavigationService* nav_      = nullptr;
    mf::core::services::ToastService*      toasts_   = nullptr;
    mf::core::theme::ThemeManager*         theme_    = nullptr;

    QSplitter*       splitter_    = nullptr;
    QTreeView*       folderTree_  = nullptr;
    QStandardItemModel* folderModel_ = nullptr;
    QListView*       fileList_    = nullptr;
    QStandardItemModel* fileModel_ = nullptr;

    QLabel*          pathLabel_   = nullptr;
    QLabel*          statusLabel_ = nullptr;
    QPushButton*     viewModeBtn_ = nullptr;
    QPushButton*     rescanBtn_   = nullptr;
    QPushButton*     addFolderBtn_ = nullptr;

    bool             gridView_ = false;
    QString          currentFolder_;
};

} // namespace mf::app::widgets

// FolderLibraryControl.cpp
// See header.

#include "FolderLibraryControl.h"

#include "../core/models/MusicFile.h"
#include "../core/services/LibraryService.h"
#include "../core/services/NavigationService.h"
#include "../core/services/ToastService.h"
#include "../core/theme/MusicefyColorScheme.h"
#include "../core/theme/ThemeManager.h"

#include <QDir>
#include <QFileDialog>
#include <QFileInfo>
#include <QFont>
#include <QHBoxLayout>
#include <QHeaderView>
#include <QLabel>
#include <QListView>
#include <QPushButton>
#include <QStandardItem>
#include <QStandardItemModel>
#include <QSplitter>
#include <QVBoxLayout>
#include <QTreeView>

namespace mf::app::widgets {

using mf::core::models::MusicFile;
using mf::core::services::LibraryService;
using mf::core::services::NavigationService;
using mf::core::services::ToastService;
using mf::core::theme::ThemeManager;
using mf::core::theme::MusicefyColorScheme;

FolderLibraryControl::FolderLibraryControl(LibraryService*    libSvc,
                                           NavigationService* nav,
                                           ToastService*      toasts,
                                           ThemeManager*      theme,
                                           QWidget*           parent)
    : QWidget(parent)
    , libSvc_(libSvc)
    , nav_(nav)
    , toasts_(toasts)
    , theme_(theme)
{
    buildUi();
    applyTheme();
    populateFolderTree();

    if (libSvc_) {
        connect(libSvc_, &LibraryService::foldersChanged,
                this, &FolderLibraryControl::onFoldersChanged);
    }
    if (theme_) {
        connect(theme_, &ThemeManager::schemeChanged,
                this, &FolderLibraryControl::onThemeChanged);
    }
}

void FolderLibraryControl::buildUi() {
    auto* root = new QVBoxLayout(this);
    root->setContentsMargins(0, 0, 0, 0);
    root->setSpacing(0);

    // ── Header bar ────────────────────────────────────────
    auto* headerBar = new QWidget(this);
    auto* hl = new QHBoxLayout(headerBar);
    hl->setContentsMargins(28, 16, 28, 8);
    hl->setSpacing(12);

    auto* titleLabel = new QLabel(QStringLiteral("Folders"), headerBar);
    QFont tf = titleLabel->font();
    tf.setPointSize(18);
    tf.setBold(true);
    titleLabel->setFont(tf);
    hl->addWidget(titleLabel);
    hl->addStretch(1);

    viewModeBtn_ = new QPushButton(QStringLiteral("Grid"), headerBar);
    viewModeBtn_->setCursor(Qt::PointingHandCursor);
    connect(viewModeBtn_, &QPushButton::clicked,
            this, &FolderLibraryControl::onToggleViewMode);
    hl->addWidget(viewModeBtn_);

    rescanBtn_ = new QPushButton(QStringLiteral("Rescan"), headerBar);
    rescanBtn_->setCursor(Qt::PointingHandCursor);
    connect(rescanBtn_, &QPushButton::clicked,
            this, &FolderLibraryControl::onRescanFolder);
    hl->addWidget(rescanBtn_);

    addFolderBtn_ = new QPushButton(QStringLiteral("+ Add"), headerBar);
    addFolderBtn_->setCursor(Qt::PointingHandCursor);
    connect(addFolderBtn_, &QPushButton::clicked,
            this, &FolderLibraryControl::onAddFolder);
    hl->addWidget(addFolderBtn_);

    root->addWidget(headerBar);

    // ── Path breadcrumb ───────────────────────────────────
    auto* pathBar = new QWidget(this);
    auto* pl = new QHBoxLayout(pathBar);
    pl->setContentsMargins(28, 0, 28, 4);

    pathLabel_ = new QLabel(QStringLiteral("All Folders"), pathBar);
    QFont pf = pathLabel_->font();
    pf.setPointSize(10);
    pathLabel_->setFont(pf);
    pathLabel_->setStyleSheet(QStringLiteral("color: rgba(128,128,128,200);"));
    pl->addWidget(pathLabel_);
    pl->addStretch(1);

    statusLabel_ = new QLabel(pathBar);
    QFont sf = statusLabel_->font();
    sf.setPointSize(10);
    statusLabel_->setFont(sf);
    statusLabel_->setStyleSheet(QStringLiteral("color: rgba(128,128,128,200);"));
    pl->addWidget(statusLabel_);

    root->addWidget(pathBar);

    // ── Splitter: folder tree | file list ─────────────────
    splitter_ = new QSplitter(Qt::Horizontal, this);
    splitter_->setChildrenCollapsible(false);

    // Folder tree (left pane)
    auto* treeWidget = new QWidget(splitter_);
    auto* treeLayout = new QVBoxLayout(treeWidget);
    treeLayout->setContentsMargins(20, 4, 0, 4);

    folderTree_ = new QTreeView(treeWidget);
    folderModel_ = new QStandardItemModel(this);
    folderTree_->setModel(folderModel_);
    folderTree_->setHeaderHidden(true);
    folderTree_->setEditTriggers(QAbstractItemView::NoEditTriggers);
    folderTree_->setSelectionMode(QAbstractItemView::SingleSelection);
    folderTree_->setRootIsDecorated(true);
    folderTree_->setIndentation(20);
    connect(folderTree_, &QTreeView::clicked,
            this, &FolderLibraryControl::onFolderSelected);
    treeLayout->addWidget(folderTree_, 1);

    splitter_->addWidget(treeWidget);

    // File list (right pane)
    auto* listWidget = new QWidget(splitter_);
    auto* listLayout = new QVBoxLayout(listWidget);
    listLayout->setContentsMargins(0, 4, 20, 4);

    fileList_ = new QListView(listWidget);
    fileModel_ = new QStandardItemModel(this);
    fileList_->setModel(fileModel_);
    fileList_->setUniformItemSizes(true);
    fileList_->setEditTriggers(QAbstractItemView::NoEditTriggers);
    fileList_->setSelectionMode(QAbstractItemView::SingleSelection);
    fileList_->setAlternatingRowColors(true);
    connect(fileList_, &QListView::doubleClicked,
            this, &FolderLibraryControl::onFileDoubleClicked);
    listLayout->addWidget(fileList_, 1);

    splitter_->addWidget(listWidget);
    splitter_->setStretchFactor(0, 1);
    splitter_->setStretchFactor(1, 3);

    root->addWidget(splitter_, 1);
}

void FolderLibraryControl::applyTheme() {
    MusicefyColorScheme s;
    if (theme_) s = theme_->scheme();
    if (!s.primary.isValid()) {
        setStyleSheet(QString());
        return;
    }
    setStyleSheet(QStringLiteral(
        "QWidget { background: transparent; color: %1; }"
        "QTreeView, QListView { background: %4; color: %1;"
        "  border: none; border-radius: 6px;"
        "  selection-background-color: %7; selection-color: %8; }"
        "QTreeView::item, QListView::item { padding: 4px 6px; }"
        "QSplitter::handle { background: %3; width: 1px; }"
        "QPushButton { background: %5; color: %1;"
        "  border: 1px solid %3; border-radius: 6px; padding: 5px 12px; }"
        "QPushButton:hover { background: %6; }"
    )
    .arg(s.onSurface.name())
    .arg(s.onSurfaceVariant.name())
    .arg(s.outlineVariant.name())
    .arg(s.surfaceContainer.name())
    .arg(s.surfaceContainerHigh.name())
    .arg(s.surfaceContainerHighest.name())
    .arg(s.primaryContainer.name())
    .arg(s.onPrimaryContainer.name())
    );
}

void FolderLibraryControl::onThemeChanged() {
    applyTheme();
}

void FolderLibraryControl::refresh() {
    populateFolderTree();
    if (!currentFolder_.isEmpty()) {
        populateFileList(currentFolder_);
    }
}

void FolderLibraryControl::populateFolderTree() {
    folderModel_->clear();

    if (!libSvc_) return;

    const auto folders = libSvc_->folders();
    if (folders.isEmpty()) {
        auto* item = new QStandardItem(QStringLiteral("No folders configured"));
        item->setEnabled(false);
        folderModel_->appendRow(item);
        return;
    }

    for (const auto& folder : folders) {
        auto* item = new QStandardItem(QFileInfo(folder).fileName());
        item->setData(folder, Qt::UserRole);
        item->setToolTip(folder);
        folderModel_->appendRow(item);

        // Count music files in the folder (one level deep).
        QDir dir(folder);
        int count = dir.entryList(QDir::Files | QDir::NoDotAndDotDot).count();
        item->setText(QStringLiteral("%1  (%2 files)")
                         .arg(QFileInfo(folder).fileName())
                         .arg(count));
    }

    folderTree_->expandAll();
}

void FolderLibraryControl::populateFileList(const QString& folderPath) {
    fileModel_->clear();
    fileModel_->setHorizontalHeaderLabels(
        {QStringLiteral("Name"), QStringLiteral("Size")});

    currentFolder_ = folderPath;
    pathLabel_->setText(folderPath);

    QDir dir(folderPath);
    if (!dir.exists()) {
        statusLabel_->setText(QStringLiteral("Folder not found"));
        return;
    }

    // List subdirectories first, then music files.
    QFileInfoList entries = dir.entryInfoList(
        QDir::Dirs | QDir::Files | QDir::NoDotAndDotDot,
        QDir::DirsFirst | QDir::Name);

    int fileCount = 0;
    for (const auto& info : entries) {
        if (info.isDir()) {
            auto* item = new QStandardItem(
                QStringLiteral("📁 %1").arg(info.fileName()));
            item->setData(info.absoluteFilePath(), Qt::UserRole);
            fileModel_->appendRow(item);
        } else {
            // Only show recognized music files.
            QString ext = info.suffix().toLower();
            if (QStringLiteral("mp3,flac,ogg,opus,wav,wma,m4a,aac,ape,wv,dsf,dff")
                    .contains(ext)) {
                auto* item = new QStandardItem(info.fileName());
                item->setData(info.absoluteFilePath(), Qt::UserRole);
                fileModel_->appendRow({
                    new QStandardItem(info.fileName()),
                    new QStandardItem(formatSize(info.size()))
                });
                ++fileCount;
            }
        }
    }

    statusLabel_->setText(QStringLiteral("%1 files").arg(fileCount));
}

QString FolderLibraryControl::formatSize(qint64 bytes) const {
    if (bytes < 1024) return QStringLiteral("%1 B").arg(bytes);
    if (bytes < 1024 * 1024)
        return QStringLiteral("%1 KB").arg(bytes / 1024.0, 0, 'f', 1);
    if (bytes < 1024 * 1024 * 1024)
        return QStringLiteral("%1 MB").arg(bytes / (1024.0 * 1024.0), 0, 'f', 1);
    return QStringLiteral("%1 GB").arg(bytes / (1024.0 * 1024.0 * 1024.0), 0, 'f', 2);
}

QStandardItem* FolderLibraryControl::findFolderItem(const QString& path) const {
    for (int i = 0; i < folderModel_->rowCount(); ++i) {
        auto* item = folderModel_->item(i);
        if (item && item->data(Qt::UserRole).toString() == path)
            return item;
    }
    return nullptr;
}

void FolderLibraryControl::onFolderSelected(const QModelIndex& index) {
    if (!index.isValid()) return;
    auto* item = folderModel_->itemFromIndex(index);
    if (!item) return;

    QString path = item->data(Qt::UserRole).toString();
    if (!path.isEmpty()) {
        populateFileList(path);
    }
}

void FolderLibraryControl::onFileDoubleClicked(const QModelIndex& index) {
    if (!index.isValid()) return;

    auto* item = fileModel_->itemFromIndex(index);
    if (!item) return;

    QString itemData = item->data(Qt::UserRole).toString();

    // If it's a subdirectory, navigate into it.
    if (QFileInfo(itemData).isDir()) {
        populateFileList(itemData);
        return;
    }

    // Otherwise, it's a music file — play it.
    if (nav_) {
        // Build a MusicFile from the path and play it.
        MusicFile mf;
        mf.setFilePath(itemData);
        mf.setTitle(QFileInfo(itemData).completeBaseName());
        // Navigate or play directly through the library view.
    }
}

void FolderLibraryControl::onToggleViewMode() {
    gridView_ = !gridView_;
    viewModeBtn_->setText(gridView_ ? QStringLiteral("List") : QStringLiteral("Grid"));

    if (gridView_) {
        fileList_->setViewMode(QListView::IconMode);
        fileList_->setGridSize(QSize(140, 180));
        fileList_->setResizeMode(QListView::Adjust);
        fileList_->setSpacing(8);
        fileList_->setWrapping(true);
    } else {
        fileList_->setViewMode(QListView::ListMode);
        fileList_->setGridSize(QSize());
        fileList_->setWrapping(false);
        fileList_->setSpacing(0);
    }
}

void FolderLibraryControl::onRescanFolder() {
    if (!libSvc_) return;
    if (currentFolder_.isEmpty()) {
        if (toasts_) toasts_->showInfo(
            QStringLiteral("Rescan"),
            QStringLiteral("Select a folder first."));
        return;
    }
    libSvc_->rescan();
    if (toasts_) toasts_->showInfo(
        QStringLiteral("Rescan"),
        QStringLiteral("Rescanning library folders…"));
}

void FolderLibraryControl::onAddFolder() {
    if (!libSvc_) return;
    QString dir = QFileDialog::getExistingDirectory(
        this, QStringLiteral("Add Music Folder"));
    if (dir.isEmpty()) return;

    libSvc_->addFolder(dir);
    if (toasts_) toasts_->showSuccess(
        QStringLiteral("Folder Added"),
        QStringLiteral("Added: %1").arg(dir));
}

void FolderLibraryControl::onFoldersChanged() {
    populateFolderTree();
}

} // namespace mf::app::widgets

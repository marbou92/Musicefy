// LibrarySettingsPanel.cpp
// See header.

#include "LibrarySettingsPanel.h"

#include "../core/services/LibraryService.h"
#include "../core/services/ToastService.h"
#include "../core/theme/MusicefyColorScheme.h"
#include "../core/theme/ThemeManager.h"

#include <QFileDialog>
#include <QFont>
#include <QHBoxLayout>
#include <QLabel>
#include <QListView>
#include <QPushButton>
#include <QStandardItem>
#include <QStandardItemModel>
#include <QVBoxLayout>

namespace mf::app::widgets {

using mf::core::services::LibraryService;
using mf::core::services::ToastService;
using mf::core::theme::ThemeManager;
using mf::core::theme::MusicefyColorScheme;

LibrarySettingsPanel::LibrarySettingsPanel(LibraryService* libSvc,
                                           ToastService*   toasts,
                                           ThemeManager*   theme,
                                           QWidget*        parent)
    : QWidget(parent)
    , libSvc_(libSvc)
    , toasts_(toasts)
    , theme_(theme)
{
    buildUi();
    applyTheme();
    syncModelFromService();

    if (libSvc_) {
        connect(libSvc_, &LibraryService::foldersChanged,
                this, &LibrarySettingsPanel::onFoldersChanged);
        connect(libSvc_, &LibraryService::scanStarted,
                this, &LibrarySettingsPanel::onScanStarted);
        connect(libSvc_, &LibraryService::scanFinished,
                this, &LibrarySettingsPanel::onScanFinished);
    }
    if (theme_) {
        connect(theme_, &ThemeManager::schemeChanged,
                this, &LibrarySettingsPanel::applyTheme);
    }
}

void LibrarySettingsPanel::buildUi() {
    auto* root = new QVBoxLayout(this);
    root->setContentsMargins(0, 0, 0, 0);
    root->setSpacing(12);

    title_ = new QLabel(QStringLiteral("Music folders"), this);
    QFont tf = title_->font();
    tf.setPointSize(16);
    tf.setBold(true);
    title_->setFont(tf);
    root->addWidget(title_);

    subtitle_ = new QLabel(
        QStringLiteral("Choose the folders Musicefy should watch. New and changed files are indexed automatically; deleted files are dropped from the library on the next rescan."),
        this);
    subtitle_->setWordWrap(true);
    subtitle_->setProperty("role", QStringLiteral("secondary"));
    root->addWidget(subtitle_);

    folderModel_ = new QStandardItemModel(this);
    folderList_  = new QListView(this);
    folderList_->setModel(folderModel_);
    folderList_->setSelectionMode(QAbstractItemView::SingleSelection);
    folderList_->setEditTriggers(QAbstractItemView::NoEditTriggers);
    folderList_->setUniformItemSizes(true);
    connect(folderList_->selectionModel(), &QItemSelectionModel::selectionChanged,
            this, &LibrarySettingsPanel::onSelectionChanged);
    root->addWidget(folderList_, /*stretch=*/1);

    // ── Action buttons ─────────────────────────────────────────────
    auto* buttons = new QHBoxLayout;
    buttons->setContentsMargins(0, 0, 0, 0);
    buttons->setSpacing(8);

    addBtn_ = new QPushButton(QStringLiteral("Add folder…"), this);
    connect(addBtn_, &QPushButton::clicked,
            this, &LibrarySettingsPanel::onAddClicked);
    buttons->addWidget(addBtn_);

    removeBtn_ = new QPushButton(QStringLiteral("Remove selected"), this);
    connect(removeBtn_, &QPushButton::clicked,
            this, &LibrarySettingsPanel::onRemoveClicked);
    buttons->addWidget(removeBtn_);

    rescanBtn_ = new QPushButton(QStringLiteral("Rescan all"), this);
    connect(rescanBtn_, &QPushButton::clicked,
            this, &LibrarySettingsPanel::onRescanClicked);
    buttons->addWidget(rescanBtn_);

    buttons->addStretch(1);
    root->addLayout(buttons);

    onSelectionChanged();
}

void LibrarySettingsPanel::applyTheme() {
    MusicefyColorScheme s;
    if (theme_) s = theme_->scheme();
    if (!s.primary.isValid()) {
        setStyleSheet(QString());
        return;
    }
    setStyleSheet(QStringLiteral(
        "QWidget { background: transparent; color: %1; }"
        "QLabel[role=\"secondary\"] { color: %2; }"
        "QListView { background: %3; border: 1px solid %4;"
        "  border-radius: 8px; padding: 4px; }"
        "QListView::item:selected { background: %5; color: %6;"
        "  border-radius: 4px; }"
        "QPushButton { background: %7; color: %8;"
        "  border: 1px solid %4; border-radius: 6px;"
        "  padding: 6px 14px; }"
        "QPushButton:hover { background: %9; }"
        "QPushButton:disabled { color: %10; }"
    )
    .arg(s.onSurface.name())
    .arg(s.onSurfaceVariant.name())
    .arg(s.surfaceContainer.name())
    .arg(s.outlineVariant.name())
    .arg(s.primaryContainer.name())
    .arg(s.onPrimaryContainer.name())
    .arg(s.surfaceContainerHigh.name())
    .arg(s.onSurface.name())
    .arg(s.surfaceContainerHighest.name())
    .arg(s.onSurfaceVariant.name())
    );
}

void LibrarySettingsPanel::syncModelFromService() {
    if (!folderModel_) return;
    folderModel_->clear();
    if (!libSvc_) return;
    const QStringList folders = libSvc_->folders();
    for (const QString& path : folders) {
        auto* item = new QStandardItem(path);
        item->setEditable(false);
        item->setToolTip(path);
        folderModel_->appendRow(item);
    }
    onSelectionChanged();
}

void LibrarySettingsPanel::onFoldersChanged() {
    syncModelFromService();
}

void LibrarySettingsPanel::onAddClicked() {
    if (!libSvc_) return;
    const QString start = libSvc_->folders().isEmpty()
        ? QDir::homePath()
        : libSvc_->folders().first();
    QString dir = QFileDialog::getExistingDirectory(
        this, QStringLiteral("Add music folder"), start);
    if (dir.isEmpty()) return;
    if (!libSvc_->addFolder(dir)) {
        if (toasts_) {
            toasts_->showError(
                QStringLiteral("Couldn't add folder"),
                QStringLiteral("%1 is not a valid directory, or it's already in your library.").arg(dir));
        }
    } else if (toasts_) {
        toasts_->showInfo(
            QStringLiteral("Folder added"),
            QStringLiteral("Scanning %1…").arg(dir));
    }
}

void LibrarySettingsPanel::onRemoveClicked() {
    if (!libSvc_) return;
    auto idx = folderList_->currentIndex();
    if (!idx.isValid()) return;
    const QString path = folderModel_->item(idx.row())->text();
    if (!libSvc_->removeFolder(path)) return;
    if (toasts_) {
        toasts_->showInfo(
            QStringLiteral("Folder removed"),
            QStringLiteral("Stopped watching %1. Any tracks from that folder have been removed from the library.").arg(path));
    }
}

void LibrarySettingsPanel::onRescanClicked() {
    if (!libSvc_) return;
    if (libSvc_->isScanning()) {
        if (toasts_) {
            toasts_->showWarning(
                QStringLiteral("Already scanning"),
                QStringLiteral("A library scan is already in progress."));
        }
        return;
    }
    if (libSvc_->folders().isEmpty()) {
        if (toasts_) {
            toasts_->showWarning(
                QStringLiteral("Nothing to scan"),
                QStringLiteral("Add at least one folder first."));
        }
        return;
    }
    libSvc_->rescan();
}

void LibrarySettingsPanel::onSelectionChanged() {
    if (!removeBtn_) return;
    const bool has = folderList_ && folderList_->currentIndex().isValid();
    removeBtn_->setEnabled(has);
}

void LibrarySettingsPanel::onScanStarted() {
    if (rescanBtn_) rescanBtn_->setEnabled(false);
}

void LibrarySettingsPanel::onScanFinished(int /*added*/, int /*updated*/) {
    if (rescanBtn_) rescanBtn_->setEnabled(true);
}

} // namespace mf::app::widgets

// PlaylistPickerDialog.cpp

#include "PlaylistPickerDialog.h"

#include "../viewmodels/LibraryViewModel.h"
#include "../../core/models/PlaylistInfo.h"
#include "../../core/theme/ThemeManager.h"
#include "../../core/theme/MusicefyColorScheme.h"

#include <QVBoxLayout>
#include <QHBoxLayout>
#include <QListView>
#include <QStandardItemModel>
#include <QPushButton>
#include <QLabel>
#include <QInputDialog>

namespace mf::app::widgets {

using mf::app::viewmodels::LibraryViewModel;
using mf::core::theme::ThemeManager;
using mf::core::theme::MusicefyColorScheme;

PlaylistPickerDialog::PlaylistPickerDialog(LibraryViewModel* libVm,
                                           ThemeManager*     theme,
                                           QWidget*          parent)
    : QDialog(parent)
    , libVm_(libVm)
    , theme_(theme)
{
    setWindowTitle(QStringLiteral("Add to Playlist"));
    setMinimumSize(360, 420);
    buildUi();
    applyTheme();
    refreshList();
}

// ──────────────────────────────────────────────────────────────────
void PlaylistPickerDialog::buildUi()
{
    auto* root = new QVBoxLayout(this);
    root->setContentsMargins(16, 16, 16, 16);
    root->setSpacing(12);

    auto* title = new QLabel(QStringLiteral("Choose a playlist"));
    QFont tf = title->font();
    tf.setPointSize(14);
    tf.setBold(true);
    title->setFont(tf);
    root->addWidget(title);

    listView_ = new QListView;
    listModel_ = new QStandardItemModel(this);
    listView_->setModel(listModel_);
    listView_->setEditTriggers(QAbstractItemView::NoEditTriggers);
    listView_->setSelectionMode(QAbstractItemView::SingleSelection);
    connect(listView_->selectionModel(), &QItemSelectionModel::currentChanged,
            this, &PlaylistPickerDialog::onSelectionChanged);
    root->addWidget(listView_, 1);

    // Bottom row
    auto* bottomRow = new QHBoxLayout;
    newPlaylistBtn_ = new QPushButton(QStringLiteral("+ New Playlist"));
    connect(newPlaylistBtn_, &QPushButton::clicked,
            this, &PlaylistPickerDialog::onNewPlaylistClicked);
    bottomRow->addWidget(newPlaylistBtn_);
    bottomRow->addStretch();

    auto* cancelBtn = new QPushButton(QStringLiteral("Cancel"));
    connect(cancelBtn, &QPushButton::clicked, this, &QDialog::reject);
    bottomRow->addWidget(cancelBtn);

    auto* addBtn = new QPushButton(QStringLiteral("Add"));
    addBtn->setDefault(true);
    addBtn->setEnabled(false);
    connect(addBtn, &QPushButton::clicked,
            this, &PlaylistPickerDialog::onAccepted);
    bottomRow->addWidget(addBtn);

    // Store add button reference for enable/disable
    addBtn->setObjectName(QStringLiteral("addBtn"));

    root->addLayout(bottomRow);
}

// ──────────────────────────────────────────────────────────────────
void PlaylistPickerDialog::applyTheme()
{
    MusicefyColorScheme s;
    if (theme_) s = theme_->scheme();
    if (!s.surface.isValid()) return;

    setStyleSheet(QStringLiteral(
        "QDialog { background: %1; color: %2; }"
        "QListView {"
        "  background: %3; color: %2; border: 1px solid %4;"
        "  border-radius: 8px; outline: none;"
        "  selection-background-color: %5; selection-color: %6;"
        "}"
        "QListView::item { padding: 10px 12px; border-radius: 6px; margin: 2px 4px; }"
        "QListView::item:hover { background: %7; }"
        "QPushButton {"
        "  background: %3; color: %2; border: 1px solid %4;"
        "  border-radius: 8px; padding: 8px 16px; font-size: 13px;"
        "}"
        "QPushButton:hover { background: %7; }"
        "QPushButton:disabled { color: %8; }"
        "QPushButton[objectName=\"addBtn\"] {"
        "  background: %5; color: %6; border: 1px solid %5; }"
        "QPushButton[objectName=\"addBtn\"]:hover {"
        "  background: %9; border: 1px solid %9; }"
        "QLabel { color: %2; }"
    )
    .arg(s.surface.name(),
         s.onSurface.name(),
         s.surfaceContainerHigh.name(),
         s.outlineVariant.name(),
         s.primary.name(),
         s.onPrimary.name(),
         s.surfaceContainerHighest.name(),
         s.onSurfaceVariant.name(),
         s.primaryContainer.name()));
}

// ──────────────────────────────────────────────────────────────────
void PlaylistPickerDialog::refreshList()
{
    listModel_->clear();
    if (!libVm_) return;

    const auto playlists = libVm_->playlists();
    for (const auto& pl : playlists) {
        auto* item = new QStandardItem(
            QStringLiteral("%1  (%2 tracks)").arg(pl.name()).arg(pl.trackCount()));
        item->setData(pl.id(), Qt::UserRole);
        item->setData(pl.name(), Qt::UserRole + 1);
        listModel_->appendRow(item);
    }
}

// ──────────────────────────────────────────────────────────────────
void PlaylistPickerDialog::onSelectionChanged()
{
    const auto idx = listView_->currentIndex();
    auto* addBtn = findChild<QPushButton*>(QStringLiteral("addBtn"));
    if (addBtn) addBtn->setEnabled(idx.isValid());

    if (idx.isValid()) {
        auto* item = listModel_->itemFromIndex(idx);
        selectedId_ = item->data(Qt::UserRole).toString();
        selectedName_ = item->data(Qt::UserRole + 1).toString();
    } else {
        selectedId_.clear();
        selectedName_.clear();
    }
}

// ──────────────────────────────────────────────────────────────────
void PlaylistPickerDialog::onNewPlaylistClicked()
{
    bool ok = false;
    const QString name = QInputDialog::getText(
        this, QStringLiteral("New Playlist"),
        QStringLiteral("Playlist name:"),
        QLineEdit::Normal, QString(), &ok);

    if (!ok || name.trimmed().isEmpty()) return;

    if (libVm_) {
        libVm_->createPlaylist(name.trimmed());
        refreshList();

        // Select the newly created playlist
        for (int i = 0; i < listModel_->rowCount(); ++i) {
            auto* item = listModel_->item(i);
            if (item->data(Qt::UserRole + 1).toString() == name.trimmed()) {
                listView_->setCurrentIndex(listModel_->index(i, 0));
                break;
            }
        }
    }
}

// ──────────────────────────────────────────────────────────────────
void PlaylistPickerDialog::onAccepted()
{
    if (selectedId_.isEmpty()) return;
    accept();
}

} // namespace mf::app::widgets

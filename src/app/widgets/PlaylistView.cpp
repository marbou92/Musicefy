// PlaylistView.cpp
// See header. The widget binds to PlaylistViewModel and renders
// the playlist. All queue work is delegated to the view model.
//
// Edit operations exposed in the UI (5.5.B / B.1-B.4):
//   - Rename        — header button → QInputDialog → vm_->rename()
//   - Add tracks…   — header button → QFileDialog   → vm_->addTrack()
//   - Drag-reorder  — QListView::InternalMove       → vm_->reorder()
//   - Inline remove — click the ✕ cell              → vm_->removeTrackAt()

#include "PlaylistView.h"

#include "CoverImage.h"
#include "SvgIcon.h"
#include "PlaylistPickerDialog.h"

#include "../core/database/LibraryRepository.h"
#include "../core/models/MusicFile.h"
#include "../core/playback/QueueManager.h"
#include "../core/services/ImageCache.h"
#include "../core/services/NavigationService.h"
#include "../core/services/ToastService.h"
#include "../core/theme/MusicefyColorScheme.h"
#include "../core/theme/ThemeManager.h"
#include "../viewmodels/PlaylistViewModel.h"
#include "../viewmodels/LibraryViewModel.h"

#include <QFileDialog>
#include <QFileInfo>
#include <QFont>
#include <QHBoxLayout>
#include <QInputDialog>
#include <QLabel>
#include <QLineEdit>
#include <QListView>
#include <QMessageBox>
#include <QPushButton>
#include <QStandardItem>
#include <QStandardItemModel>
#include <QVBoxLayout>

namespace mf::app::widgets {

using mf::core::database::LibraryRepository;
using mf::core::models::MusicFile;
using mf::core::models::PlaylistInfo;
using mf::core::playback::QueueManager;
using mf::core::services::ImageCache;
using mf::core::services::NavigationService;
using mf::core::services::ToastService;
using mf::core::theme::ThemeManager;
using mf::core::theme::MusicefyColorScheme;
using mf::app::viewmodels::PlaylistViewModel;
using mf::app::viewmodels::LibraryViewModel;

namespace {

constexpr int kColNumber   = 0;
constexpr int kColTitle    = 1;
constexpr int kColArtist   = 2;
constexpr int kColDuration = 3;
constexpr int kColRemove   = 4;
constexpr int kColCount    = 5;

QString formatDuration(std::chrono::seconds s) {
    qint64 total = s.count();
    if (total <= 0) return QStringLiteral("--:--");
    return QStringLiteral("%1:%2")
        .arg(total / 60)
        .arg(total % 60, 2, 10, QLatin1Char('0'));
}

// Audio-file filter used by both the Add-tracks dialog and the
// drag-drop handler. Mirrors the formats the v1 LibraryScanner
// recognised.
QString audioFileFilter() {
    return QStringLiteral(
        "Audio files (*.mp3 *.flac *.wav *.ogg *.m4a *.aac *.wma "
        "*.ape *.mpc *.wv *.aiff *.dsf);;All files (*.*)");
}

} // anonymous namespace

PlaylistView::PlaylistView(PlaylistViewModel* vm,
                           NavigationService* nav,
                           QueueManager*     queue,
                           ImageCache*       imageCache,
                           LibraryRepository* repo,
                           ThemeManager*     theme,
                           LibraryViewModel* libVm,
                           QWidget*          parent)
    : QWidget(parent)
    , vm_(vm)
    , nav_(nav)
    , queue_(queue)
    , imageCache_(imageCache)
    , repo_(repo)
    , theme_(theme)
    , libVm_(libVm)
{
    buildUi();
    applyTheme();
    if (theme_) {
        connect(theme_, &ThemeManager::schemeChanged,
                this, [this]() {
                    applyTheme();
                    if (coverLabel_) coverLabel_->refreshPlaceholder();
                });
    }
    if (queue_) {
        connect(queue_, &QueueManager::shuffleChangedQ,
                this, &PlaylistView::onShuffleChanged);
    }
    if (vm_) {
        connect(vm_, &PlaylistViewModel::infoChanged,
                this, &PlaylistView::onVmInfoChanged);
        connect(vm_, &PlaylistViewModel::tracksChanged,
                this, &PlaylistView::onVmTracksChanged);
        connect(vm_, &PlaylistViewModel::errorReported,
                this, &PlaylistView::onVmErrorReported);
        renderPlaylist();
    }
}

void PlaylistView::buildUi() {
    auto* root = new QVBoxLayout(this);
    root->setContentsMargins(28, 18, 28, 28);
    root->setSpacing(12);

    auto* topRow = new QHBoxLayout();
    topRow->setSpacing(8);
    backBtn_ = new QPushButton(this);
    backBtn_->setText(QStringLiteral("Back"));
    backBtn_->setCursor(Qt::PointingHandCursor);
    backBtn_->setFlat(true);
    backBtn_->setIconSize(QSize(18, 18));
    connect(backBtn_, &QPushButton::clicked,
            this, &PlaylistView::onBackClicked);
    topRow->addWidget(backBtn_, /*stretch=*/0);

    topRow->addStretch(1);

    renameBtn_ = new QPushButton(this);
    renameBtn_->setText(QStringLiteral("  Rename"));
    renameBtn_->setCursor(Qt::PointingHandCursor);
    renameBtn_->setObjectName(QStringLiteral("secondary"));
    renameBtn_->setIconSize(QSize(16, 16));
    connect(renameBtn_, &QPushButton::clicked,
            this, &PlaylistView::onRenameClicked);
    topRow->addWidget(renameBtn_, /*stretch=*/0);

    addTracksBtn_ = new QPushButton(this);
    addTracksBtn_->setText(QStringLiteral("  Add tracks…"));
    addTracksBtn_->setCursor(Qt::PointingHandCursor);
    addTracksBtn_->setObjectName(QStringLiteral("secondary"));
    addTracksBtn_->setIconSize(QSize(16, 16));
    connect(addTracksBtn_, &QPushButton::clicked,
            this, &PlaylistView::onAddTracksClicked);
    topRow->addWidget(addTracksBtn_, /*stretch=*/0);

    if (libVm_) {
        fromPlaylistBtn_ = new QPushButton(this);
        fromPlaylistBtn_->setText(QStringLiteral("  From playlist…"));
        fromPlaylistBtn_->setCursor(Qt::PointingHandCursor);
        fromPlaylistBtn_->setObjectName(QStringLiteral("secondary"));
        fromPlaylistBtn_->setIconSize(QSize(16, 16));
        connect(fromPlaylistBtn_, &QPushButton::clicked,
                this, &PlaylistView::onFromPlaylistClicked);
        topRow->addWidget(fromPlaylistBtn_, /*stretch=*/0);
    }

    shuffleBtn_ = new QPushButton(this);
    shuffleBtn_->setText(QStringLiteral("  Shuffle"));
    shuffleBtn_->setCursor(Qt::PointingHandCursor);
    shuffleBtn_->setObjectName(QStringLiteral("secondary"));
    shuffleBtn_->setIconSize(QSize(16, 16));
    connect(shuffleBtn_, &QPushButton::clicked,
            this, &PlaylistView::onShuffleClicked);
    topRow->addWidget(shuffleBtn_, /*stretch=*/0);

    playAllBtn_ = new QPushButton(this);
    playAllBtn_->setText(QStringLiteral("  Play all"));
    playAllBtn_->setCursor(Qt::PointingHandCursor);
    playAllBtn_->setObjectName(QStringLiteral("primary"));
    playAllBtn_->setIconSize(QSize(16, 16));
    connect(playAllBtn_, &QPushButton::clicked,
            this, &PlaylistView::onPlayAllClicked);
    topRow->addWidget(playAllBtn_, /*stretch=*/0);

    root->addLayout(topRow);

    auto* headerRow = new QHBoxLayout();
    headerRow->setSpacing(20);

    coverLabel_ = new CoverImage(imageCache_, this);
    coverLabel_->setFixedSize(140, 140);
    headerRow->addWidget(coverLabel_, /*stretch=*/0);

    auto* textCol = new QVBoxLayout();
    textCol->setSpacing(6);

    titleLabel_ = new QLabel(this);
    QFont tf = titleLabel_->font();
    tf.setPointSize(22);
    tf.setBold(true);
    titleLabel_->setFont(tf);
    titleLabel_->setWordWrap(true);
    textCol->addWidget(titleLabel_);

    subtitleLabel_ = new QLabel(this);
    subtitleLabel_->setProperty("role", QStringLiteral("secondary"));
    subtitleLabel_->setWordWrap(true);
    textCol->addWidget(subtitleLabel_);

    textCol->addStretch(1);
    headerRow->addLayout(textCol, /*stretch=*/1);

    root->addLayout(headerRow);

    tracksList_  = new QListView(this);
    tracksModel_ = new QStandardItemModel(this);
    tracksList_->setModel(tracksModel_);
    tracksList_->setUniformItemSizes(true);
    tracksList_->setEditTriggers(QAbstractItemView::NoEditTriggers);
    tracksList_->setSelectionMode(QAbstractItemView::SingleSelection);
    tracksList_->setAlternatingRowColors(true);
    // Block 5.5.B / B.2: drag-reorder within the playlist.
    // InternalMove keeps the rows in the same QStandardItemModel;
    // the rowsMoved signal gives us the (from, to) pair to forward
    // to PlaylistViewModel::reorder().
    tracksList_->setDragEnabled(true);
    tracksList_->setAcceptDrops(true);
    tracksList_->setDropIndicatorShown(true);
    tracksList_->setDragDropMode(QAbstractItemView::InternalMove);
    tracksList_->setMovement(QListView::Snap);
    connect(tracksList_, &QListView::doubleClicked,
            this, &PlaylistView::onTrackDoubleClicked);
    // Inline remove (B.4): any click on the ✕ cell removes the row.
    connect(tracksList_, &QListView::clicked,
            this, &PlaylistView::onTrackListClicked);
    connect(tracksModel_, &QStandardItemModel::rowsMoved,
            this, &PlaylistView::onTracksModelRowsMoved);
    root->addWidget(tracksList_, /*stretch=*/1);
}

QString PlaylistView::formatTotalDuration(std::chrono::seconds s) const {
    qint64 total = s.count();
    if (total <= 0) return QString();
    if (total < 3600) {
        return QStringLiteral("%1 min").arg(total / 60);
    }
    return QStringLiteral("%1 hr %2 min")
        .arg(total / 3600)
        .arg((total % 3600) / 60);
}

void PlaylistView::applyTheme() {
    MusicefyColorScheme s;
    if (theme_) s = theme_->scheme();
    if (!s.surface.isValid()) {
        setStyleSheet(QString());
        return;
    }
    setStyleSheet(QStringLiteral(
        "QWidget { background: %1; color: %2; }"
        "QPushButton { background: transparent; color: %3;"
        "  border: none; padding: 4px 8px; }"
        "QPushButton:hover { color: %4; }"
        "QPushButton#primary {"
        "  background: %4; color: %5; border-radius: 6px;"
        "  padding: 8px 16px; border: none; font-weight: 600; }"
        "QPushButton#primary:hover { background: %6; }"
        "QPushButton#primary:disabled { background: %7; color: %8; }"
        "QPushButton#secondary {"
        "  background: transparent; color: %3; border-radius: 6px;"
        "  padding: 8px 14px; border: 1px solid %9; }"
        "QPushButton#secondary:hover { background: %10; color: %4; }"
        "QLabel[role=\"secondary\"] { color: %11; }"
        "QListView { background: %12; color: %2;"
        "  border: 1px solid %9; border-radius: 6px;"
        "  selection-background-color: %4; selection-color: %5;"
        "  alternate-background-color: %13; }"
    )
    .arg(s.surface.name())                  // 1  bg
    .arg(s.onSurface.name())                // 2  text
    .arg(s.onSurfaceVariant.name())         // 3  secondary text/button
    .arg(s.primary.name())                  // 4  primary accent
    .arg(s.onPrimary.name())                // 5  on-primary
    .arg(s.primaryContainer.name())         // 6  primary hover
    .arg(s.surfaceContainerHighest.name()) // 7  disabled bg
    .arg(s.onSurfaceVariant.name())         // 8  disabled text
    .arg(s.outlineVariant.name())           // 9  borders
    .arg(s.surfaceContainerHighest.name())  // 10 secondary hover
    .arg(s.onSurfaceVariant.name())         // 11 secondary text
    .arg(s.surfaceContainerHigh.name())     // 12 list bg
    .arg(s.surfaceContainer.name())         // 13 alt row
    );
    if (coverLabel_) coverLabel_->refreshPlaceholder();

    // Re-render SVG icons with the current theme colors.
    if (backBtn_) {
        backBtn_->setIcon(SvgIcon::get("arrow-left", s.onSurfaceVariant, 18));
    }
    if (renameBtn_) {
        renameBtn_->setIcon(SvgIcon::get(QStringLiteral("check"),
                                          s.onSurfaceVariant, 16));
    }
    if (addTracksBtn_) {
        addTracksBtn_->setIcon(SvgIcon::get(QStringLiteral("plus"),
                                             s.onSurfaceVariant, 16));
    }
    if (shuffleBtn_) {
        shuffleBtn_->setIcon(SvgIcon::get("shuffle", s.onSurfaceVariant, 16));
    }
    if (playAllBtn_) {
        playAllBtn_->setIcon(SvgIcon::get("play", s.onPrimary, 16));
    }
}

void PlaylistView::setPlaylist(const PlaylistInfo& info) {
    if (vm_) vm_->setInfo(info);
}

void PlaylistView::onVmInfoChanged() {
    renderPlaylist();
}

void PlaylistView::onVmTracksChanged() {
    renderPlaylist();
}

void PlaylistView::onVmErrorReported(const QString& message) {
    // Most VM errors are "Read-only playlist" when the user tries to
    // edit a YouTube playlist. Show as a non-fatal info modal — the
    // action is rejected at the model layer, the UI just informs.
    QMessageBox::information(this,
        QStringLiteral("Playlist"),
        message,
        QMessageBox::Ok);
}

void PlaylistView::renderPlaylist() {
    if (!vm_) return;
    const PlaylistInfo& info = vm_->info();
    titleLabel_->setText(info.name());
    QStringList sub;
    sub << QStringLiteral("%1 tracks").arg(vm_->trackCount());
    const QString totalDur = formatTotalDuration(
        std::chrono::seconds(vm_->totalDurationMs() / 1000));
    if (!totalDur.isEmpty()) sub << totalDur;
    subtitleLabel_->setText(sub.join(QStringLiteral("  \u2022  ")));

    if (coverLabel_) coverLabel_->setPlaceholderText(info.name());

    const bool editable = vm_->canEdit();
    if (renameBtn_)    renameBtn_->setEnabled(editable);
    if (addTracksBtn_) addTracksBtn_->setEnabled(editable);
    if (playAllBtn_)   playAllBtn_->setEnabled(vm_->canPlay());
    if (shuffleBtn_)   shuffleBtn_->setEnabled(vm_->canPlay());

    refreshTrackModel();
}

void PlaylistView::refreshTrackModel() {
    if (!tracksModel_) return;
    // Block signal emission while we tear the model down + rebuild
    // it, otherwise the rowsMoved handler fires with garbage
    // indices during the reset.
    const bool wasBlocked = tracksModel_->signalsBlocked();
    tracksModel_->blockSignals(true);
    tracksModel_->clear();
    tracksModel_->setHorizontalHeaderLabels(
        {QStringLiteral("#"), QStringLiteral("Title"),
         QStringLiteral("Artist"), QStringLiteral("Duration"),
         QStringLiteral("")});
    tracksModel_->setColumnCount(kColCount);
    int n = 0;
    for (const auto& t : vm_->tracks()) {
        ++n;
        auto* numItem  = new QStandardItem(QString::number(n));
        numItem->setTextAlignment(Qt::AlignCenter);
        numItem->setEditable(false);
        auto* titleItem  = new QStandardItem(t.title());
        titleItem->setEditable(false);
        auto* artistItem = new QStandardItem(t.artist());
        artistItem->setEditable(false);
        auto* durItem    = new QStandardItem(formatDuration(t.duration()));
        durItem->setTextAlignment(Qt::AlignCenter);
        durItem->setEditable(false);
        auto* removeItem = new QStandardItem(
            QString::fromUtf8("\u2715"));  // ✕
        removeItem->setTextAlignment(Qt::AlignCenter);
        removeItem->setToolTip(QStringLiteral("Remove from playlist"));
        removeItem->setEditable(false);
        // Stash the row index on the remove cell so the click
        // handler can resolve it even when filtered.
        removeItem->setData(n - 1, Qt::UserRole + 1);
        tracksModel_->appendRow({numItem, titleItem, artistItem,
                                 durItem, removeItem});
    }
    tracksModel_->blockSignals(wasBlocked);
}

void PlaylistView::onBackClicked() {
    if (nav_) nav_->closeOverlay();
}

void PlaylistView::onPlayAllClicked() {
    if (!vm_) return;
    vm_->playAll();
    if (playAllBtn_) playAllBtn_->setText(QStringLiteral("  Playing\u2026"));
}

void PlaylistView::onShuffleChanged(bool on) {
    if (!shuffleBtn_) return;
    shuffleBtn_->setChecked(on);
}

void PlaylistView::onShuffleClicked() {
    if (!vm_) return;
    vm_->shufflePlay();
}

void PlaylistView::onRenameClicked() {
    if (!vm_) return;
    if (!vm_->canEdit()) {
        onVmErrorReported(QStringLiteral("Read-only playlist"));
        return;
    }
    bool ok = false;
    const QString current = vm_->name();
    const QString newName = QInputDialog::getText(
        this,
        QStringLiteral("Rename playlist"),
        QStringLiteral("New name:"),
        QLineEdit::Normal,
        current,
        &ok);
    if (!ok) return;
    if (newName.trimmed().isEmpty()) {
        QMessageBox::information(this,
            QStringLiteral("Rename playlist"),
            QStringLiteral("Playlist name cannot be empty."),
            QMessageBox::Ok);
        return;
    }
    if (newName == current) return;
    vm_->rename(newName);
}

void PlaylistView::onAddTracksClicked() {
    if (!vm_) return;
    if (!vm_->canEdit()) {
        onVmErrorReported(QStringLiteral("Read-only playlist"));
        return;
    }
    const QString lastDir = QDir::homePath();
    const QStringList files = QFileDialog::getOpenFileNames(
        this,
        QStringLiteral("Add tracks to '%1'").arg(vm_->name()),
        lastDir,
        audioFileFilter());
    if (files.isEmpty()) return;
    addFilePaths(files);
}

void PlaylistView::onFromPlaylistClicked() {
    if (!vm_ || !libVm_) return;
    if (!vm_->canEdit()) {
        onVmErrorReported(QStringLiteral("Read-only playlist"));
        return;
    }

    PlaylistPickerDialog dlg(libVm_, theme_, this);
    if (dlg.exec() != QDialog::Accepted) return;

    const QString srcId = dlg.selectedPlaylistId();
    if (srcId.isEmpty()) return;

    // Find the source playlist in the LibraryViewModel
    const auto playlists = libVm_->playlists();
    for (const auto& pl : playlists) {
        if (pl.id() == srcId) {
            // Add all tracks from the source playlist to the current one
            int added = 0;
            for (const auto& t : pl.tracks()) {
                vm_->addTrack(t);
                ++added;
            }
            if (added > 0) renderPlaylist();
            return;
        }
    }
}

void PlaylistView::addFilePaths(const QStringList& paths) {
    if (!vm_ || paths.isEmpty()) return;
    int added = 0;
    for (const QString& p : paths) {
        MusicFile mf;
        // Prefer the library copy so we keep full metadata; fall
        // back to a placeholder with the file-name title otherwise.
        if (repo_) {
            const auto existing = repo_->trackByPath(p);
            if (existing.has_value()) {
                mf = *existing;
            } else {
                mf.setFilePath(p);
                mf.setTitle(QFileInfo(p).fileName());
            }
        } else {
            mf.setFilePath(p);
            mf.setTitle(QFileInfo(p).fileName());
        }
        if (mf.filePath().isEmpty()) continue;
        vm_->addTrack(mf);
        ++added;
    }
    if (added > 0) {
        // Refresh the model once at the end; addTrack emits
        // tracksChanged for each call which would trigger a
        // per-track refresh otherwise.
        renderPlaylist();
    }
}

void PlaylistView::onTrackListClicked(const QModelIndex& idx) {
    if (!idx.isValid() || !vm_) return;
    if (!vm_->canEdit()) return;
    if (idx.column() != kColRemove) return;
    const int row = idx.row();
    if (row < 0 || row >= vm_->tracks().size()) return;
    vm_->removeTrackAt(row);
}

void PlaylistView::onTracksModelRowsMoved(const QModelIndex& /*parent*/,
                                           int start, int /*end*/,
                                           const QModelIndex& /*destination*/,
                                           int row) {
    if (!vm_) return;
    if (!vm_->canEdit()) return;
    // QAbstractItemView inserts the dragged row *at* `row`, but
    // PlaylistViewModel::reorder expects "the item originally at
    // `start` ends up at `row`". The two views are equivalent when
    // direction == down (start < row), but the indices shift when
    // the user drags *up* (start > row). Adjust accordingly.
    int to = row;
    if (to > start) --to;
    if (to == start) return;
    vm_->reorder(start, to);
    // Re-render so the # column reflects the new order, and so
    // the model state matches what the view just drew.
    refreshTrackModel();
}

void PlaylistView::onTrackDoubleClicked(const QModelIndex& idx) {
    if (!idx.isValid() || !vm_ || !queue_) return;
    int row = idx.row();
    if (row < 0 || row >= vm_->tracks().size()) return;
    const MusicFile& t = vm_->tracks().at(row);
    if (t.filePath().isEmpty()) return;
    queue_->enqueue(t);
    queue_->setCurrentIndex(queue_->count() - 1);
}

} // namespace mf::app::widgets

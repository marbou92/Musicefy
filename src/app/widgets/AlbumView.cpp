// AlbumView.cpp
// See header. The widget binds to AlbumViewModel and renders the
// album. All queue work is delegated to the view model; the widget
// only handles styling, layout, and signal routing.

#include "AlbumView.h"

#include "CoverImage.h"
#include "SvgIcon.h"

#include "../core/models/MusicFile.h"
#include "../core/playback/QueueManager.h"
#include "../core/services/ImageCache.h"
#include "../core/services/NavigationService.h"
#include "../core/theme/MusicefyColorScheme.h"
#include "../core/theme/ThemeManager.h"
#include "../viewmodels/AlbumViewModel.h"

#include <QFont>
#include <QHBoxLayout>
#include <QLabel>
#include <QListView>
#include <QPushButton>
#include <QStandardItem>
#include <QStandardItemModel>
#include <QVBoxLayout>

namespace mf::app::widgets {

using mf::core::models::AlbumInfo;
using mf::core::models::MusicFile;
using mf::core::playback::QueueManager;
using mf::core::services::ImageCache;
using mf::core::services::NavigationService;
using mf::core::theme::ThemeManager;
using mf::core::theme::MusicefyColorScheme;
using mf::app::viewmodels::AlbumViewModel;

namespace {
QString formatDuration(std::chrono::seconds s) {
    qint64 total = s.count();
    if (total <= 0) return QStringLiteral("--:--");
    return QStringLiteral("%1:%2")
        .arg(total / 60)
        .arg(total % 60, 2, 10, QLatin1Char('0'));
}
} // anonymous namespace

AlbumView::AlbumView(AlbumViewModel* vm,
                     NavigationService* nav,
                     QueueManager*     queue,
                     ImageCache*       imageCache,
                     ThemeManager*     theme,
                     QWidget*          parent)
    : QWidget(parent)
    , vm_(vm)
    , nav_(nav)
    , queue_(queue)
    , imageCache_(imageCache)
    , theme_(theme)
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
                this, &AlbumView::onShuffleChanged);
    }
    if (vm_) {
        connect(vm_, &AlbumViewModel::infoChanged,
                this, &AlbumView::onVmInfoChanged);
        connect(vm_, &AlbumViewModel::savedChanged,
                this, &AlbumView::onVmSavedChanged);
        // Initial render in case setAlbum was called before ctor
        // connected.
        renderAlbum();
    }
}

void AlbumView::buildUi() {
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
            this, &AlbumView::onBackClicked);
    topRow->addWidget(backBtn_, /*stretch=*/0);

    topRow->addStretch(1);

    saveBtn_ = new QPushButton(this);
    saveBtn_->setCursor(Qt::PointingHandCursor);
    saveBtn_->setCheckable(true);
    saveBtn_->setObjectName(QStringLiteral("save"));
    saveBtn_->setIconSize(QSize(16, 16));
    connect(saveBtn_, &QPushButton::toggled,
            this, &AlbumView::onSaveToggled);
    topRow->addWidget(saveBtn_, /*stretch=*/0);

    shuffleBtn_ = new QPushButton(this);
    shuffleBtn_->setText(QStringLiteral("  Shuffle"));
    shuffleBtn_->setCursor(Qt::PointingHandCursor);
    shuffleBtn_->setObjectName(QStringLiteral("secondary"));
    shuffleBtn_->setIconSize(QSize(16, 16));
    connect(shuffleBtn_, &QPushButton::clicked,
            this, &AlbumView::onShuffleClicked);
    topRow->addWidget(shuffleBtn_, /*stretch=*/0);

    playAllBtn_ = new QPushButton(this);
    playAllBtn_->setText(QStringLiteral("  Play all"));
    playAllBtn_->setCursor(Qt::PointingHandCursor);
    playAllBtn_->setObjectName(QStringLiteral("primary"));
    playAllBtn_->setIconSize(QSize(16, 16));
    connect(playAllBtn_, &QPushButton::clicked,
            this, &AlbumView::onPlayAllClicked);
    topRow->addWidget(playAllBtn_, /*stretch=*/0);

    root->addLayout(topRow);

    auto* headerRow = new QHBoxLayout();
    headerRow->setSpacing(20);

    coverLabel_ = new CoverImage(imageCache_, this);
    coverLabel_->setFixedSize(180, 180);
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
    connect(tracksList_, &QListView::doubleClicked,
            this, &AlbumView::onTrackDoubleClicked);
    root->addWidget(tracksList_, /*stretch=*/1);
}

QString AlbumView::formatTotalDuration(std::chrono::seconds s) const {
    qint64 total = s.count();
    if (total <= 0) return QString();
    if (total < 3600) {
        return QStringLiteral("%1 min").arg(total / 60);
    }
    return QStringLiteral("%1 hr %2 min")
        .arg(total / 3600)
        .arg((total % 3600) / 60);
}

void AlbumView::applyTheme() {
    MusicefyColorScheme s;
    if (theme_) s = theme_->scheme();
    if (!s.surface.isValid()) {
        setStyleSheet(QString());
        return;
    }
    setStyleSheet(QStringLiteral(
        "QWidget { background: %1; color: %2; }"
        "QPushButton { background: transparent; color: %3; text-align: left;"
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
        "QPushButton#save {"
        "  background: transparent; color: %3; border-radius: 6px;"
        "  padding: 8px 14px; border: 1px solid %9; }"
        "QPushButton#save:checked {"
        "  background: %4; color: %5; border: 1px solid %4; }"
        "QPushButton#save:hover { background: %10; }"
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
    if (saveBtn_) {
        saveBtn_->setIcon(SvgIcon::get(
            saveBtn_->isChecked() ? "star" : "plus",
            s.onSurfaceVariant, 16));
    }
    if (shuffleBtn_) {
        shuffleBtn_->setIcon(SvgIcon::get("shuffle", s.onSurfaceVariant, 16));
    }
    if (playAllBtn_) {
        playAllBtn_->setIcon(SvgIcon::get("play", s.onPrimary, 16));
    }
}

void AlbumView::setAlbum(const AlbumInfo& info) {
    if (vm_) vm_->setInfo(info);
}

void AlbumView::onVmInfoChanged() {
    renderAlbum();
}

void AlbumView::onVmSavedChanged() {
    if (!vm_) return;
    if (saveBtn_) saveBtn_->setChecked(vm_->isSaved());
}

void AlbumView::renderAlbum() {
    if (!vm_) return;
    const AlbumInfo& info = vm_->info();
    titleLabel_->setText(info.name());
    QStringList sub;
    if (!info.artist().isEmpty()) sub << info.artist();
    if (info.year() > 0)          sub << QString::number(info.year());
    sub << QStringLiteral("%1 tracks").arg(info.trackCount());
    const QString totalDur = formatTotalDuration(std::chrono::seconds(vm_->totalDurationMs() / 1000));
    if (!totalDur.isEmpty()) sub << totalDur;
    subtitleLabel_->setText(sub.join(QStringLiteral("  \u2022  ")));

    const QString coverSource = !info.coverUrl().isEmpty()
        ? info.coverUrl()
        : info.coverPath();
    if (coverLabel_) coverLabel_->setSource(coverSource, info.name());
    if (saveBtn_)    saveBtn_->setChecked(info.isSaved());
    if (saveBtn_)    saveBtn_->setText(info.isSaved()
        ? QStringLiteral("  Saved")
        : QStringLiteral("  Save"));
    // Re-render the save button's icon now that the checked state
    // has been set.
    MusicefyColorScheme s;
    if (theme_) s = theme_->scheme();
    if (saveBtn_ && s.onSurfaceVariant.isValid()) {
        saveBtn_->setIcon(SvgIcon::get(
            info.isSaved() ? "star" : "plus",
            s.onSurfaceVariant, 16));
    }

    tracksModel_->clear();
    tracksModel_->setHorizontalHeaderLabels(
        {QStringLiteral("#"), QStringLiteral("Title"),
         QStringLiteral("Duration")});
    int n = 0;
    for (const auto& t : info.tracks()) {
        ++n;
        tracksModel_->appendRow({
            new QStandardItem(QString::number(t.trackNumber() > 0 ? t.trackNumber() : n)),
            new QStandardItem(t.title()),
            new QStandardItem(formatDuration(t.duration())),
        });
    }

    if (playAllBtn_) playAllBtn_->setEnabled(vm_->canPlay());
    if (shuffleBtn_) shuffleBtn_->setEnabled(vm_->canPlay());
}

void AlbumView::onBackClicked() {
    if (nav_) nav_->closeOverlay();
}

void AlbumView::onPlayAllClicked() {
    if (!vm_) return;
    vm_->playAll();
    if (playAllBtn_) playAllBtn_->setText(QStringLiteral("  Playing\u2026"));
}

void AlbumView::onShuffleClicked() {
    if (!vm_) return;
    vm_->shufflePlay();
}

void AlbumView::onSaveToggled(bool checked) {
    if (!vm_) return;
    if (vm_->isSaved() == checked) {
        // SaveBtn checked state already mirrors vm; just refresh label.
        if (saveBtn_) saveBtn_->setText(checked
            ? QStringLiteral("  Saved")
            : QStringLiteral("  Save"));
        return;
    }
    vm_->toggleSaved();
    // Re-render the star/plus icon to match the new state.
    if (saveBtn_ && theme_) {
        const auto s = theme_->scheme();
        saveBtn_->setIcon(SvgIcon::get(
            checked ? "star" : "plus",
            s.onSurfaceVariant, 16));
    }
}

void AlbumView::onShuffleChanged(bool on) {
    if (shuffleBtn_) {
        shuffleBtn_->setText(on
            ? QStringLiteral("  Shuffle: On")
            : QStringLiteral("  Shuffle"));
    }
}

void AlbumView::onTrackDoubleClicked(const QModelIndex& idx) {
    if (!idx.isValid() || !vm_ || !queue_) return;
    int row = idx.row();
    if (row < 0 || row >= vm_->tracks().size()) return;
    const MusicFile& t = vm_->tracks().at(row);
    if (t.filePath().isEmpty()) return;
    queue_->enqueue(t);
    queue_->setCurrentIndex(queue_->count() - 1);
}

} // namespace mf::app::widgets

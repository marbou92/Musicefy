// NowPlayingView.cpp
// See header.

#include "NowPlayingView.h"

#include "CoverImage.h"
#include "LyricsPanel.h"
#include "SvgIcon.h"

#include "../core/models/MusicFile.h"
#include "../core/playback/QueueManager.h"
#include "../core/services/ImageCache.h"
#include "../core/services/NavigationService.h"
#include "../core/services/PathToImage.h"
#include "../core/services/SleepTimer.h"
#include "../core/theme/MusicefyColorScheme.h"
#include "../core/theme/ThemeManager.h"

#include "viewmodels/PlayerViewModel.h"

#include <QFont>
#include <QHBoxLayout>
#include <QLabel>
#include <QListView>
#include <QMenu>
#include <QPushButton>
#include <QStandardItem>
#include <QStandardItemModel>
#include <QSlider>
#include <QStackedWidget>
#include <QVBoxLayout>
#include <QEvent>

// For artist/album click navigation
#include "../core/models/ArtistInfo.h"
#include "../core/models/AlbumInfo.h"

namespace mf::app::widgets {

using mf::app::viewmodels::PlayerViewModel;
using mf::core::models::MusicFile;
using mf::core::playback::QueueManager;
using mf::core::services::ImageCache;
using mf::core::services::NavigationService;
using mf::core::theme::ThemeManager;
using mf::core::theme::MusicefyColorScheme;
using mf::core::services::SleepTimer;

NowPlayingView::NowPlayingView(PlayerViewModel*   vm,
                               QueueManager*      queue,
                               NavigationService* nav,
                               ImageCache*        imageCache,
                               ThemeManager*      theme,
                               SleepTimer*        sleepTimer,
                               QWidget*           parent)
    : QWidget(parent)
    , vm_(vm)
    , queue_(queue)
    , nav_(nav)
    , imageCache_(imageCache)
    , theme_(theme)
    , sleepTimer_(sleepTimer)
{
    buildUi();
    applyTheme();

    // Install event filter for clickable artist/album labels.
    if (artistLabel_) artistLabel_->installEventFilter(this);
    if (albumLabel_) albumLabel_->installEventFilter(this);

    if (vm_) {
        connect(vm_, &PlayerViewModel::isPlayingChanged,
                this, &NowPlayingView::onVmIsPlayingChanged);
        connect(vm_, &PlayerViewModel::isPausedChanged,
                this, &NowPlayingView::onVmIsPausedChanged);
        connect(vm_, &PlayerViewModel::currentTrackChanged,
                this, &NowPlayingView::onVmCurrentTrackChanged);
        connect(vm_, &PlayerViewModel::positionChanged,
                this, &NowPlayingView::onVmPositionChanged);
        connect(vm_, &PlayerViewModel::durationChanged,
                this, &NowPlayingView::onVmDurationChanged);
        connect(vm_, &PlayerViewModel::shuffleChanged,
                this, &NowPlayingView::onVmShuffleChanged);
        connect(vm_, &PlayerViewModel::repeatChanged,
                this, &NowPlayingView::onVmRepeatChanged);
        connect(vm_, &PlayerViewModel::isFavoriteChanged,
                this, [this]() { updateFavIcon(); });
        connect(vm_, &PlayerViewModel::audioFormatChanged,
                this, [this]() {
            if (formatLabel_) formatLabel_->setText(vm_->audioFormatText());
        });
    }
    if (queue_) {
        connect(queue_, &QueueManager::queueChangedQ,
                this, &NowPlayingView::onQueueQueueChanged);
        connect(queue_, &QueueManager::indexChangedQ,
                this, [this](int) { onQueueIndexChanged(); });
    }
    if (queueModel_) {
        connect(queueModel_, &QStandardItemModel::rowsMoved,
                this, &NowPlayingView::onQueueRowsMoved);
    }
    if (theme_) {
        connect(theme_, &ThemeManager::schemeChanged,
                this, [this]() {
                    applyTheme();
                    if (coverLabel_) coverLabel_->refreshPlaceholder();
                });
    }
    if (sleepTimer_) {
        connect(sleepTimer_, &SleepTimer::activeChanged,
                this, &NowPlayingView::onSleepTimerExpired);
    }

    // Initial sync.
    onVmIsPlayingChanged();
    onVmIsPausedChanged();
    onVmCurrentTrackChanged();
    onVmPositionChanged();
    onVmDurationChanged();
    onVmShuffleChanged();
    onVmRepeatChanged();
    onQueueQueueChanged();
    updateFavIcon();
    if (formatLabel_) formatLabel_->setText(vm_ ? vm_->audioFormatText() : QString());
}

void NowPlayingView::buildUi() {
    auto* root = new QVBoxLayout(this);
    root->setContentsMargins(0, 0, 0, 0);
    root->setSpacing(0);

    // â”€â”€ Top bar with back button â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    auto* topRow = new QHBoxLayout();
    topRow->setContentsMargins(16, 12, 16, 0);
    backBtn_ = new QPushButton(this);
    backBtn_->setText(QStringLiteral("Back"));
    backBtn_->setCursor(Qt::PointingHandCursor);
    backBtn_->setFlat(true);
    backBtn_->setIconSize(QSize(18, 18));
    connect(backBtn_, &QPushButton::clicked,
            this, &NowPlayingView::onBackClicked);
    topRow->addWidget(backBtn_, /*stretch=*/0);
    topRow->addStretch(1);
    root->addLayout(topRow);

    // â”€â”€ Two-column main body â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    auto* bodyRow = new QHBoxLayout();
    bodyRow->setContentsMargins(28, 20, 28, 20);
    bodyRow->setSpacing(28);

    // Left column: cover + metadata + controls.
    auto* leftCol = new QVBoxLayout();
    leftCol->setSpacing(12);

    coverLabel_ = new CoverImage(imageCache_, this);
    coverLabel_->setFixedSize(280, 280);
    leftCol->addWidget(coverLabel_, /*stretch=*/0, Qt::AlignHCenter);

    titleLabel_ = new QLabel(this);
    QFont tf = titleLabel_->font();
    tf.setPointSize(20);
    tf.setBold(true);
    titleLabel_->setFont(tf);
    titleLabel_->setWordWrap(true);
    titleLabel_->setAlignment(Qt::AlignHCenter);
    leftCol->addWidget(titleLabel_);

    artistLabel_ = new QLabel(this);
    QFont af = artistLabel_->font();
    af.setPointSize(13);
    artistLabel_->setFont(af);
    artistLabel_->setAlignment(Qt::AlignHCenter);
    artistLabel_->setCursor(Qt::PointingHandCursor);
    leftCol->addWidget(artistLabel_);

    albumLabel_ = new QLabel(this);
    albumLabel_->setProperty("role", QStringLiteral("secondary"));
    albumLabel_->setAlignment(Qt::AlignHCenter);
    albumLabel_->setCursor(Qt::PointingHandCursor);
    leftCol->addWidget(albumLabel_);

    // Favorite + Share row.
    auto* actionRow = new QHBoxLayout();
    actionRow->setSpacing(16);
    actionRow->setAlignment(Qt::AlignHCenter);

    favBtn_ = new QPushButton(this);
    favBtn_->setFixedSize(36, 36);
    favBtn_->setCursor(Qt::PointingHandCursor);
    favBtn_->setIconSize(QSize(20, 20));
    favBtn_->setToolTip(QStringLiteral("Toggle favourite"));
    connect(favBtn_, &QPushButton::clicked,
            this, &NowPlayingView::onFavClicked);
    actionRow->addWidget(favBtn_);

    shareBtn_ = new QPushButton(this);
    shareBtn_->setFixedSize(36, 36);
    shareBtn_->setCursor(Qt::PointingHandCursor);
    shareBtn_->setIconSize(QSize(18, 18));
    shareBtn_->setToolTip(QStringLiteral("Copy track info"));
    connect(shareBtn_, &QPushButton::clicked,
            this, &NowPlayingView::onShareClicked);
    actionRow->addWidget(shareBtn_);

    lyricsBtn_ = new QPushButton(this);
    lyricsBtn_->setFixedSize(36, 36);
    lyricsBtn_->setCursor(Qt::PointingHandCursor);
    lyricsBtn_->setIconSize(QSize(18, 18));
    lyricsBtn_->setToolTip(QStringLiteral("Lyrics"));
    connect(lyricsBtn_, &QPushButton::clicked,
            this, &NowPlayingView::onLyricsClicked);
    actionRow->addWidget(lyricsBtn_);

    leftCol->addLayout(actionRow);

    // Lyrics panel (bottom sheet, initially hidden).
    lyricsPanel_ = new LyricsPanel(vm_, theme_, this);

    // Seek + time row.
    auto* seekRow = new QHBoxLayout();
    seekRow->setSpacing(8);
    elapsedLabel_ = new QLabel(QStringLiteral("0:00"), this);
    elapsedLabel_->setProperty("role", QStringLiteral("timeLabel"));
    elapsedLabel_->setMinimumWidth(40);
    elapsedLabel_->setAlignment(Qt::AlignRight | Qt::AlignVCenter);
    seekRow->addWidget(elapsedLabel_);

    seekSlider_ = new QSlider(Qt::Horizontal, this);
    seekSlider_->setRange(0, 1000);
    connect(seekSlider_, &QSlider::sliderPressed,
            this, &NowPlayingView::onSeekPressed);
    connect(seekSlider_, &QSlider::sliderReleased,
            this, &NowPlayingView::onSeekReleased);
    connect(seekSlider_, &QSlider::sliderMoved,
            this, &NowPlayingView::onSeekMoved);
    seekRow->addWidget(seekSlider_, /*stretch=*/1);

    totalLabel_ = new QLabel(QStringLiteral("0:00"), this);
    totalLabel_->setProperty("role", QStringLiteral("timeLabel"));
    totalLabel_->setMinimumWidth(40);
    seekRow->addWidget(totalLabel_);
    leftCol->addLayout(seekRow);

    // Transport row.
    auto* transportRow = new QHBoxLayout();
    transportRow->setSpacing(12);
    prevBtn_ = new QPushButton(this);
    prevBtn_->setFixedSize(48, 48);
    prevBtn_->setCursor(Qt::PointingHandCursor);
    prevBtn_->setIconSize(QSize(22, 22));
    connect(prevBtn_, &QPushButton::clicked,
            this, &NowPlayingView::onPrevClicked);
    transportRow->addWidget(prevBtn_, /*stretch=*/0);

    playPauseBtn_ = new QPushButton(this);
    playPauseBtn_->setFixedSize(64, 64);
    playPauseBtn_->setIconSize(QSize(28, 28));
    playPauseBtn_->setCursor(Qt::PointingHandCursor);
    playPauseBtn_->setProperty("role", QStringLiteral("playPause"));
    connect(playPauseBtn_, &QPushButton::clicked,
            this, &NowPlayingView::onPlayPauseClicked);
    transportRow->addWidget(playPauseBtn_, /*stretch=*/0);

    nextBtn_ = new QPushButton(this);
    nextBtn_->setFixedSize(48, 48);
    nextBtn_->setCursor(Qt::PointingHandCursor);
    nextBtn_->setIconSize(QSize(22, 22));
    connect(nextBtn_, &QPushButton::clicked,
            this, &NowPlayingView::onNextClicked);
    transportRow->addWidget(nextBtn_, /*stretch=*/0);
    transportRow->addStretch(1);
    leftCol->addLayout(transportRow);

    // Volume row.
    auto* volRow = new QHBoxLayout();
    volRow->setSpacing(8);
    volIcon_ = new QLabel(this);
    volIcon_->setProperty("role", QStringLiteral("secondary"));
    volIcon_->setFixedSize(20, 20);
    volRow->addWidget(volIcon_);
    volumeSlider_ = new QSlider(Qt::Horizontal, this);
    volumeSlider_->setRange(0, 100);
    volumeSlider_->setValue(80);
    connect(volumeSlider_, &QSlider::valueChanged,
            this, &NowPlayingView::onVolumeChanged);
    volRow->addWidget(volumeSlider_, /*stretch=*/1);
    leftCol->addLayout(volRow);

    // Audio format label.
    formatLabel_ = new QLabel(this);
    formatLabel_->setProperty("role", QStringLiteral("secondary"));
    formatLabel_->setAlignment(Qt::AlignHCenter);
    QFont fmtf = formatLabel_->font();
    fmtf.setPointSize(10);
    formatLabel_->setFont(fmtf);
    leftCol->addWidget(formatLabel_);

    // Shuffle / repeat row.
    auto* modeRow = new QHBoxLayout();
    modeRow->setSpacing(8);
    shuffleBtn_ = new QPushButton(this);
    shuffleBtn_->setText(QStringLiteral("Shuffle"));
    shuffleBtn_->setCursor(Qt::PointingHandCursor);
    shuffleBtn_->setCheckable(true);
    shuffleBtn_->setIconSize(QSize(16, 16));
    shuffleBtn_->setProperty("role", QStringLiteral("modeButton"));
    connect(shuffleBtn_, &QPushButton::toggled,
            this, &NowPlayingView::onShuffleClicked);
    modeRow->addWidget(shuffleBtn_);

    repeatBtn_ = new QPushButton(this);
    repeatBtn_->setText(QStringLiteral("Repeat: Off"));
    repeatBtn_->setCursor(Qt::PointingHandCursor);
    repeatBtn_->setIconSize(QSize(16, 16));
    repeatBtn_->setProperty("role", QStringLiteral("modeButton"));
    connect(repeatBtn_, &QPushButton::clicked,
            this, &NowPlayingView::onRepeatClicked);
    modeRow->addWidget(repeatBtn_);

    // Sleep timer button with dropdown menu.
    sleepBtn_ = new QPushButton(this);
    sleepBtn_->setText(QStringLiteral("Sleep"));
    sleepBtn_->setCursor(Qt::PointingHandCursor);
    sleepBtn_->setIconSize(QSize(16, 16));
    sleepBtn_->setProperty("role", QStringLiteral("modeButton"));
    connect(sleepBtn_, &QPushButton::clicked,
            this, &NowPlayingView::onSleepClicked);
    modeRow->addWidget(sleepBtn_);

    modeRow->addStretch(1);
    leftCol->addLayout(modeRow);

    leftCol->addStretch(1);
    bodyRow->addLayout(leftCol, /*stretch=*/1);

    // Right column: queue list.
    auto* rightCol = new QVBoxLayout();
    rightCol->setSpacing(8);
    auto* queueHeader = new QLabel(QStringLiteral("Up next"), this);
    QFont qhf = queueHeader->font();
    qhf.setPointSize(14);
    qhf.setBold(true);
    queueHeader->setFont(qhf);
    rightCol->addWidget(queueHeader);

    queueList_  = new QListView(this);
    queueModel_ = new QStandardItemModel(this);
    queueList_->setModel(queueModel_);
    queueList_->setUniformItemSizes(true);
    queueList_->setEditTriggers(QAbstractItemView::NoEditTriggers);
    queueList_->setSelectionMode(QAbstractItemView::SingleSelection);
    queueList_->setAlternatingRowColors(true);
    // Drag-reorder: InternalMove lets the model rearrange the rows
    // for us. We then mirror the new order back into the queue via
    // onQueueRowsMoved â†’ setOrderFromVisible.
    queueList_->setMovement(QListView::Snap);
    queueList_->setDragDropMode(QAbstractItemView::InternalMove);
    queueList_->setDragEnabled(true);
    queueList_->setAcceptDrops(true);
    queueList_->setDropIndicatorShown(true);
    connect(queueList_, &QListView::doubleClicked,
            this, &NowPlayingView::onQueueDoubleClicked);
    rightCol->addWidget(queueList_, /*stretch=*/1);
    bodyRow->addLayout(rightCol, /*stretch=*/1);

    root->addLayout(bodyRow, /*stretch=*/1);

    // Lyrics panel overlays at the bottom.
    if (lyricsPanel_) {
        lyricsPanel_->setParent(this);
        connect(lyricsPanel_, &LyricsPanel::visibilityChanged,
                this, [this](bool visible) {
            // When lyrics show, hide queue; when lyrics hide, restore
            if (visible && queueList_) queueList_->setVisible(false);
            if (!visible && queueList_) queueList_->setVisible(true);
        });
    }
}

void NowPlayingView::applyTheme() {
    MusicefyColorScheme s;
    if (theme_) s = theme_->scheme();
    if (!s.surface.isValid()) {
        setStyleSheet(QString());
        return;
    }
    setStyleSheet(QStringLiteral(
        "QWidget { background: %1; color: %2; }"
        "QPushButton { background: transparent; color: %2;"
        "  border: none; padding: 4px 8px; }"
        "QPushButton:hover { color: %3; }"
        "QPushButton[role=\"playPause\"] {"
        "  background: %3; color: %4; border-radius: 32px;"
        "  border: none; }"
        "QPushButton[role=\"playPause\"]:hover { background: %5; }"
        "QPushButton[role=\"modeButton\"] {"
        "  background: %6; color: %2;"
        "  border: 1px solid %7; border-radius: 6px; padding: 4px 12px; }"
        "QPushButton[role=\"modeButton\"]:checked {"
        "  background: %3; color: %4; border: 1px solid %3; }"
        "QLabel[role=\"secondary\"] { color: %8; }"
        "QLabel[role=\"timeLabel\"] { color: %8; }"
        "QLabel[role=\"coverArt\"] {"
        "  background: %9; color: %3; border-radius: 8px;"
        "  border: 1px solid %7; }"
        "QSlider::groove:horizontal {"
        "  background: %6; height: 6px; border-radius: 3px; }"
        "QSlider::handle:horizontal {"
        "  background: %3; width: 14px; height: 14px;"
        "  margin: -4px 0; border-radius: 7px; }"
        "QSlider::sub-page:horizontal {"
        "  background: %3; height: 6px; border-radius: 3px; }"
        "QListView { background: %6; color: %2;"
        "  border: 1px solid %7; border-radius: 6px;"
        "  selection-background-color: %3; selection-color: %4;"
        "  alternate-background-color: %10; }"
    )
    .arg(s.surface.name())                 // 1 background
    .arg(s.onSurface.name())               // 2 default text
    .arg(s.primary.name())                 // 3 highlight
    .arg(s.onPrimary.name())               // 4 highlight text
    .arg(s.surfaceContainerHighest.name()) // 5 hover
    .arg(s.surfaceContainerHigh.name())    // 6 list/button bg
    .arg(s.outlineVariant.name())          // 7 borders
    .arg(s.onSurfaceVariant.name())        // 8 secondary
    .arg(s.surfaceContainerHigh.name())    // 9 cover bg
    .arg(s.surfaceContainer.name())        // 10 alt row
    );

    // Re-render SVG icons with the current theme colors.
    if (backBtn_) {
        backBtn_->setIcon(SvgIcon::get("arrow-left", s.onSurface, 18));
    }
    if (prevBtn_) {
        prevBtn_->setIcon(SvgIcon::get("skip-back", s.onSurface, 22));
    }
    if (nextBtn_) {
        nextBtn_->setIcon(SvgIcon::get("skip-forward", s.onSurface, 22));
    }
    if (playPauseBtn_) {
        playPauseBtn_->setIcon(SvgIcon::get(
            (vm_ && vm_->isPlaying()) ? "pause" : "play",
            s.onPrimary, 28));
    }
    if (shuffleBtn_) {
        shuffleBtn_->setIcon(SvgIcon::get(
            "shuffle",
            s.onSurface, 16));
    }
    if (repeatBtn_) {
        repeatBtn_->setIcon(SvgIcon::get(
            "repeat",
            s.onSurface, 16));
    }
    if (volIcon_) {
        volIcon_->setPixmap(
            SvgIcon::get("volume-2", s.onSurfaceVariant, 18).pixmap(18, 18));
    }
    updateFavIcon();
    if (shareBtn_) {
        shareBtn_->setIcon(SvgIcon::get("share", s.onSurfaceVariant, 18));
    }
    if (lyricsBtn_) {
        lyricsBtn_->setIcon(SvgIcon::get("lyrics", s.onSurfaceVariant, 18));
    }
}

QString NowPlayingView::formatMs(qint64 ms) const {
    if (ms < 0) ms = 0;
    qint64 totalSec = ms / 1000;
    qint64 mins = totalSec / 60;
    qint64 secs = totalSec % 60;
    return QStringLiteral("%1:%2")
        .arg(mins)
        .arg(secs, 2, 10, QLatin1Char('0'));
}

bool NowPlayingView::eventFilter(QObject* obj, QEvent* event) {
    if (event->type() == QEvent::MouseButtonRelease) {
        if (obj == artistLabel_) {
            onArtistClicked();
            return true;
        }
        if (obj == albumLabel_) {
            onAlbumClicked();
            return true;
        }
    }
    return QWidget::eventFilter(obj, event);
}

void NowPlayingView::onBackClicked() {
    if (nav_) nav_->closeOverlay();
}

void NowPlayingView::onPlayPauseClicked() {
    if (vm_) vm_->togglePlayPause();
}

void NowPlayingView::onNextClicked() {
    if (vm_) vm_->next();
}

void NowPlayingView::onPrevClicked() {
    if (vm_) vm_->previous();
}

void NowPlayingView::onShuffleClicked(bool on) {
    if (vm_) vm_->setShuffle(on);
}

void NowPlayingView::onRepeatClicked() {
    if (vm_) vm_->cycleRepeat();
}

void NowPlayingView::onVolumeChanged(int v) {
    if (vm_) vm_->setVolume(v / 100.0f);
}

void NowPlayingView::onSeekPressed() {
    seekSliderBeingDragged_ = true;
}

void NowPlayingView::onSeekReleased() {
    seekSliderBeingDragged_ = false;
    if (vm_) {
        vm_->seekPercent(seekSlider_->value() / 1000.0);
    }
}

void NowPlayingView::onSeekMoved(int v) {
    // Update the elapsed label to give feedback while dragging.
    if (vm_) {
        qint64 dur = vm_->durationMs();
        if (dur > 0) {
            elapsedLabel_->setText(formatMs((v * dur) / 1000));
        }
    }
}

void NowPlayingView::onQueueDoubleClicked(const QModelIndex& idx) {
    if (!queue_ || !idx.isValid()) return;
    int row = idx.row();
    if (row < 0 || row >= queue_->count()) return;
    queue_->setCurrentIndex(row);
}

void NowPlayingView::onVmIsPlayingChanged() {
    if (!vm_ || !playPauseBtn_) return;
    // Refresh just the play/pause icon â€” full re-style is handled
    // by applyTheme() on theme changes.
    MusicefyColorScheme s;
    if (theme_) s = theme_->scheme();
    playPauseBtn_->setIcon(SvgIcon::get(
        vm_->isPlaying() ? "pause" : "play",
        s.onPrimary, 28));
}

void NowPlayingView::onVmIsPausedChanged() {
    // isPaused is derived from isPlaying + isStopped; isPlayingChanged
    // already updated the icon.
}

void NowPlayingView::onVmCurrentTrackChanged() {
    if (!vm_) return;
    titleLabel_->setText(vm_->currentTitle().isEmpty()
        ? QStringLiteral("(no track)")
        : vm_->currentTitle());
    artistLabel_->setText(vm_->currentArtist());
    albumLabel_->setText(vm_->currentAlbum());

    // Look up the current track's cover art. We grab the track from
    // QueueManager (which is the source of truth) so we get the
    // coverPath/coverUrl fields, not just the title/artist strings.
    QString coverSource;
    QString placeholderLetter;
    if (queue_) {
        const MusicFile t = queue_->currentTrack();
        coverSource = !t.coverUrl().isEmpty() ? t.coverUrl() : t.coverPath();
        if (!t.title().isEmpty()) placeholderLetter = t.title().left(1).toUpper();
    }
    if (placeholderLetter.isEmpty()) {
        placeholderLetter = vm_->currentTitle().left(1).toUpper();
    }
    if (coverLabel_) {
        coverLabel_->setSource(coverSource, placeholderLetter);
    }
}

void NowPlayingView::onVmPositionChanged() {
    if (!vm_) return;
    if (!seekSliderBeingDragged_) {
        qint64 pos = vm_->positionMs();
        qint64 dur = vm_->durationMs();
        if (dur > 0) {
            seekSlider_->setValue(int((pos * 1000) / dur));
        }
    }
    elapsedLabel_->setText(formatMs(vm_->positionMs()));
}

void NowPlayingView::onVmDurationChanged() {
    if (!vm_) return;
    totalLabel_->setText(formatMs(vm_->durationMs()));
}

void NowPlayingView::onVmShuffleChanged() {
    if (!vm_ || !shuffleBtn_) return;
    shuffleBtn_->setChecked(vm_->shuffle());
}

void NowPlayingView::onVmRepeatChanged() {
    if (!vm_ || !repeatBtn_) return;
    // The RepeatMode enum is in QueueManager / IQueueManager. We
    // don't have a direct string for it, so just reflect the int
    // as a human label.
    int mode = vm_->repeatMode();
    QString label;
    switch (mode) {
    case 0:  label = QStringLiteral("Repeat: Off");    break;
    case 1:  label = QStringLiteral("Repeat: All");    break;
    case 2:  label = QStringLiteral("Repeat: One");    break;
    default: label = QStringLiteral("Repeat: Off");    break;
    }
    repeatBtn_->setText(label);
    // The repeat icon itself doesn't change shape with the mode,
    // just the label. Nothing else to refresh here.
}

void NowPlayingView::onQueueQueueChanged() {
    onQueueIndexChanged();
}

void NowPlayingView::onQueueIndexChanged() {
    if (!queue_ || !queueModel_) return;
    queueModel_->clear();
    const auto tracks = queue_->tracks();
    int cur = queue_->currentIndex();
    int n = 0;
    for (const auto& t : tracks) {
        ++n;
        QString line = QStringLiteral("%1.  %2  â€”  %3")
                        .arg(n)
                        .arg(t.title(), t.artist());
        if (n - 1 == cur) {
            line = QStringLiteral("\u25B6 ") + line;
        }
        auto* item = new QStandardItem(line);
        // Stash the full MusicFile so a drag-reorder can read the
        // post-move order back out of the model and feed it to
        // QueueManager::setOrderFromVisible. Drag/drop is enabled
        // by default on the item flags + the view's
        // setDragDropMode(InternalMove).
        item->setData(QVariant::fromValue(t), Qt::UserRole);
        item->setFlags(item->flags() | Qt::ItemIsDragEnabled |
                                   Qt::ItemIsDropEnabled);
        queueModel_->appendRow(item);
    }
}

void NowPlayingView::onQueueRowsMoved(const QModelIndex& parent,
                                       int /*start*/, int /*end*/,
                                       const QModelIndex& /*dest*/,
                                       int /*destRow*/) {
    Q_UNUSED(parent);
    if (!queue_ || !queueModel_) return;
    // The view's InternalMove has already rearranged the items in
    // the model. Read them back and push the new visible order to
    // the queue as the new canonical order. QueueManager preserves
    // the currently-playing track by filePath identity.
    QList<MusicFile> newOrder;
    newOrder.reserve(queueModel_->rowCount());
    for (int i = 0; i < queueModel_->rowCount(); ++i) {
        QStandardItem* item = queueModel_->item(i);
        if (!item) continue;
        const MusicFile t = item->data(Qt::UserRole).value<MusicFile>();
        newOrder.append(t);
    }
    queue_->setOrderFromVisible(newOrder);
}

void NowPlayingView::onSleepClicked() {
    if (!sleepTimer_ || !sleepBtn_) return;

    QMenu menu(this);

    MusicefyColorScheme s;
    if (theme_) s = theme_->scheme();
    const QString bg = s.surfaceContainerHigh.name();
    const QString fg = s.onSurface.name();
    const QString border = s.outlineVariant.name();
    const QString sel = s.primaryContainer.name();
    menu.setStyleSheet(QStringLiteral(
        "QMenu { background: %1; color: %2; border: 1px solid %3; }"
        "QMenu::item:selected { background: %4; }"
    ).arg(bg, fg, border, sel));

    menu.addAction(QStringLiteral("Off"), this, [this]() {
        if (sleepTimer_) sleepTimer_->cancel();
        if (sleepBtn_) sleepBtn_->setText(QStringLiteral("Sleep"));
    });
    menu.addSeparator();
    menu.addAction(QStringLiteral("15 min"),  this, [this]() {
        if (sleepTimer_) sleepTimer_->start(SleepTimer::Minutes15);
    });
    menu.addAction(QStringLiteral("30 min"),  this, [this]() {
        if (sleepTimer_) sleepTimer_->start(SleepTimer::Minutes30);
    });
    menu.addAction(QStringLiteral("60 min"),  this, [this]() {
        if (sleepTimer_) sleepTimer_->start(SleepTimer::Minutes60);
    });
    menu.addAction(QStringLiteral("90 min"),  this, [this]() {
        if (sleepTimer_) sleepTimer_->start(SleepTimer::Minutes90);
    });
    menu.addAction(QStringLiteral("120 min"), this, [this]() {
        if (sleepTimer_) sleepTimer_->start(SleepTimer::Minutes120);
    });
    menu.addSeparator();
    menu.addAction(QStringLiteral("End of track"), this, [this]() {
        if (sleepTimer_) sleepTimer_->start(SleepTimer::EndOfTrack);
    });

    menu.exec(sleepBtn_->mapToGlobal(QPoint(0, sleepBtn_->height())));
}

void NowPlayingView::onSleepTimerExpired() {
    if (!sleepBtn_ || !sleepTimer_) return;
    if (sleepTimer_->isActive()) {
        int min = sleepTimer_->presetMinutes();
        if (min == -1) {
            sleepBtn_->setText(QStringLiteral("Sleep: End"));
        } else {
            sleepBtn_->setText(QStringLiteral("Sleep: %1m").arg(min));
        }
    } else {
        sleepBtn_->setText(QStringLiteral("Sleep"));
    }
}

// â”€â”€ New actions â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

void NowPlayingView::onFavClicked() {
    if (vm_) vm_->toggleFavorite();
}

void NowPlayingView::onShareClicked() {
    if (vm_) vm_->shareCurrentTrack();
}

void NowPlayingView::onLyricsClicked() {
    if (lyricsPanel_) lyricsPanel_->toggle();
}

void NowPlayingView::onArtistClicked() {
    if (!vm_ || !nav_) return;
    mf::core::models::ArtistInfo artist;
    artist.setName(vm_->currentArtist());
    nav_->requestArtist(artist);
}

void NowPlayingView::onAlbumClicked() {
    if (!vm_ || !nav_) return;
    mf::core::models::AlbumInfo album;
    album.setName(vm_->currentAlbum());
    album.setArtist(vm_->currentArtist());
    nav_->requestAlbum(album);
}

void NowPlayingView::updateFavIcon() {
    if (!favBtn_ || !vm_ || !theme_) return;
    MusicefyColorScheme s = theme_->scheme();
    if (vm_->isFavorite()) {
        favBtn_->setIcon(SvgIcon::get("heart-filled", s.primary, 20));
    } else {
        favBtn_->setIcon(SvgIcon::get("heart", s.onSurfaceVariant, 20));
    }
}

} // namespace mf::app::widgets

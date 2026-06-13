// NowPlayingBar.cpp
// See header. Everything is pulled from PlayerViewModel and
// ThemeManager — no internal state to keep in sync.

#include "NowPlayingBar.h"

#include "CoverImage.h"
#include "SvgIcon.h"

#include "viewmodels/PlayerViewModel.h"
#include "../core/models/MusicFile.h"
#include "../core/playback/QueueManager.h"
#include "../core/services/ImageCache.h"
#include "../core/services/SleepTimer.h"
#include "../core/theme/ThemeManager.h"
#include "../core/theme/MusicefyColorScheme.h"

#include <QFont>
#include <QHBoxLayout>
#include <QLabel>
#include <QMouseEvent>
#include <QPushButton>
#include <QSignalBlocker>
#include <QSlider>
#include <QSpacerItem>
#include <QVBoxLayout>

namespace mf::app::widgets {

using mf::app::viewmodels::PlayerViewModel;
using mf::core::models::MusicFile;
using mf::core::playback::QueueManager;
using mf::core::services::ImageCache;
using mf::core::theme::ThemeManager;
using mf::core::theme::MusicefyColorScheme;
using mf::core::services::SleepTimer;

namespace {

QString fmtTime(qint64 ms) {
    if (ms < 0) ms = 0;
    qint64 s = ms / 1000;
    return QStringLiteral("%1:%2")
        .arg(s / 60, 2, 10, QLatin1Char('0'))
        .arg(s % 60, 2, 10, QLatin1Char('0'));
}

} // namespace

NowPlayingBar::NowPlayingBar(PlayerViewModel* vm,
                             QueueManager*    queue,
                             ImageCache*      imageCache,
                             ThemeManager*    theme,
                             SleepTimer*      sleepTimer,
                             QWidget*         parent)
    : QFrame(parent)
    , vm_(vm)
    , queue_(queue)
    , imageCache_(imageCache)
    , theme_(theme)
    , sleepTimer_(sleepTimer)
{
    setObjectName(QStringLiteral("NowPlayingBar"));
    setFrameShape(QFrame::NoFrame);
    buildUi();
    wireViewModel();
    wireTheme();
    applyTheme();
    refreshFromViewModel();
}

void NowPlayingBar::buildUi() {
    // Left cluster: cover thumbnail + title + artist.
    auto* leftRow = new QHBoxLayout;
    leftRow->setContentsMargins(0, 0, 0, 0);
    leftRow->setSpacing(10);

    coverThumb_ = new CoverImage(imageCache_, this);
    coverThumb_->setFixedSize(60, 60);
    leftRow->addWidget(coverThumb_, /*stretch=*/0);

    auto* leftCol = new QVBoxLayout;
    leftCol->setContentsMargins(0, 0, 0, 0);
    leftCol->setSpacing(2);

    titleLabel_ = new QLabel(this);
    QFont tf = titleLabel_->font();
    tf.setPointSize(11);
    tf.setBold(true);
    titleLabel_->setFont(tf);
    titleLabel_->setText(QStringLiteral("Nothing playing"));

    artistLabel_ = new QLabel(this);
    QFont af = artistLabel_->font();
    af.setPointSize(9);
    artistLabel_->setFont(af);
    artistLabel_->setText(QString());
    artistLabel_->setProperty("role", QStringLiteral("secondary"));

    leftCol->addWidget(titleLabel_);
    leftCol->addWidget(artistLabel_);

    leftRow->addLayout(leftCol, /*stretch=*/1);

    auto* leftWrap = new QVBoxLayout;
    leftWrap->setContentsMargins(12, 8, 12, 8);
    leftWrap->addLayout(leftRow);
    auto* leftContainer = new QWidget(this);
    leftContainer->setLayout(leftWrap);
    leftContainer->setMinimumWidth(240);

    // Middle cluster: prev / play-pause / next + seek bar + time.
    auto* midCol = new QVBoxLayout;
    midCol->setContentsMargins(0, 0, 0, 0);
    midCol->setSpacing(4);

    auto* buttons = new QHBoxLayout;
    buttons->setSpacing(8);
    buttons->addStretch(1);
    prevBtn_      = new QPushButton(this);
    playPauseBtn_ = new QPushButton(this);
    nextBtn_      = new QPushButton(this);
    prevBtn_->setFixedSize(36, 36);
    playPauseBtn_->setFixedSize(44, 44);
    nextBtn_->setFixedSize(36, 36);
    prevBtn_->setIconSize(QSize(18, 18));
    playPauseBtn_->setIconSize(QSize(20, 20));
    nextBtn_->setIconSize(QSize(18, 18));
    prevBtn_->setProperty("role",      QStringLiteral("transport"));
    playPauseBtn_->setProperty("role", QStringLiteral("playPause"));
    nextBtn_->setProperty("role",      QStringLiteral("transport"));
    buttons->addWidget(prevBtn_);
    buttons->addWidget(playPauseBtn_);
    buttons->addWidget(nextBtn_);
    buttons->addStretch(1);
    midCol->addLayout(buttons);

    auto* seekRow = new QHBoxLayout;
    seekRow->setSpacing(8);
    timeLabel_ = new QLabel(QStringLiteral("00:00"), this);
    timeLabel_->setMinimumWidth(40);
    timeLabel_->setAlignment(Qt::AlignRight | Qt::AlignVCenter);
    seekSlider_ = new QSlider(Qt::Horizontal, this);
    seekSlider_->setRange(0, 1000);  // permille; converted to ms via duration
    seekSlider_->setEnabled(false);
    auto* timeRight = new QLabel(QStringLiteral("00:00"), this);
    timeRight->setMinimumWidth(40);
    timeRight->setObjectName(QStringLiteral("DurationLabel"));
    seekRow->addWidget(timeLabel_);
    seekRow->addWidget(seekSlider_, /*stretch=*/1);
    seekRow->addWidget(timeRight);
    midCol->addLayout(seekRow);

    auto* midContainer = new QWidget(this);
    midContainer->setLayout(midCol);

    // Right cluster: volume slider.
    auto* rightRow = new QHBoxLayout;
    rightRow->setContentsMargins(12, 0, 12, 0);
    rightRow->setSpacing(6);

    // Sleep timer indicator (hidden when inactive).
    sleepLabel_ = new QLabel(this);
    QFont slf = sleepLabel_->font();
    slf.setPointSize(8);
    sleepLabel_->setFont(slf);
    sleepLabel_->setProperty("role", QStringLiteral("sleepIndicator"));
    sleepLabel_->setAlignment(Qt::AlignRight | Qt::AlignVCenter);
    sleepLabel_->setMinimumWidth(60);
    sleepLabel_->hide();

    auto* volLabel = new QLabel(QStringLiteral("Vol"), this);
    QFont vf = volLabel->font();
    vf.setPointSize(9);
    volLabel->setFont(vf);
    volumeSlider_ = new QSlider(Qt::Horizontal, this);
    volumeSlider_->setRange(0, 100);
    volumeSlider_->setValue(100);
    volumeSlider_->setMinimumWidth(120);
    rightRow->addWidget(sleepLabel_);
    rightRow->addWidget(volLabel);
    rightRow->addWidget(volumeSlider_, /*stretch=*/1);
    auto* rightContainer = new QWidget(this);
    rightContainer->setLayout(rightRow);
    rightContainer->setMinimumWidth(180);

    auto* root = new QHBoxLayout(this);
    root->setContentsMargins(0, 0, 0, 0);
    root->setSpacing(0);
    root->addWidget(leftContainer,   /*stretch=*/0);
    root->addWidget(midContainer,    /*stretch=*/1, Qt::AlignCenter);
    root->addWidget(rightContainer,  /*stretch=*/0);
}

void NowPlayingBar::wireViewModel() {
    if (!vm_) return;

    connect(prevBtn_,      &QPushButton::clicked, this, &NowPlayingBar::onPreviousClicked);
    connect(playPauseBtn_, &QPushButton::clicked, this, &NowPlayingBar::onPlayPauseClicked);
    connect(nextBtn_,      &QPushButton::clicked, this, &NowPlayingBar::onNextClicked);

    connect(volumeSlider_, &QSlider::valueChanged,
            this, &NowPlayingBar::onVolumeChanged);

    connect(seekSlider_, &QSlider::sliderPressed,
            this, &NowPlayingBar::onSeekPressed);
    connect(seekSlider_, &QSlider::sliderReleased,
            this, &NowPlayingBar::onSeekReleased);
    connect(seekSlider_, &QSlider::sliderMoved,
            this, &NowPlayingBar::onSeekMoved);

    // VM → view: react to all the state-change signals.
    connect(vm_, &PlayerViewModel::currentTrackChanged,
            this, [this]() {
        titleLabel_->setText(vm_->currentTitle().isEmpty()
                             ? QStringLiteral("Nothing playing")
                             : vm_->currentTitle());
        artistLabel_->setText(vm_->currentArtist());
        // Cover art: pull the current track from the queue so we get
        // the full coverPath/coverUrl fields (the VM only exposes
        // title/artist/album as strings).
        if (coverThumb_) {
            QString coverSource;
            QString letter;
            if (queue_) {
                const MusicFile t = queue_->currentTrack();
                coverSource = !t.coverUrl().isEmpty() ? t.coverUrl() : t.coverPath();
                if (!t.title().isEmpty()) letter = t.title().left(1).toUpper();
            }
            if (letter.isEmpty()) letter = vm_->currentTitle().left(1).toUpper();
            coverThumb_->setSource(coverSource, letter);
        }
    });
    connect(vm_, &PlayerViewModel::isPlayingChanged, this, [this]() {
        MusicefyColorScheme s;
        if (theme_) s = theme_->scheme();
        const QColor onAcc = s.onPrimary.isValid() ? s.onPrimary : QColor("#ffffff");
        updatePlayPauseIcon(vm_->isPlaying(), onAcc);
    });
    connect(vm_, &PlayerViewModel::positionChanged, this, [this]() {
        if (seekSliderBeingDragged_) return;
        qint64 dur = vm_->durationMs();
        qint64 pos = vm_->positionMs();
        if (dur > 0) {
            QSignalBlocker block(seekSlider_);
            seekSlider_->setValue(int((double(pos) / double(dur)) * 1000.0));
        } else {
            QSignalBlocker block(seekSlider_);
            seekSlider_->setValue(0);
        }
        timeLabel_->setText(fmtTime(pos));
    });
    connect(vm_, &PlayerViewModel::durationChanged, this, [this]() {
        qint64 dur = vm_->durationMs();
        seekSlider_->setEnabled(dur > 0);
        // Also update the right-hand time label.
        if (auto* durLabel = findChild<QLabel*>(QStringLiteral("DurationLabel"))) {
            durLabel->setText(fmtTime(dur));
        }
    });
    connect(vm_, &PlayerViewModel::volumeChanged, this, [this]() {
        QSignalBlocker block(volumeSlider_);
        volumeSlider_->setValue(int(vm_->volume() * 100.0f));
    });
    connect(vm_, &PlayerViewModel::mutedChanged, this, [this]() {
        // Visual cue: a muted bar uses a different accent. Kept simple here.
        applyTheme();
    });
    connect(vm_, &PlayerViewModel::navigationChanged, this, [this]() {
        prevBtn_->setEnabled(vm_->hasPrevious());
        nextBtn_->setEnabled(vm_->hasNext());
    });

    // Sleep timer: show/hide indicator and update remaining time.
    if (sleepTimer_) {
        connect(sleepTimer_, &SleepTimer::activeChanged,
                this, &NowPlayingBar::onSleepTimerActiveChanged);
        connect(sleepTimer_, &SleepTimer::tick,
                this, &NowPlayingBar::onSleepTimerTick);
    }
}

void NowPlayingBar::wireTheme() {
    if (!theme_) return;
    connect(theme_, &ThemeManager::schemeChanged,
            this, &NowPlayingBar::applyTheme);
}

void NowPlayingBar::applyTheme() {
    MusicefyColorScheme s;
    if (theme_) s = theme_->scheme();
    if (!s.primary.isValid()) {
        // Fall back to the QApplication palette when no theme manager
        // is bound (e.g. inside a unit test).
        setStyleSheet(QString());
        return;
    }

    const QString bg     = s.surfaceContainerHigh.name();
    const QString onBg   = s.onSurface.name();
    const QString muted  = s.onSurfaceVariant.name();
    const QString accent = s.primary.name();
    const QString onAcc  = s.onPrimary.name();

    setStyleSheet(QStringLiteral(
        "QFrame#NowPlayingBar { background: %1; border-top: 1px solid %2; }"
        "QLabel { color: %3; background: transparent; }"
        "QLabel[role=\"secondary\"] { color: %4; }"
        "QLabel[role=\"sleepIndicator\"] { color: %6; font-weight: bold; }"
        "QPushButton {"
        "  color: %3; background: transparent; border: none;"
        "  border-radius: 18px; padding: 0;"
        "}"
        "QPushButton[role=\"playPause\"] {"
        "  color: %5; background: %6; border-radius: 22px;"
        "  font-size: 16pt; font-weight: bold;"
        "}"
        "QPushButton:hover:!pressed[role=\"transport\"] { background: %7; }"
        "QPushButton:hover:!pressed[role=\"playPause\"] { background: %8; }"
        "QPushButton:disabled { color: %9; }"
        "QSlider::groove:horizontal {"
        "  background: %10; height: 4px; border-radius: 2px;"
        "}"
        "QSlider::sub-page:horizontal {"
        "  background: %6; border-radius: 2px;"
        "}"
        "QSlider::handle:horizontal {"
        "  background: %3; width: 12px; height: 12px;"
        "  margin: -4px 0; border-radius: 6px;"
        "}"
    )
    .arg(bg)        // 1: bg
    .arg(s.outlineVariant.name())  // 2: border
    .arg(onBg)      // 3: text
    .arg(muted)     // 4: muted text
    .arg(onAcc)     // 5: play-pause text
    .arg(accent)    // 6: accent
    .arg(s.surfaceContainerHighest.name())  // 7: hover
    .arg(s.primaryContainer.name())          // 8: play-pause hover
    .arg(s.outlineVariant.name())            // 9: disabled
    .arg(s.surfaceContainerHighest.name())  // 10: groove
    );

    // Re-render transport icons with the current color scheme.
    prevBtn_->setIcon(SvgIcon::get("skip-back",   s.onSurface, 18));
    nextBtn_->setIcon(SvgIcon::get("skip-forward", s.onSurface, 18));
    updatePlayPauseIcon(vm_ && vm_->isPlaying(), s.onPrimary);
}

void NowPlayingBar::updatePlayPauseIcon(bool isPlaying, const QColor& color) {
    if (!playPauseBtn_) return;
    playPauseBtn_->setIcon(SvgIcon::get(isPlaying ? "pause" : "play",
                                        color, 20));
}

void NowPlayingBar::refreshFromViewModel() {
    if (!vm_) return;
    titleLabel_->setText(vm_->currentTitle().isEmpty()
                         ? QStringLiteral("Nothing playing")
                         : vm_->currentTitle());
    artistLabel_->setText(vm_->currentArtist());
    {
        QSignalBlocker block(volumeSlider_);
        volumeSlider_->setValue(int(vm_->volume() * 100.0f));
    }
    prevBtn_->setEnabled(vm_->hasPrevious());
    nextBtn_->setEnabled(vm_->hasNext());
    if (coverThumb_) {
        QString coverSource;
        QString letter;
        if (queue_) {
            const MusicFile t = queue_->currentTrack();
            coverSource = !t.coverUrl().isEmpty() ? t.coverUrl() : t.coverPath();
            if (!t.title().isEmpty()) letter = t.title().left(1).toUpper();
        }
        if (letter.isEmpty()) letter = vm_->currentTitle().left(1).toUpper();
        coverThumb_->setSource(coverSource, letter);
    }
    // The play/pause icon is rendered by applyTheme(); we just need
    // to refresh its playing/paused state here. applyTheme() has
    // already been called by the ctor, so the icon is current.
    MusicefyColorScheme s;
    if (theme_) s = theme_->scheme();
    const QColor onAcc = s.onPrimary.isValid() ? s.onPrimary : QColor("#ffffff");
    updatePlayPauseIcon(vm_->isPlaying(), onAcc);
}

void NowPlayingBar::onPlayPauseClicked()  { if (vm_) vm_->togglePlayPause(); }
void NowPlayingBar::onNextClicked()       { if (vm_) vm_->next(); }
void NowPlayingBar::onPreviousClicked()   { if (vm_) vm_->previous(); }

void NowPlayingBar::onVolumeChanged(int v) {
    if (vm_) vm_->setVolume(v / 100.0f);
}

void NowPlayingBar::onSeekPressed() {
    seekSliderBeingDragged_ = true;
}

void NowPlayingBar::onSeekReleased() {
    if (vm_) {
        vm_->seekPercent(seekSlider_->value() / 1000.0);
    }
    seekSliderBeingDragged_ = false;
}

void NowPlayingBar::onSeekMoved(int v) {
    qint64 dur = vm_ ? vm_->durationMs() : 0;
    if (dur > 0) {
        timeLabel_->setText(fmtTime(qint64(double(dur) * (v / 1000.0))));
    }
}

void NowPlayingBar::mousePressEvent(QMouseEvent* e) {
    if (e->button() == Qt::LeftButton) {
        emit expandRequested();
        e->accept();
        return;
    }
    QFrame::mousePressEvent(e);
}

void NowPlayingBar::onSleepTimerActiveChanged() {
    if (!sleepTimer_ || !sleepLabel_) return;
    if (sleepTimer_->isActive()) {
        sleepLabel_->show();
        onSleepTimerTick(sleepTimer_->remainingMs());
    } else {
        sleepLabel_->hide();
    }
}

void NowPlayingBar::onSleepTimerTick(qint64 remainingMs) {
    if (!sleepLabel_) return;
    if (remainingMs <= 0) {
        sleepLabel_->hide();
        return;
    }
    const qint64 s = remainingMs / 1000;
    const int min = static_cast<int>(s / 60);
    const int sec = static_cast<int>(s % 60);
    sleepLabel_->setText(QStringLiteral("Z %1:%2")
        .arg(min, 2, 10, QLatin1Char('0'))
        .arg(sec, 2, 10, QLatin1Char('0')));
}

// ──────────────────────────────────────────────────────────────────
// Mini-player mode: hides seek bar, volume slider, time labels,
// and shrinks the bar to a compact strip with just cover + title +
// play/pause/next.
// ──────────────────────────────────────────────────────────────────

void NowPlayingBar::toggleMiniPlayer() {
    miniPlayer_ = !miniPlayer_;
    applyMiniPlayerMode();
    emit miniPlayerToggled(miniPlayer_);
}

void NowPlayingBar::applyMiniPlayerMode() {
    if (miniPlayer_) {
        // Hide elements not needed in mini mode
        seekSlider_->hide();
        volumeSlider_->hide();
        timeLabel_->hide();
        sleepLabel_->hide();
        if (auto* durLabel = findChild<QLabel*>(QStringLiteral("DurationLabel")))
            durLabel->hide();
        // Hide the vol label
        if (auto* volLabel = findChild<QLabel*>())
            if (volLabel->text() == QStringLiteral("Vol"))
                volLabel->hide();
        // Set compact size
        setFixedHeight(56);
    } else {
        // Show all elements
        seekSlider_->show();
        volumeSlider_->show();
        timeLabel_->show();
        if (auto* durLabel = findChild<QLabel*>(QStringLiteral("DurationLabel")))
            durLabel->show();
        if (auto* volLabel = findChild<QLabel*>())
            if (volLabel->text() == QStringLiteral("Vol"))
                volLabel->show();
        setMinimumHeight(0);
        setMaximumHeight(QWIDGETSIZE_MAX);
    }
}

} // namespace mf::app::widgets
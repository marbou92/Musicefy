// NowPlayingView.h
// Expanded "Now Playing" overlay. Reachable via the bottom
// NowPlayingBar (which fires a `clicked` signal on press).
//
//   ┌──────────────────────────────────────────────────────┐
//   │ [← Back]                                             │
//   │                                                      │
//   │  ┌──────────┐  Up Next (queue)                        │
//   │  │          │  ┌─────────────────────────────────┐   │
//   │  │   ♪      │  │ 1.  Track A — Album X   3:42   │   │
//   │  │  COVER   │  │ 2.  Track B — Album Y   4:01   │   │
//   │  │ 240×240  │  │ ▶3. Track C — Album Z   2:58   │   │
//   │  │          │  │ 4.  …                           │   │
//   │  └──────────┘  └─────────────────────────────────┘   │
//   │                                                      │
//   │  Track Title                                         │
//   │  Artist — Album                                      │
//   │                                                      │
//   │  0:00 ────●─────────── 3:42                          │
//   │  [⏮]  [⏯]  [⏭]   🔊 ━━●━━                            │
//   │  [🔀 Shuffle]  [🔁 Repeat: Off]                       │
//   └──────────────────────────────────────────────────────┘
//
// All state is read from PlayerViewModel and QueueManager. Two-way
// controls (play/pause, seek, volume, queue-jump) write back to the
// same services.

#pragma once

#include <QWidget>

class QLabel;
class QListView;
class QPushButton;
class QSlider;
class QStandardItemModel;

namespace mf::app::viewmodels { class PlayerViewModel; }
namespace mf::core::playback   { class QueueManager; }
namespace mf::core::services   { class NavigationService; class ImageCache; class SleepTimer; }
namespace mf::core::theme      { class ThemeManager; }
namespace mf::app::widgets     { class CoverImage; class LyricsPanel; }

namespace mf::app::widgets {

class NowPlayingView : public QWidget {
    Q_OBJECT
public:
    NowPlayingView(mf::app::viewmodels::PlayerViewModel* vm,
                   mf::core::playback::QueueManager*     queue,
                   mf::core::services::NavigationService* nav,
                   mf::core::services::ImageCache*       imageCache,
                   mf::core::theme::ThemeManager*        theme,
                   mf::core::services::SleepTimer*       sleepTimer,
                   QWidget* parent = nullptr);
    ~NowPlayingView() override = default;

private slots:
    void onBackClicked();
    void onPlayPauseClicked();
    void onNextClicked();
    void onPrevClicked();
    void onShuffleClicked(bool on);
    void onRepeatClicked();
    void onVolumeChanged(int v);
    void onSeekPressed();
    void onSeekReleased();
    void onSeekMoved(int v);
    void onQueueDoubleClicked(const QModelIndex& idx);
    void onFavClicked();
    void onShareClicked();
    void onArtistClicked();
    void onAlbumClicked();
    void onLyricsClicked();

    void onSleepClicked();
    void onSleepTimerExpired();

    void onVmIsPlayingChanged();
    void onVmIsPausedChanged();
    void onVmCurrentTrackChanged();
    void onVmPositionChanged();
    void onVmDurationChanged();
    void onVmShuffleChanged();
    void onVmRepeatChanged();

    void onQueueQueueChanged();
    void onQueueIndexChanged();
    // Drag-reorder in the queue list. The QListView's InternalMove
    // has already mutated the model; we read the new order back and
    // push it to QueueManager via setOrderFromVisible.
    void onQueueRowsMoved(const QModelIndex& parent, int start, int end,
                          const QModelIndex& dest,   int destRow);

private:
    void buildUi();
    void applyTheme();
    QString formatMs(qint64 ms) const;
    void updateFavIcon();

    bool eventFilter(QObject* obj, QEvent* event) override;

    mf::app::viewmodels::PlayerViewModel* vm_    = nullptr;
    mf::core::playback::QueueManager*     queue_ = nullptr;
    mf::core::services::NavigationService* nav_   = nullptr;
    mf::core::services::ImageCache*       imageCache_ = nullptr;
    mf::core::theme::ThemeManager*        theme_ = nullptr;
    mf::core::services::SleepTimer*       sleepTimer_ = nullptr;

    QPushButton*        backBtn_    = nullptr;
    CoverImage*         coverLabel_ = nullptr;
    QLabel*             titleLabel_ = nullptr;
    QLabel*             artistLabel_= nullptr;
    QLabel*             albumLabel_ = nullptr;
    QLabel*             elapsedLabel_ = nullptr;
    QLabel*             totalLabel_   = nullptr;
    QLabel*             volIcon_     = nullptr;
    QSlider*            seekSlider_   = nullptr;
    QPushButton*        prevBtn_      = nullptr;
    QPushButton*        playPauseBtn_ = nullptr;
    QPushButton*        nextBtn_      = nullptr;
    QSlider*            volumeSlider_ = nullptr;
    QPushButton*        shuffleBtn_   = nullptr;
    QPushButton*        repeatBtn_    = nullptr;
    QPushButton*        sleepBtn_     = nullptr;
    QPushButton*        favBtn_       = nullptr;
    QPushButton*        shareBtn_     = nullptr;
    QPushButton*        lyricsBtn_    = nullptr;
    QLabel*             formatLabel_  = nullptr;
    LyricsPanel*        lyricsPanel_  = nullptr;

    QListView*          queueList_   = nullptr;
    QStandardItemModel* queueModel_  = nullptr;

    bool seekSliderBeingDragged_ = false;
};

} // namespace mf::app::widgets

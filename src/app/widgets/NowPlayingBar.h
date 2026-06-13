// NowPlayingBar.h
// Bottom-of-window playback control bar. Stateless: it pulls all of
// its data from a PlayerViewModel and re-pulls on every NOTIFY signal.
// All visual styling is recomputed from a ThemeManager; switching
// theme or mode triggers a re-style with zero extra plumbing.

#pragma once

#include <QColor>
#include <QFrame>
#include <memory>

class QLabel;
class QPushButton;
class QSlider;

namespace mf::app::viewmodels { class PlayerViewModel; }
namespace mf::core::playback   { class QueueManager; }
namespace mf::core::services   { class ImageCache; class SleepTimer; }
namespace mf::core::theme      { class ThemeManager; }
namespace mf::app::widgets     { class CoverImage; }

namespace mf::app::widgets {

class NowPlayingBar : public QFrame {
    Q_OBJECT
public:
    NowPlayingBar(mf::app::viewmodels::PlayerViewModel* vm,
                  mf::core::playback::QueueManager*     queue,
                  mf::core::services::ImageCache*       imageCache,
                  mf::core::theme::ThemeManager*        theme,
                  mf::core::services::SleepTimer*       sleepTimer,
                  QWidget* parent = nullptr);
    ~NowPlayingBar() override = default;

    QSize sizeHint() const override { return QSize(800, 84); }
    QSize minimumSizeHint() const override { return QSize(400, 84); }

    /// Toggle between full and mini player modes.
    Q_INVOKABLE void toggleMiniPlayer();
    bool isMiniPlayer() const { return miniPlayer_; }

signals:
    void expandRequested();
    void miniPlayerToggled(bool isMini);

protected:
    void mousePressEvent(QMouseEvent* e) override;

private slots:
    void onPlayPauseClicked();
    void onNextClicked();
    void onPreviousClicked();
    void onVolumeChanged(int v);
    void onSeekPressed();
    void onSeekReleased();
    void onSeekMoved(int v);
    void onSleepTimerTick(qint64 remainingMs);
    void onSleepTimerActiveChanged();

private:
    void buildUi();
    void wireViewModel();
    void wireTheme();
    void applyTheme();
    void refreshFromViewModel();
    void updatePlayPauseIcon(bool isPlaying, const QColor& color);
    void applyMiniPlayerMode();

    mf::app::viewmodels::PlayerViewModel* vm_          = nullptr;
    mf::core::playback::QueueManager*     queue_       = nullptr;
    mf::core::services::ImageCache*       imageCache_  = nullptr;
    mf::core::theme::ThemeManager*        theme_       = nullptr;
    mf::core::services::SleepTimer*       sleepTimer_  = nullptr;

    CoverImage* coverThumb_   = nullptr;
    QLabel*     titleLabel_    = nullptr;
    QLabel*     artistLabel_   = nullptr;
    QLabel*     timeLabel_     = nullptr;
    QLabel*     sleepLabel_    = nullptr;
    QPushButton* prevBtn_      = nullptr;
    QPushButton* playPauseBtn_ = nullptr;
    QPushButton* nextBtn_      = nullptr;
    QSlider*    seekSlider_    = nullptr;
    QSlider*    volumeSlider_  = nullptr;

    bool seekSliderBeingDragged_ = false;
    bool miniPlayer_ = false;
};

} // namespace mf::app::widgets
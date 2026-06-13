// SleepTimer.h
// Countdown timer that pauses playback after a configurable delay.
// Preset durations: 15, 30, 60, 90, 120 minutes, or "end of track".
// Emits tick signals so the UI can show remaining time, and a
// timerExpired signal when the countdown reaches zero.

#pragma once

#include <QObject>
#include <QTimer>
#include <QElapsedTimer>

namespace mf::core::playback { class PlaybackService; }

namespace mf::core::services {

class SleepTimer : public QObject {
    Q_OBJECT
    Q_PROPERTY(bool     active     READ isActive   NOTIFY activeChanged)
    Q_PROPERTY(qint64   remainingMs READ remainingMs NOTIFY tick)
    Q_PROPERTY(int      presetMinutes READ presetMinutes NOTIFY activeChanged)

public:
    enum Preset {
        Off       = 0,
        Minutes15 = 15,
        Minutes30 = 30,
        Minutes60 = 60,
        Minutes90 = 90,
        Minutes120 = 120,
        EndOfTrack = -1
    };
    Q_ENUM(Preset)

    explicit SleepTimer(mf::core::playback::PlaybackService* playback,
                        QObject* parent = nullptr);
    ~SleepTimer() override = default;

    bool   isActive()      const { return active_; }
    qint64 remainingMs()   const;
    int    presetMinutes() const { return preset_; }

    /// Start the timer with the given preset.
    void start(Preset preset);
    /// Cancel any running timer.
    void cancel();

public slots:
    /// Called by PlaybackService when a track ends. If EndOfTrack
    /// mode is active, pause playback.
    void onTrackEnded();

signals:
    void activeChanged();
    void tick(qint64 remainingMs);
    void timerExpired();

private slots:
    void onTick();

private:
    mf::core::playback::PlaybackService* playback_ = nullptr;
    QTimer       timer_;
    QElapsedTimer elapsed_;
    bool         active_ = false;
    int          preset_ = 0;
    qint64       totalMs_ = 0;
};

} // namespace mf::core::services

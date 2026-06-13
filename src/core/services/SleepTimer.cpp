// SleepTimer.cpp
// See header.

#include "SleepTimer.h"

#include "../playback/PlaybackService.h"

#include <QCoreApplication>

namespace mf::core::services {

using mf::core::playback::PlaybackService;

SleepTimer::SleepTimer(PlaybackService* playback, QObject* parent)
    : QObject(parent)
    , playback_(playback)
{
    connect(&timer_, &QTimer::timeout, this, &SleepTimer::onTick);
}

qint64 SleepTimer::remainingMs() const {
    if (!active_) return 0;
    const qint64 elapsed = elapsed_.elapsed();
    return qMax(qint64{0}, totalMs_ - elapsed);
}

void SleepTimer::start(Preset preset) {
    cancel();

    if (preset == Off) return;

    preset_ = static_cast<int>(preset);
    active_ = true;
    emit activeChanged();

    if (preset == EndOfTrack) {
        // No countdown — onTrackEnded() will handle it.
        return;
    }

    totalMs_ = qint64(preset) * 60 * 1000;
    elapsed_.start();
    // Tick every second for UI updates.
    timer_.start(1000);
    emit tick(totalMs_);
}

void SleepTimer::cancel() {
    timer_.stop();
    const bool wasActive = active_;
    active_ = false;
    preset_ = 0;
    totalMs_ = 0;
    if (wasActive) {
        emit activeChanged();
        emit tick(0);
    }
}

void SleepTimer::onTrackEnded() {
    if (active_ && preset_ == EndOfTrack) {
        if (playback_) {
            playback_->pause();
        }
        emit timerExpired();
        cancel();
    }
}

void SleepTimer::onTick() {
    const qint64 rem = remainingMs();
    emit tick(rem);
    if (rem <= 0) {
        if (playback_) {
            playback_->pause();
        }
        emit timerExpired();
        cancel();
    }
}

} // namespace mf::core::services

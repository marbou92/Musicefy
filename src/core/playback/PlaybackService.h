// PlaybackService.h
// QMediaPlayer-based implementation of IAudioPlayer. The central playback
// component. Tracks position, duration, state, volume. Emits signals
// to subscribers (UI, SMTC, scrobble log).

#pragma once

#include "../interfaces/IAudioPlayer.h"

#include <QMediaPlayer>
#include <QAudioOutput>

#include <memory>

namespace mf::core::playback {

class PlaybackService : public QObject, public mf::core::interfaces::IAudioPlayer {
    Q_OBJECT
public:
    explicit PlaybackService(QObject* parent = nullptr);
    ~PlaybackService() override;

    // IAudioPlayer ──────────────────────────────────────────────────────
    void play() override;
    void pause() override;
    void stop() override;
    void togglePlayPause() override;
    void seek(std::chrono::milliseconds position) override;
    void setVolume(float volume) override;
    float volume() const override;
    void setMuted(bool muted) override;
    bool isMuted() const override;

    void setTrack(mf::core::models::MusicFile track) override;
    mf::core::models::MusicFile currentTrack() const override { return currentTrack_; }
    std::chrono::milliseconds position() const override;
    std::chrono::milliseconds duration() const override;
    PlaybackState state() const override;

    void setOnStateChanged(StateChangedCallback cb) override { onStateChanged_ = std::move(cb); }
    void setOnTrackChanged(TrackChangedCallback cb) override { onTrackChanged_ = std::move(cb); }
    void setOnPositionChanged(PositionChangedCallback cb) override { onPositionChanged_ = std::move(cb); }
    void setOnError(ErrorCallback cb) override { onError_ = std::move(cb); }

    // Direct access for components that need it (e.g. SMTC metadata).
    QMediaPlayer* mediaPlayer() { return player_.get(); }

signals:
    // Qt-native signals for QML / QProperty bindings.
    void stateChangedQ(int state);
    void trackChangedQ();
    void positionChangedQ(qint64 ms);
    void durationChangedQ(qint64 ms);
    void volumeChangedQ(float v);
    void mutedChangedQ(bool m);
    void errorOccurredQ(QString message);

private slots:
    void onMediaStateChanged(QMediaPlayer::State s);
    void onMediaStatusChanged(QMediaPlayer::MediaStatus status);
    void onPositionChanged(qint64 pos);
    void onDurationChanged(qint64 dur);
    void onErrorOccurred(int err);

private:
    void emitState();

    // Owned via unique_ptr (no QObject parent) so the destruction order is
    // well-defined: unique_ptr destroys them after ~QObject's children walk.
    std::unique_ptr<QAudioOutput>  audioOutput_;
    std::unique_ptr<QMediaPlayer>  player_;
    mf::core::models::MusicFile    currentTrack_;
    PlaybackState                  lastEmittedState_ = PlaybackState::Stopped;

    StateChangedCallback           onStateChanged_;
    TrackChangedCallback           onTrackChanged_;
    PositionChangedCallback        onPositionChanged_;
    ErrorCallback                  onError_;
};

} // namespace mf::core::playback
#pragma once

#include "../models/MusicFile.h"

#include <QObject>

#include <functional>
#include <memory>

namespace mf::core::interfaces {

class IAudioPlayer {
public:
    virtual ~IAudioPlayer() = default;

    using StateChangedCallback = std::function<void(int)>;     // PlaybackState enum as int
    using TrackChangedCallback = std::function<void(mf::core::models::MusicFile)>;
    using PositionChangedCallback = std::function<void(std::chrono::milliseconds)>;
    using ErrorCallback = std::function<void(QString)>;

    enum class PlaybackState { Stopped, Playing, Paused, Buffering, Error };

    virtual void play() = 0;
    virtual void pause() = 0;
    virtual void stop() = 0;
    virtual void togglePlayPause() = 0;
    virtual void seek(std::chrono::milliseconds position) = 0;
    virtual void setVolume(float volume) = 0;
    virtual float volume() const = 0;
    virtual void setMuted(bool muted) = 0;
    virtual bool isMuted() const = 0;

    virtual void setTrack(mf::core::models::MusicFile track) = 0;
    virtual mf::core::models::MusicFile currentTrack() const = 0;
    virtual std::chrono::milliseconds position() const = 0;
    virtual std::chrono::milliseconds duration() const = 0;
    virtual PlaybackState state() const = 0;

    virtual void setOnStateChanged(StateChangedCallback cb) = 0;
    virtual void setOnTrackChanged(TrackChangedCallback cb) = 0;
    virtual void setOnPositionChanged(PositionChangedCallback cb) = 0;
    virtual void setOnError(ErrorCallback cb) = 0;
};

} // namespace mf::core::interfaces

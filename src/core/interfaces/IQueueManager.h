#pragma once

#include "../models/MusicFile.h"

#include <QObject>

#include <functional>

namespace mf::core::interfaces {

class IQueueManager {
public:
    virtual ~IQueueManager() = default;

    using QueueChangedCallback = std::function<void()>;
    using IndexChangedCallback = std::function<void(int)>;
    using ShuffleChangedCallback = std::function<void(bool)>;
    using RepeatChangedCallback = std::function<void(int)>; // 0=Off, 1=All, 2=One

    enum class RepeatMode { Off, All, One };

    virtual void enqueue(mf::core::models::MusicFile track) = 0;
    virtual void enqueueMany(QList<mf::core::models::MusicFile> tracks) = 0;
    virtual void dequeueAt(int index) = 0;
    virtual void move(int from, int to) = 0;
    virtual void setOrderFromVisible(const QList<mf::core::models::MusicFile>& newOrder) = 0;
    virtual void clear() = 0;

    virtual QList<mf::core::models::MusicFile> tracks() const = 0;
    virtual mf::core::models::MusicFile trackAt(int index) const = 0;
    virtual int count() const = 0;
    virtual int currentIndex() const = 0;
    virtual mf::core::models::MusicFile currentTrack() const = 0;

    virtual void setCurrentIndex(int index) = 0;
    virtual void next() = 0;
    virtual void previous() = 0;
    virtual bool hasNext() const = 0;
    virtual bool hasPrevious() const = 0;

    virtual void setShuffle(bool enabled) = 0;
    virtual bool isShuffle() const = 0;
    virtual void setRepeatMode(RepeatMode mode) = 0;
    virtual RepeatMode repeatMode() const = 0;

    virtual void persist() const = 0;
    virtual void restore() = 0;

    virtual void setOnQueueChanged(QueueChangedCallback cb) = 0;
    virtual void setOnIndexChanged(IndexChangedCallback cb) = 0;
    virtual void setOnShuffleChanged(ShuffleChangedCallback cb) = 0;
    virtual void setOnRepeatChanged(RepeatChangedCallback cb) = 0;
};

} // namespace mf::core::interfaces

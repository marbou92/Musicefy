// QueueManager.h
// Tracks the current playback queue, current index, shuffle & repeat modes.
// Persists state to the `queue` table in SQLite via LibraryRepository.
//
// Shuffle is implemented as a derived index permutation: the original
// (unshuffled) list is preserved and the visible order is a permutation
// of it. Disabling shuffle restores the original order.

#pragma once

#include "../interfaces/IQueueManager.h"

#include <QList>
#include <QObject>
#include <QString>

#include <random>

namespace mf::core::database {
class LibraryRepository;
}

namespace mf::core::playback {

class QueueManager : public QObject, public mf::core::interfaces::IQueueManager {
    Q_OBJECT
public:
    explicit QueueManager(QObject* parent = nullptr);
    QueueManager(mf::core::database::LibraryRepository* repo, QObject* parent = nullptr);
    ~QueueManager() override;

    // IQueueManager ──────────────────────────────────────────────────────
    void enqueue(mf::core::models::MusicFile track) override;
    void enqueueMany(QList<mf::core::models::MusicFile> tracks) override;
    void dequeueAt(int index) override;
    void move(int from, int to) override;
    void setOrderFromVisible(const QList<mf::core::models::MusicFile>& newOrder) override;
    void clear() override;

    QList<mf::core::models::MusicFile> tracks() const override;
    mf::core::models::MusicFile trackAt(int index) const override;
    int count() const override { return visibleOrder_.size(); }
    int currentIndex() const override { return currentIndex_; }
    mf::core::models::MusicFile currentTrack() const override;

    void setCurrentIndex(int index) override;
    void next() override;
    void previous() override;
    bool hasNext() const override;
    bool hasPrevious() const override;

    void setShuffle(bool enabled) override;
    bool isShuffle() const override { return shuffle_; }
    void setRepeatMode(RepeatMode mode) override { repeatMode_ = mode; emitRepeatChanged(); }
    RepeatMode repeatMode() const override { return repeatMode_; }

    void persist() const override;
    void restore() override;
    bool needsPersist() const;

    void setOnQueueChanged(QueueChangedCallback cb) override { onQueueChanged_ = std::move(cb); }
    void setOnIndexChanged(IndexChangedCallback cb) override { onIndexChanged_ = std::move(cb); }
    void setOnShuffleChanged(ShuffleChangedCallback cb) override { onShuffleChanged_ = std::move(cb); }
    void setOnRepeatChanged(RepeatChangedCallback cb) override { onRepeatChanged_ = std::move(cb); }

signals:
    void queueChangedQ();
    void indexChangedQ(int idx);
    void shuffleChangedQ(bool enabled);
    void repeatChangedQ(int mode);

private:
    void rebuildShufflePermutation();
    void emitQueueChanged();
    void emitIndexChanged();
    void emitShuffleChanged();
    void emitRepeatChanged();

    QList<mf::core::models::MusicFile> originalOrder_; // insertion order
    QList<int>                         visibleOrder_;   // indices into originalOrder_
    QList<mf::core::models::MusicFile> resolvedVisible_() const;
    int                                currentIndex_ = -1;
    bool                               shuffle_ = false;
    RepeatMode                         repeatMode_ = RepeatMode::Off;

    std::mt19937                       rng_;

    mf::core::database::LibraryRepository* repo_ = nullptr;

    QueueChangedCallback               onQueueChanged_;
    IndexChangedCallback               onIndexChanged_;
    ShuffleChangedCallback             onShuffleChanged_;
    RepeatChangedCallback              onRepeatChanged_;
};

} // namespace mf::core::playback

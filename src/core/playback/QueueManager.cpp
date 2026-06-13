// QueueManager.cpp
// See header for design notes.

#include "QueueManager.h"

#include "../database/LibraryRepository.h"

#include <algorithm>
#include <random>

#include <QJsonArray>
#include <QJsonDocument>
#include <QJsonObject>

namespace mf::core::playback {

using mf::core::interfaces::IQueueManager;
using mf::core::models::MusicFile;

QueueManager::QueueManager(QObject* parent)
    : QObject(parent)
    , rng_(std::random_device{}())
{
}

QueueManager::QueueManager(mf::core::database::LibraryRepository* repo, QObject* parent)
    : QObject(parent)
    , repo_(repo)
    , rng_(std::random_device{}())
{
}

QueueManager::~QueueManager() = default;

QList<MusicFile> QueueManager::resolvedVisible_() const {
    QList<MusicFile> out;
    out.reserve(visibleOrder_.size());
    for (int idx : visibleOrder_) {
        out.append(originalOrder_[idx]);
    }
    return out;
}

void QueueManager::enqueue(MusicFile track) {
    originalOrder_.append(std::move(track));
    rebuildShufflePermutation();
    if (currentIndex_ < 0 && !originalOrder_.isEmpty()) {
        currentIndex_ = 0;
        emitIndexChanged();
    }
    emitQueueChanged();
}

void QueueManager::enqueueMany(QList<MusicFile> tracks) {
    if (tracks.isEmpty()) {
        return;
    }
    originalOrder_.append(std::move(tracks));
    rebuildShufflePermutation();
    if (currentIndex_ < 0 && !originalOrder_.isEmpty()) {
        currentIndex_ = 0;
        emitIndexChanged();
    }
    emitQueueChanged();
}

void QueueManager::dequeueAt(int index) {
    if (index < 0 || index >= visibleOrder_.size()) {
        return;
    }
    int originalIdx = visibleOrder_[index];
    originalOrder_.removeAt(originalIdx);
    rebuildShufflePermutation();
    if (currentIndex_ >= visibleOrder_.size()) {
        currentIndex_ = visibleOrder_.isEmpty() ? -1 : visibleOrder_.size() - 1;
        emitIndexChanged();
    }
    emitQueueChanged();
}

void QueueManager::move(int from, int to) {
    if (from < 0 || from >= originalOrder_.size()) return;
    if (to   < 0 || to   >= originalOrder_.size()) return;
    if (from == to) return;

    // Remember the currently-playing track so we can re-locate it
    // after the take+insert + shuffle rebuild. filePath-based
    // equality (MusicFile::operator==) is stable across mutations
    // and works even when MusicFile::id() is unset.
    MusicFile currentTrack;
    if (currentIndex_ >= 0 && currentIndex_ < visibleOrder_.size()) {
        currentTrack = originalOrder_[visibleOrder_[currentIndex_]];
    }

    auto track = originalOrder_.takeAt(from);
    originalOrder_.insert(to, std::move(track));
    rebuildShufflePermutation();

    // Re-locate the currently-playing track in the new visible order.
    if (!currentTrack.filePath().isEmpty()) {
        for (int i = 0; i < visibleOrder_.size(); ++i) {
            if (originalOrder_[visibleOrder_[i]] == currentTrack) {
                currentIndex_ = i;
                break;
            }
        }
    }
    emitQueueChanged();
}

void QueueManager::setOrderFromVisible(
    const QList<MusicFile>& newOrder) {
    // Used by the UI after a model-driven drag-drop. The new order
    // is what the user sees; we mirror it as the new originalOrder_
    // and rebuild the shuffle permutation from scratch. The
    // currently-playing track is re-located by filePath identity.
    if (newOrder.size() != originalOrder_.size()) return;

    MusicFile currentTrack;
    if (currentIndex_ >= 0 && currentIndex_ < visibleOrder_.size()) {
        currentTrack = originalOrder_[visibleOrder_[currentIndex_]];
    }

    originalOrder_ = newOrder;
    rebuildShufflePermutation();

    if (!currentTrack.filePath().isEmpty()) {
        for (int i = 0; i < visibleOrder_.size(); ++i) {
            if (originalOrder_[visibleOrder_[i]] == currentTrack) {
                currentIndex_ = i;
                break;
            }
        }
    }
    emitQueueChanged();
}

void QueueManager::clear() {
    originalOrder_.clear();
    visibleOrder_.clear();
    currentIndex_ = -1;
    emitIndexChanged();
    emitQueueChanged();
}

QList<MusicFile> QueueManager::tracks() const {
    return resolvedVisible_();
}

MusicFile QueueManager::trackAt(int index) const {
    if (index < 0 || index >= visibleOrder_.size()) {
        return MusicFile();
    }
    return originalOrder_[visibleOrder_[index]];
}

MusicFile QueueManager::currentTrack() const {
    if (currentIndex_ < 0 || currentIndex_ >= visibleOrder_.size()) {
        return MusicFile();
    }
    return originalOrder_[visibleOrder_[currentIndex_]];
}

void QueueManager::setCurrentIndex(int index) {
    if (index == currentIndex_) {
        return;
    }
    if (index < -1 || index >= visibleOrder_.size()) {
        return;
    }
    currentIndex_ = index;
    emitIndexChanged();
}

void QueueManager::next() {
    if (visibleOrder_.isEmpty()) {
        return;
    }
    if (currentIndex_ < visibleOrder_.size() - 1) {
        currentIndex_++;
        emitIndexChanged();
        return;
    }
    if (repeatMode_ == RepeatMode::All) {
        currentIndex_ = 0;
        emitIndexChanged();
    }
}

void QueueManager::previous() {
    if (visibleOrder_.isEmpty()) {
        return;
    }
    if (currentIndex_ > 0) {
        currentIndex_--;
        emitIndexChanged();
        return;
    }
    if (repeatMode_ == RepeatMode::All) {
        currentIndex_ = visibleOrder_.size() - 1;
        emitIndexChanged();
    }
}

bool QueueManager::hasNext() const {
    if (visibleOrder_.isEmpty()) return false;
    if (currentIndex_ < visibleOrder_.size() - 1) return true;
    return repeatMode_ == RepeatMode::All;
}

bool QueueManager::hasPrevious() const {
    if (visibleOrder_.isEmpty()) return false;
    if (currentIndex_ > 0) return true;
    return repeatMode_ == RepeatMode::All;
}

void QueueManager::setShuffle(bool enabled) {
    if (shuffle_ == enabled) {
        return;
    }
    shuffle_ = enabled;
    rebuildShufflePermutation();
    emitShuffleChanged();
    emitQueueChanged();
}

void QueueManager::rebuildShufflePermutation() {
    int n = originalOrder_.size();
    visibleOrder_.clear();
    visibleOrder_.reserve(n);
    for (int i = 0; i < n; ++i) {
        visibleOrder_.append(i);
    }
    if (shuffle_ && n > 1) {
        std::shuffle(visibleOrder_.begin(), visibleOrder_.end(), rng_);
    }
    // currentIndex_ is a position into the *visible* list, so it stays
    // valid across re-permutations. The user keeps playing the same track
    // they were playing before, even if its slot in the list moved.
    if (currentIndex_ >= n) {
        currentIndex_ = n - 1;
    }
}

void QueueManager::persist() const {
    if (!repo_) {
        return;
    }

    QJsonObject root;
    // File paths in original insertion order
    QJsonArray paths;
    for (const auto& track : originalOrder_) {
        paths.append(track.filePath());
    }
    root["paths"] = paths;
    root["currentIndex"] = currentIndex_;
    root["shuffle"] = shuffle_;
    root["repeatMode"] = static_cast<int>(repeatMode_);

    QJsonDocument doc(root);
    repo_->setAppState("queue_state_v1", QString::fromUtf8(doc.toJson(QJsonDocument::Compact)));
}

void QueueManager::restore() {
    if (!repo_) {
        return;
    }

    auto jsonStr = repo_->appState("queue_state_v1");
    if (!jsonStr.has_value() || jsonStr->isEmpty()) {
        return;
    }

    QJsonDocument doc = QJsonDocument::fromJson(jsonStr->toUtf8());
    if (doc.isNull() || !doc.isObject()) {
        return;
    }

    QJsonObject root = doc.object();
    QJsonArray paths = root["paths"].toArray();

    // Clear existing state without emitting signals
    originalOrder_.clear();
    visibleOrder_.clear();
    currentIndex_ = -1;

    // Resolve each persisted path back to a MusicFile via the repository
    for (const auto& val : paths) {
        QString path = val.toString();
        if (path.isEmpty()) continue;
        auto track = repo_->trackByPath(path);
        if (track.has_value()) {
            originalOrder_.append(std::move(*track));
        }
    }

    // Restore shuffle / repeat / index
    shuffle_ = root["shuffle"].toBool(false);
    repeatMode_ = static_cast<RepeatMode>(root["repeatMode"].toInt(0));

    rebuildShufflePermutation();

    int savedIndex = root["currentIndex"].toInt(-1);
    if (savedIndex >= 0 && savedIndex < visibleOrder_.size()) {
        currentIndex_ = savedIndex;
    } else if (!originalOrder_.isEmpty()) {
        currentIndex_ = 0;
    }
}

bool QueueManager::needsPersist() const {
    return !originalOrder_.isEmpty();
}

void QueueManager::emitQueueChanged() {
    if (onQueueChanged_) onQueueChanged_();
    emit queueChangedQ();
}

void QueueManager::emitIndexChanged() {
    if (onIndexChanged_) onIndexChanged_(currentIndex_);
    emit indexChangedQ(currentIndex_);
}

void QueueManager::emitShuffleChanged() {
    if (onShuffleChanged_) onShuffleChanged_(shuffle_);
    emit shuffleChangedQ(shuffle_);
}

void QueueManager::emitRepeatChanged() {
    if (onRepeatChanged_) onRepeatChanged_(int(repeatMode_));
    emit repeatChangedQ(int(repeatMode_));
}

} // namespace mf::core::playback
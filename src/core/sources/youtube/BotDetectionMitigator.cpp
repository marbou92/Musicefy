// BotDetectionMitigator.cpp
// See header.

#include "BotDetectionMitigator.h"

namespace mf::core::sources::youtube {

BotDetectionMitigator::BotDetectionMitigator() = default;

void BotDetectionMitigator::notifyPlaybackSuccess() {
    consecutiveFailures_ = 0;
    rotationPending_ = false;
}

void BotDetectionMitigator::notifyPlaybackFailure() {
    ++consecutiveFailures_;
    if (consecutiveFailures_ >= rotationThreshold_) {
        rotationPending_ = true;
    }
}

bool BotDetectionMitigator::shouldRotateVisitorData() const {
    return rotationPending_;
}

void BotDetectionMitigator::reset() {
    consecutiveFailures_ = 0;
    rotationPending_ = false;
}

void BotDetectionMitigator::setRotationThreshold(int threshold) {
    rotationThreshold_ = threshold;
}

} // namespace mf::core::sources::youtube

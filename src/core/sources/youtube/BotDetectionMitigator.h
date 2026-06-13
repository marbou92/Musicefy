// BotDetectionMitigator.h
// Tracks consecutive YouTube playback failures and triggers visitor-data
// rotation when a threshold is exceeded. This mitigates YouTube's bot
// detection which flags repeated failures from the same visitor.
//
// Usage:
//   1. Before each InnerTube request, call shouldRotateVisitorData().
//      If true, generate new visitor data and set it on the
//      InnerTubeClient.
//   2. After a successful playback, call notifyPlaybackSuccess().
//   3. After a failed playback (HTTP 4xx, playability error, cipher
//      failure), call notifyPlaybackFailure().
//
// The mitigator is per-session (each YouTubeSession owns one).

#pragma once

#include <QString>

namespace mf::core::sources::youtube {

class BotDetectionMitigator {
public:
    BotDetectionMitigator();

    // Call after a successful stream URL resolution. Resets the
    // consecutive failure counter.
    void notifyPlaybackSuccess();

    // Call after a failed stream URL resolution (any reason:
    // HTTP error, playability status, cipher failure, etc.).
    // Increments the consecutive failure counter.
    void notifyPlaybackFailure();

    // Returns true if the consecutive failure count has reached
    // the rotation threshold. The caller should generate new
    // visitor data and call reset() after rotating.
    bool shouldRotateVisitorData() const;

    // Reset the consecutive failure counter and the rotation flag.
    // Called after visitor data has been rotated.
    void reset();

    // Read-only accessors for testing.
    int  consecutiveFailures() const { return consecutiveFailures_; }
    bool rotationPending()     const { return rotationPending_; }

    // Configurable threshold (default: 3 consecutive failures).
    void setRotationThreshold(int threshold);
    int  rotationThreshold() const { return rotationThreshold_; }

private:
    int  consecutiveFailures_ = 0;
    int  rotationThreshold_   = 3;
    bool rotationPending_     = false;
};

} // namespace mf::core::sources::youtube

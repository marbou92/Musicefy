// SmtcController.h
// Wrapper around the Windows System Media Transport Controls (SMTC).
// SMTC is the lock-screen / volume-flyout / Cortana / global media keys
// integration on Windows 8+.
//
// On Windows 7 this class is a no-op: Win 7 has no SMTC, so we just log
// metadata updates and ignore commands. The contract is the same.
//
// On Windows 8+ the implementation uses the WinRT
// Windows.Media.SystemMediaTransportControls class. WinRT requires:
//   - /ZW compiler flag (consume WinRT), OR
//   - C++/WinRT headers (no flag needed; just include <winrt/...>)
// We use the C++/WinRT path because it does not require linking to
// platform projections for every TURN.
//
// This build intentionally ships the stub. The full WinRT path is gated by
// MUSICEFY_ENABLE_WINRT_SRTC (and the SmtcController::isSupported() check
// at runtime). The stub still calls setOnCommand() / commandReceivedQ()
// so the rest of the app can be wired identically.

#pragma once

#include "../models/MusicFile.h"

#include <QObject>

#include <functional>

namespace mf::core::playback {

enum class SmtcCommand {
    Play,
    Pause,
    PlayPauseToggle,
    Next,
    Previous,
    Stop,
};

class SmtcController : public QObject {
    Q_OBJECT
public:
    using CommandCallback = std::function<void(SmtcCommand)>;

    explicit SmtcController(QObject* parent = nullptr);
    ~SmtcController() override;

    // Returns true on Windows 8+ when the real WinRT ISystemMediaTransportControls
    // is wired in. Always false on Windows 7 (and on the stub build).
    static bool isSupported();

    // Update displayed metadata. Cover art is shown if a valid path/URL is
    // provided; otherwise SMTC falls back to a default thumbnail.
    void updateMetadata(const mf::core::models::MusicFile& track);
    void clearMetadata();

    // Update playback status (Playing / Paused / Stopped / Changing).
    void updatePlaybackStatus(bool isPlaying, bool isPaused, bool isBuffering);

    // Update timeline (current position, total duration, last known).
    // Throttle: the OS only refreshes a few times per second, so this is
    // safe to call from a QTimer or onPositionChanged.
    void updateTimeline(qint64 positionMs, qint64 durationMs);

    void setOnCommand(CommandCallback cb) { onCommand_ = std::move(cb); }

signals:
    void commandReceivedQ(int cmd); // SmtcCommand as int

private:
    struct Impl;
    Impl* impl_ = nullptr;

    CommandCallback onCommand_;
};

} // namespace mf::core::playback

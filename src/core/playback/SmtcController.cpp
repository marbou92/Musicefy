// SmtcController.cpp
// Wrapper around the Windows System Media Transport Controls (SMTC).
// Two build modes:
//   - Stub (default): logs metadata updates and never fires the
//     command callback. Used on Win 7 where WinRT ISystemMediaTransportControls
//     is unavailable.
//   - Real WinRT (MUSICEFY_ENABLE_WINRT_SRTC): uses C++/WinRT to wire the
//     real lock-screen / volume-flyout / Cortana / media-key surface to
//     PlaybackService + QueueManager. Requires the C++/WinRT NuGet package
//     (FetchContent in the root CMakeLists) and winrt::init_apartment in
//     main.cpp.

#include "SmtcController.h"

#include <QDebug>
#include <QFile>
#include <QGuiApplication>

#if defined(Q_OS_WIN)
#  if !defined(MUSICEFY_ENABLE_WINRT_SRTC)
#    define MUSICEFY_NO_WINRT 1
#  endif
#  ifndef _WIN32_WINNT_WIN10
#    define _WIN32_WINNT_WIN10 0x0A00
#  endif
#endif

// Real-WinRT includes. Only compiled in when MUSICEFY_ENABLE_WINRT_SRTC
// is defined. Note: the C++/WinRT package isn't on disk unless the
// developer opted in via the CMake flag.
#if !defined(MUSICEFY_NO_WINRT) && defined(Q_OS_WIN)
#  include <winrt/Windows.Foundation.h>
#  include <winrt/Windows.Media.h>
#  include <winrt/Windows.Storage.h>
#  include <winrt/Windows.Storage.Streams.h>
#  include <winrt/Windows.System.h>
#  include <winrt/base.h>
#endif

namespace mf::core::playback {

namespace {
// ── Helpers shared by stub + real paths ─────────────────────────────────
inline QString qstrOrEmpty(const QString& s) { return s; }
} // namespace

// ── Impl ───────────────────────────────────────────────────────────────
struct SmtcController::Impl {
    QString lastTitle;
    QString lastArtist;
    QString lastAlbum;

#if !defined(MUSICEFY_NO_WINRT) && defined(Q_OS_WIN)
    // WinRT handles (only present in the real-SMTC build). Held in
    // the Impl so the .h stays pure.
    winrt::Windows::Media::SystemMediaTransportControls smtc{ nullptr };
    winrt::event_token buttonPressedToken{};
    bool buttonPressedHooked = false;
#endif
};

// ── Static helpers ─────────────────────────────────────────────────────
bool SmtcController::isSupported() {
#if defined(MUSICEFY_NO_WINRT)
    return false;
#elif defined(Q_OS_WIN) && (WINVER >= _WIN32_WINNT_WIN10)
    // Real WinRT path is compiled in. Runtime support also requires
    // that winrt::init_apartment() was called in main() — we can't
    // detect that from here, so we just report "compiled in" and let
    // the integration test on Win 10+ do the real validation.
    return true;
#else
    return false;
#endif
}

#if !defined(MUSICEFY_NO_WINRT) && defined(Q_OS_WIN)
// Map a WinRT SMTC button to our internal SmtcCommand enum.
static SmtcCommand mapButton(
    winrt::Windows::Media::SystemMediaTransportControlsButton btn) {
    using W = winrt::Windows::Media::SystemMediaTransportControlsButton;
    switch (btn) {
        case W::Play:  return SmtcCommand::Play;
        case W::Pause: return SmtcCommand::Pause;
        case W::Next:  return SmtcCommand::Next;
        case W::Previous: return SmtcCommand::Previous;
        case W::Stop:  return SmtcCommand::Stop;
        default:       return SmtcCommand::PlayPauseToggle;
    }
}
#endif

// ── Lifecycle ──────────────────────────────────────────────────────────
SmtcController::SmtcController(QObject* parent)
    : QObject(parent)
    , impl_(new Impl)
{
#if defined(MUSICEFY_NO_WINRT) || !defined(Q_OS_WIN)
    qDebug() << "[SMTC] stub mode (real SMTC disabled)";
#elif defined(Q_OS_WIN) && (WINVER >= _WIN32_WINNT_WIN10)
    // Real WinRT path: get the singleton for this process and wire
    // its ButtonPressed event. Note: the app must have been launched
    // with winrt::init_apartment() in main() for this to succeed.
    try {
        impl_->smtc =
            winrt::Windows::Media::SystemMediaTransportControls::GetForCurrentView();
        // Enable the buttons we care about. By default all are enabled,
        // but be explicit.
        impl_->smtc.IsPlayEnabled(true);
        impl_->smtc.IsPauseEnabled(true);
        impl_->smtc.IsNextEnabled(true);
        impl_->smtc.IsPreviousEnabled(true);
        impl_->smtc.IsStopEnabled(true);

        impl_->buttonPressedToken = impl_->smtc.ButtonPressed(
            [this](winrt::Windows::Media::SystemMediaTransportControls const&,
                   winrt::Windows::Media::SystemMediaTransportControlsButtonPressedEventArgs const& args) {
                const SmtcCommand cmd = mapButton(args.Button());
                emit commandReceivedQ(static_cast<int>(cmd));
                if (onCommand_) onCommand_(cmd);
            });
        impl_->buttonPressedHooked = true;

        // Start with a Stopped state so the OS doesn't display stale
        // metadata from a previous run of the same AUMID.
        impl_->smtc.PlaybackStatus(
            winrt::Windows::Media::MediaPlaybackStatus::Stopped);

        qDebug() << "[SMTC] real WinRT path initialised";
    } catch (const winrt::hresult_error& e) {
        qWarning() << "[SMTC] WinRT init failed:" << e.message().c_str()
                   << "(falling back to stub behaviour)";
        impl_->smtc = nullptr;
    }
#endif
}

SmtcController::~SmtcController() {
#if !defined(MUSICEFY_NO_WINRT) && defined(Q_OS_WIN) && (WINVER >= _WIN32_WINNT_WIN10)
    if (impl_->buttonPressedHooked && impl_->smtc) {
        impl_->smtc.ButtonPressed(impl_->buttonPressedToken);
        impl_->buttonPressedHooked = false;
    }
#endif
    delete impl_;
}

// ── Metadata ───────────────────────────────────────────────────────────
void SmtcController::updateMetadata(const mf::core::models::MusicFile& track) {
    impl_->lastTitle  = track.title();
    impl_->lastArtist = track.artist();
    impl_->lastAlbum  = track.album();

    qDebug() << "[SMTC] updateMetadata:" << track.title()
             << "by" << track.artist()
             << "(" << track.album() << ")";

#if !defined(MUSICEFY_NO_WINRT) && defined(Q_OS_WIN) && (WINVER >= _WIN32_WINNT_WIN10)
    if (!impl_->smtc) return;
    try {
        auto updater  = impl_->smtc.DisplayUpdater();
        auto props    = updater.MusicProperties();
        props.Title(  winrt::hstring(track.title().toStdWString()));
        props.Artist( winrt::hstring(track.artist().toStdWString()));
        props.AlbumTitle(winrt::hstring(track.album().toStdWString()));

        // Thumbnail: best-effort, only for local files. The WinRT
        // API needs a StorageFile or a RandomAccessStreamReference;
        // building the latter from raw bytes requires an in-memory
        // RandomAccessStream which is more involved. We just attempt
        // the file path; the OS will fall back to its default
        // thumbnail on failure.
        const QString coverPath = track.coverPath();
        if (!coverPath.isEmpty() && QFile::exists(coverPath)) {
            try {
                auto file = winrt::Windows::Storage::StorageFile::GetFileFromPathAsync(
                    winrt::hstring(coverPath.toStdWString())).get();
                updater.Thumbnail(
                    winrt::Windows::Storage::Streams::RandomAccessStreamReference::CreateFromFile(file));
            } catch (...) {
                // Thumbnail is non-fatal; leave whatever the OS has.
            }
        }
        updater.Update();
    } catch (const winrt::hresult_error& e) {
        qWarning() << "[SMTC] updateMetadata failed:" << e.message().c_str();
    }
#endif
}

void SmtcController::clearMetadata() {
    impl_->lastTitle.clear();
    impl_->lastArtist.clear();
    impl_->lastAlbum.clear();
    qDebug() << "[SMTC] clearMetadata";

#if !defined(MUSICEFY_NO_WINRT) && defined(Q_OS_WIN) && (WINVER >= _WIN32_WINNT_WIN10)
    if (!impl_->smtc) return;
    try {
        impl_->smtc.DisplayUpdater().ClearAll();
    } catch (const winrt::hresult_error& e) {
        qWarning() << "[SMTC] clearMetadata failed:" << e.message().c_str();
    }
#endif
}

// ── Status ─────────────────────────────────────────────────────────────
void SmtcController::updatePlaybackStatus(bool isPlaying, bool isPaused, bool isBuffering) {
    Q_UNUSED(isBuffering);
    qDebug() << "[SMTC] updatePlaybackStatus playing=" << isPlaying
             << "paused=" << isPaused;

#if !defined(MUSICEFY_NO_WINRT) && defined(Q_OS_WIN) && (WINVER >= _WIN32_WINNT_WIN10)
    if (!impl_->smtc) return;
    using M = winrt::Windows::Media::MediaPlaybackStatus;
    M s = M::Stopped;
    if      (isPlaying)  s = M::Playing;
    else if (isPaused)   s = M::Paused;
    try {
        impl_->smtc.PlaybackStatus(s);
    } catch (const winrt::hresult_error& e) {
        qWarning() << "[SMTC] updatePlaybackStatus failed:" << e.message().c_str();
    }
#endif
}

// ── Timeline ───────────────────────────────────────────────────────────
void SmtcController::updateTimeline(qint64 positionMs, qint64 durationMs) {
    qDebug() << "[SMTC] updateTimeline pos=" << positionMs
             << "dur=" << durationMs;

#if !defined(MUSICEFY_NO_WINRT) && defined(Q_OS_WIN) && (WINVER >= _WIN32_WINNT_WIN10)
    if (!impl_->smtc) return;
    try {
        auto t   = impl_->smtc.TimelineProperties();
        t.StartTime(winrt::Windows::Foundation::TimeSpan{ 0 });
        t.Position(winrt::Windows::Foundation::TimeSpan{ positionMs * 10000LL });
        t.EndTime( winrt::Windows::Foundation::TimeSpan{ durationMs * 10000LL });
        impl_->smtc.UpdateTimelineProperties(t);
    } catch (const winrt::hresult_error& e) {
        qWarning() << "[SMTC] updateTimeline failed:" << e.message().c_str();
    }
#endif
}

} // namespace mf::core::playback

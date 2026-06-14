// AppLifecycle.cpp
// See header for design notes.

#include "AppLifecycle.h"

#include "../core/playback/MediaKeyFilter.h"
#include "../core/playback/PlaybackService.h"
#include "../core/playback/QueueManager.h"
#include "../core/playback/SmtcController.h"
#include "../core/services/HealthCheckService.h"
#include "../core/services/LibraryService.h"
#include "../core/services/ToastService.h"
#include "../core/sources/StreamingSourceManager.h"
#include "../core/theme/ThemeManager.h"

#include <QDebug>
#include <QTimer>

namespace mf::app {

using mf::core::playback::MediaKeyFilter;
using mf::core::playback::MediaKey;
using mf::core::playback::PlaybackService;
using mf::core::playback::QueueManager;
using mf::core::playback::SmtcController;
using mf::core::playback::SmtcCommand;
using mf::core::services::HealthCheckService;
using mf::core::services::LibraryService;
using mf::core::services::ToastService;
using mf::core::sources::StreamingSourceManager;

AppLifecycle::AppLifecycle(AppContainer& container, QObject* parent)
    : QObject(parent)
    , container_(container)
{
}

AppLifecycle::~AppLifecycle() {
    shutdown();
}

void AppLifecycle::start() {
    if (started_) return;

    qInfo() << "AppLifecycle::start() — resolving services...";
    auto playback = container_.playback();
    auto queue    = container_.queue();
    auto smtc     = container_.smtc();
    auto health   = container_.health();
    auto mgr      = container_.sourceManager();
    auto theme    = container_.theme();
    auto toasts   = container_.toasts();
    auto libSvc   = container_.libraryService();
    Q_UNUSED(mgr);
    Q_UNUSED(theme);

    // ── Null-service diagnostics ───────────────────────────────────
    // If any core service is null, log it (to error.log) and bail
    // instead of segfaulting on a connect() call.
    if (!playback) { qCritical() << "AppLifecycle::start() FATAL: playback is null"; return; }
    if (!queue)    { qCritical() << "AppLifecycle::start() FATAL: queue is null";    return; }
    if (!smtc)     { qCritical() << "AppLifecycle::start() FATAL: smtc is null";     return; }

    qInfo() << "AppLifecycle::start() — wiring queue → playback";
    // Wire queue → playback: when the current track changes, load it
    // into the player.
    QObject::connect(queue.get(), &QueueManager::indexChangedQ,
                     playback.get(), [playback, queue](int) {
        auto track = queue->currentTrack();
        if (!track.filePath().isEmpty() || !track.sourceUri().isEmpty()) {
            playback->setTrack(track);
        }
    });

    qInfo() << "AppLifecycle::start() — wiring queue → SMTC";
    // Wire queue → SMTC: title/artist/album updates ride along.
    QObject::connect(queue.get(), &QueueManager::indexChangedQ,
                     this, [smtc, queue](int) {
        smtc->updateMetadata(queue->currentTrack());
    });

    qInfo() << "AppLifecycle::start() — wiring playback → SMTC";
    // Wire playback state → SMTC.
    QObject::connect(playback.get(), &PlaybackService::stateChangedQ,
                     this, [smtc](int state) {
        smtc->updatePlaybackStatus(state == int(PlaybackService::PlaybackState::Playing),
                                   state == int(PlaybackService::PlaybackState::Paused),
                                   false);
    });

    qInfo() << "AppLifecycle::start() — wiring playback position → SMTC";
    // Wire playback position → SMTC. Throttled to 1 Hz because the OS
    // only refreshes the lock-screen progress bar that often anyway.
    if (smtc) {
        auto* timelineTimer = new QTimer(this);
        timelineTimer->setInterval(1000);
        auto lastPos = std::make_shared<qint64>(-1);
        auto lastDur = std::make_shared<qint64>(-1);
        QObject::connect(timelineTimer, &QTimer::timeout, this,
            [smtc, playback, lastPos, lastDur]() {
                const qint64 pos = playback->position().count();
                const qint64 dur = playback->duration().count();
                if (pos == *lastPos && dur == *lastDur) return;
                *lastPos = pos;
                *lastDur = dur;
                smtc->updateTimeline(pos, dur);
            });
        timelineTimer->start();
    }

    qInfo() << "AppLifecycle::start() — wiring SMTC commands";
    // OS-originated SMTC commands → playback/queue. On Windows 8+ the
    // SMTC surface (lock screen, volume flyout, Cortana, media keys) is
    // the canonical entry point for media commands while the app is in
    // the background. On Windows 7 SMTC is a no-op and these never fire
    // — hardware keys are still picked up by MediaKeyFilter.
    wireSmtc();

    qInfo() << "AppLifecycle::start() — restoring persisted queue";
    // Restore persisted queue from last session.
    queue->restore();

    qInfo() << "AppLifecycle::start() — wiring media keys";
    // Wire media keys to playback actions.
    wireMediaKeys();

    qInfo() << "AppLifecycle::start() — starting health check";
    // Start the periodic health check loop.
    if (health) {
        health->start();
    }

    qInfo() << "AppLifecycle::start() — wiring library → toast notifications";
    // ── Library scan → toast notifications ───────────────────────────
    // The user sees a live "Scanning…" toast while a scan runs, then
    // a success/warning toast when it finishes.
    if (libSvc && toasts) {
        QObject::connect(libSvc.get(), &LibraryService::scanStarted,
                         toasts.get(), [toasts](const QStringList& folders) {
            const int n = folders.size();
            toasts->showInfo(
                QStringLiteral("Scanning library"),
                n == 1
                    ? QStringLiteral("Indexing 1 folder…")
                    : QStringLiteral("Indexing %1 folders…").arg(n));
        });
        QObject::connect(libSvc.get(), &LibraryService::scanFinished,
                         toasts.get(), [toasts](int added, int updated) {
            toasts->showSuccess(
                QStringLiteral("Library scan complete"),
                QStringLiteral("Added %1, updated %2").arg(added).arg(updated));
        });
        QObject::connect(libSvc.get(), &LibraryService::scanCancelled,
                         toasts.get(), [toasts]() {
            toasts->showWarning(
                QStringLiteral("Library scan cancelled"),
                QStringLiteral("Some tracks may not have been indexed yet."));
        });
    }

    qInfo() << "AppLifecycle::start() — all services wired OK";
    started_ = true;
}

void AppLifecycle::shutdown() {
    if (!started_) return;

    // Persist queue before shutdown.
    auto queue = container_.queue();
    if (queue) {
        queue->persist();
    }

    auto health = container_.health();
    if (health) {
        health->stop();
    }

    auto playback = container_.playback();
    if (playback) {
        playback->stop();
    }

    started_ = false;
}

void AppLifecycle::wireMediaKeys() {
    auto filter   = container_.mediaKeys();
    auto playback = container_.playback();
    auto queue    = container_.queue();
    if (!filter || !playback || !queue) return;

    QObject::connect(filter.get(), &MediaKeyFilter::mediaKeyPressed,
                     this, [playback, queue](int key) {
        switch (static_cast<MediaKey>(key)) {
            case MediaKey::PlayPause: playback->togglePlayPause(); break;
            case MediaKey::Next:      queue->next();                break;
            case MediaKey::Previous:  queue->previous();            break;
            case MediaKey::Stop:      playback->stop();              break;
            default: break; // Volume keys: defer to OS volume mixer.
        }
    });
}

void AppLifecycle::wireQueue() {
    // (currently empty — placeholder for queue <-> view model wiring
    // that will be added when the UI layer is brought up)
}

void AppLifecycle::wireSmtc() {
    auto smtc     = container_.smtc();
    auto playback = container_.playback();
    auto queue    = container_.queue();
    if (!smtc || !playback || !queue) return;

    // Bind the C++-style callback (used by the WinRT code path) to the
    // same routing as the Qt signal. Even on the stub build this gives
    // us a single dispatch point and lets tests fire commands without
    // standing up the WinRT runtime.
    smtc->setOnCommand([playback, queue](SmtcCommand cmd) {
        switch (cmd) {
            case SmtcCommand::Play:
                if (playback->state() != PlaybackService::PlaybackState::Playing)
                    playback->play();
                break;
            case SmtcCommand::Pause:
                if (playback->state() != PlaybackService::PlaybackState::Paused)
                    playback->pause();
                break;
            case SmtcCommand::PlayPauseToggle:
                playback->togglePlayPause();
                break;
            case SmtcCommand::Next:     queue->next();     break;
            case SmtcCommand::Previous: queue->previous(); break;
            case SmtcCommand::Stop:     playback->stop();  break;
        }
    });

    QObject::connect(smtc.get(), &SmtcController::commandReceivedQ,
                     this, [playback, queue](int cmd) {
        switch (static_cast<SmtcCommand>(cmd)) {
            case SmtcCommand::Play:
                if (playback->state() != PlaybackService::PlaybackState::Playing)
                    playback->play();
                break;
            case SmtcCommand::Pause:
                if (playback->state() != PlaybackService::PlaybackState::Paused)
                    playback->pause();
                break;
            case SmtcCommand::PlayPauseToggle:
                playback->togglePlayPause();
                break;
            case SmtcCommand::Next:     queue->next();     break;
            case SmtcCommand::Previous: queue->previous(); break;
            case SmtcCommand::Stop:     playback->stop();  break;
        }
    });
}

} // namespace mf::app

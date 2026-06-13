// AppLifecycle.h
// Coordinates the lifecycle of long-running services: starts the
// health-check loop on startup, hooks media-key events to the
// playback service, and stops everything cleanly on shutdown.

#pragma once

#include "AppContainer.h"

#include <QObject>

namespace mf::app {

class AppLifecycle : public QObject {
    Q_OBJECT
public:
    explicit AppLifecycle(AppContainer& container, QObject* parent = nullptr);
    ~AppLifecycle() override;

    /// Start the application. After this returns, the health check
    /// loop is running, media keys are wired, and the playback
    /// service is ready to receive tracks.
    void start();

    /// Stop everything cleanly. Called from main() before returning.
    void shutdown();

private:
    void wireMediaKeys();
    void wireQueue();
    void wireSmtc();

    AppContainer& container_;
    bool          started_ = false;
};

} // namespace mf::app

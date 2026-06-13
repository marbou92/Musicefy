// HealthCheckService.h
// Periodically pings every configured streaming source and tracks the
// health state (Healthy / Degraded / Unhealthy / PermanentlyUnhealthy)
// with exponential backoff. The check interval is source-type aware
// (local sources are checked less aggressively than remote ones).
//
// The service is started with start() and stopped with stop(). A
// single-threaded QTimer drives the check loop. Failed checks don't
// run again until the source's currentRetryDelay elapses, which is
// computed by SourceHealthState::getNextRetryDelay() (30s, 60s, 120s,
// 300s, 600s).

#pragma once

#include "../interfaces/IHealthCheckService.h"
#include "../interfaces/IStreamingSourceManager.h"
#include "../models/SourceHealthState.h"

#include <QHash>
#include <QObject>
#include <QString>
#include <QTimer>

#include <memory>

namespace mf::core::services {

class HealthCheckService : public QObject,
                           public mf::core::interfaces::IHealthCheckService {
    Q_OBJECT
public:
    explicit HealthCheckService(QObject* parent = nullptr);
    HealthCheckService(mf::core::interfaces::IStreamingSourceManager* sources,
                       QObject* parent = nullptr);
    ~HealthCheckService() override;

    void start() override;
    void stop() override;
    bool isRunning() const override { return timer_.isActive(); }

    void checkSource(QString sourceId, StateCallback onDone) override;
    void checkAll(MapCallback onDone) override;
    mf::core::models::SourceHealthState stateFor(QString sourceId) const override;
    QHash<QString, mf::core::models::SourceHealthState> allStates() const override;

    void setOnStateChanged(StateCallback cb) override { onStateChanged_ = std::move(cb); }

    void setCheckInterval(int ms) { checkIntervalMs_ = ms; }
    int  checkInterval() const { return checkIntervalMs_; }

signals:
    void stateChangedQ(QString sourceId, int status);

private:
    void runCheckTick();
    void scheduleNextCheck(const QString& sourceId, std::chrono::seconds delay);

    mf::core::interfaces::IStreamingSourceManager* sources_ = nullptr;
    QTimer                                          timer_;
    int                                             checkIntervalMs_ = 60'000; // 1 min
    QHash<QString, mf::core::models::SourceHealthState> states_;
    StateCallback                                   onStateChanged_;
};

} // namespace mf::core::services

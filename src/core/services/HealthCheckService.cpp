// HealthCheckService.cpp
// See header for design notes.

#include "HealthCheckService.h"

#include "../interfaces/IMusicSourceSession.h"
#include "../models/StreamingSource.h"

namespace mf::core::services {

using mf::core::interfaces::IHealthCheckService;
using mf::core::interfaces::IMusicSourceSession;
using mf::core::interfaces::IStreamingSourceManager;
using mf::core::models::SourceHealthState;
using mf::core::models::SourceHealthStatus;
using mf::core::models::StreamingSource;

HealthCheckService::HealthCheckService(QObject* parent)
    : QObject(parent)
{
}

HealthCheckService::HealthCheckService(IStreamingSourceManager* sources, QObject* parent)
    : QObject(parent)
    , sources_(sources)
{
    connect(&timer_, &QTimer::timeout, this, &HealthCheckService::runCheckTick);
}

HealthCheckService::~HealthCheckService() {
    stop();
}

void HealthCheckService::start() {
    if (timer_.isActive()) return;
    timer_.start(checkIntervalMs_);
    // Run an initial sweep immediately.
    QMetaObject::invokeMethod(this, &HealthCheckService::runCheckTick, Qt::QueuedConnection);
}

void HealthCheckService::stop() {
    timer_.stop();
}

void HealthCheckService::checkSource(QString sourceId, StateCallback onDone) {
    if (!sources_) {
        if (onDone) onDone(SourceHealthState{});
        return;
    }
    auto session = sources_->createSession(sourceId);
    if (!session) {
        SourceHealthState s;
        s.setSourceId(sourceId);
        s.setStatus(SourceHealthStatus::PermanentlyUnhealthy);
        s.setLastErrorMessage(QStringLiteral("No provider for source"));
        states_.insert(sourceId, s);
        if (onStateChanged_) onStateChanged_(s);
        emit stateChangedQ(sourceId, int(s.status()));
        if (onDone) onDone(s);
        return;
    }
    session->ping([this, sourceId, onDone](bool ok, QString err) {
        SourceHealthState s;
        s.setSourceId(sourceId);
        s.setLastHealthCheck(QDateTime::currentDateTime());
        if (ok) {
            s.recordSuccess();
        } else {
            s.recordFailure(err);
        }
        states_.insert(sourceId, s);
        if (onStateChanged_) onStateChanged_(s);
        emit stateChangedQ(sourceId, int(s.status()));
        if (onDone) onDone(s);
    });
}

void HealthCheckService::checkAll(MapCallback onDone) {
    if (!sources_) {
        if (onDone) onDone({});
        return;
    }
    QList<StreamingSource> all = sources_->allSources();
    if (all.isEmpty()) {
        if (onDone) onDone({});
        return;
    }
    int remaining = all.size();
    QHash<QString, SourceHealthState> results;
    for (const StreamingSource& src : all) {
        checkSource(src.id(), [&, onDone](SourceHealthState s) {
            results.insert(s.sourceId(), s);
            if (--remaining == 0) {
                if (onDone) onDone(results);
            }
        });
    }
}

SourceHealthState HealthCheckService::stateFor(QString sourceId) const {
    return states_.value(sourceId);
}

QHash<QString, SourceHealthState> HealthCheckService::allStates() const {
    return states_;
}

void HealthCheckService::runCheckTick() {
    if (!sources_) return;
    QList<StreamingSource> all = sources_->allSources();
    QDateTime now = QDateTime::currentDateTime();
    for (const StreamingSource& src : all) {
        const SourceHealthState& prev = states_.value(src.id());
        if (prev.status() == SourceHealthStatus::PermanentlyUnhealthy) {
            continue;
        }
        // Honour the backoff: if the last check happened less than
        // currentRetryDelay ago, skip this source.
        if (prev.lastHealthCheck().has_value() && prev.lastHealthCheck().value().isValid()) {
            qint64 elapsed = prev.lastHealthCheck().value().secsTo(now);
            if (elapsed >= 0 && elapsed < prev.currentRetryDelay().count()) {
                continue;
            }
        }
        checkSource(src.id(), nullptr);
    }
}

} // namespace mf::core::services
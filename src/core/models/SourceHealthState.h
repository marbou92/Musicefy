// SourceHealthState.h
// Tracks health of a streaming source with exponential backoff.
// Port of Musicefy.Core.Models.SourceHealthState.

#pragma once

#include <QDateTime>
#include <QString>
#include <chrono>
#include <optional>

namespace mf::core::models {

enum class SourceHealthStatus {
    Healthy,
    Degraded,
    Unhealthy,
    PermanentlyUnhealthy,
};

class SourceHealthState {
public:
    SourceHealthState() = default;

    QString sourceId() const { return sourceId_; }
    void setSourceId(QString v) { sourceId_ = std::move(v); }

    SourceHealthStatus status() const { return status_; }
    void setStatus(SourceHealthStatus v) { status_ = v; }

    int consecutiveFailures() const { return consecutiveFailures_; }
    void setConsecutiveFailures(int v) { consecutiveFailures_ = v; }

    std::optional<QDateTime> lastSuccessfulConnection() const { return lastSuccessfulConnection_; }
    void setLastSuccessfulConnection(std::optional<QDateTime> v) { lastSuccessfulConnection_ = v; }

    std::optional<QDateTime> lastHealthCheck() const { return lastHealthCheck_; }
    void setLastHealthCheck(std::optional<QDateTime> v) { lastHealthCheck_ = v; }

    std::chrono::seconds currentRetryDelay() const { return currentRetryDelay_; }
    void setCurrentRetryDelay(std::chrono::seconds v) { currentRetryDelay_ = v; }

    QString lastErrorMessage() const { return lastErrorMessage_; }
    void setLastErrorMessage(QString v) { lastErrorMessage_ = std::move(v); }

    /// Exponential backoff: 30s, 60s, 120s, 300s, 600s
    std::chrono::seconds getNextRetryDelay() const;

    void recordSuccess();
    void recordFailure(QString errorMessage);

private:
    QString sourceId_;
    SourceHealthStatus status_ = SourceHealthStatus::Healthy;
    int consecutiveFailures_ = 0;
    std::optional<QDateTime> lastSuccessfulConnection_;
    std::optional<QDateTime> lastHealthCheck_;
    std::chrono::seconds currentRetryDelay_{30};
    QString lastErrorMessage_;
};

} // namespace mf::core::models

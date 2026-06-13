// SourceHealthState.cpp

#include "SourceHealthState.h"

#include <algorithm>
#include <array>

namespace mf::core::models {

std::chrono::seconds SourceHealthState::getNextRetryDelay() const {
    static const std::array<int, 5> delays = {30, 60, 120, 300, 600};
    const int index = std::min(consecutiveFailures_, static_cast<int>(delays.size()) - 1);
    return std::chrono::seconds{ delays[static_cast<size_t>(index)] };
}

void SourceHealthState::recordSuccess() {
    status_ = SourceHealthStatus::Healthy;
    consecutiveFailures_ = 0;
    lastSuccessfulConnection_ = QDateTime::currentDateTimeUtc();
    currentRetryDelay_ = std::chrono::seconds{30};
    lastErrorMessage_.clear();
}

void SourceHealthState::recordFailure(QString errorMessage) {
    ++consecutiveFailures_;
    lastErrorMessage_ = std::move(errorMessage);
    lastHealthCheck_ = QDateTime::currentDateTimeUtc();

    if (consecutiveFailures_ >= 5) {
        status_ = SourceHealthStatus::PermanentlyUnhealthy;
    } else if (consecutiveFailures_ >= 3) {
        status_ = SourceHealthStatus::Unhealthy;
    } else {
        status_ = SourceHealthStatus::Degraded;
    }

    currentRetryDelay_ = getNextRetryDelay();
}

} // namespace mf::core::models

#pragma once

#include "../models/SourceHealthState.h"

#include <QHash>
#include <QString>

#include <functional>

namespace mf::core::interfaces {

class IHealthCheckService {
public:
    virtual ~IHealthCheckService() = default;

    using StateCallback = std::function<void(mf::core::models::SourceHealthState)>;
    using MapCallback   = std::function<void(QHash<QString, mf::core::models::SourceHealthState>)>;

    virtual void start() = 0;
    virtual void stop() = 0;
    virtual bool isRunning() const = 0;

    virtual void checkSource(QString sourceId, StateCallback onDone) = 0;
    virtual void checkAll(MapCallback onDone) = 0;
    virtual mf::core::models::SourceHealthState stateFor(QString sourceId) const = 0;
    virtual QHash<QString, mf::core::models::SourceHealthState> allStates() const = 0;

    virtual void setOnStateChanged(StateCallback cb) = 0;
};

} // namespace mf::core::interfaces

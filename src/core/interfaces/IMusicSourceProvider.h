#pragma once

#include "../models/SourceConfigField.h"
#include "../models/StreamingSource.h"

#include <QString>

#include <memory>

namespace mf::core::interfaces {

class IMusicSourceSession;

class IMusicSourceProvider {
public:
    virtual ~IMusicSourceProvider() = default;

    virtual QString sourceType() const = 0;
    virtual QString displayName() const = 0;
    virtual QList<mf::core::models::SourceConfigField> configFields() const = 0;

    virtual std::unique_ptr<IMusicSourceSession> createSession(
        const mf::core::models::StreamingSource& source) const = 0;
};

} // namespace mf::core::interfaces

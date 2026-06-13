#pragma once

#include "../models/StreamingSource.h"
#include "IMusicSourceProvider.h"
#include "IMusicSourceSession.h"

#include <QList>
#include <QString>

#include <functional>
#include <memory>

namespace mf::core::interfaces {

class IStreamingSourceManager {
public:
    virtual ~IStreamingSourceManager() = default;

    using SourceListCallback = std::function<void(QList<mf::core::models::StreamingSource>)>;
    using ProviderListCallback = std::function<void(QList<QString /*sourceType*/>)>;

    virtual void registerProvider(std::shared_ptr<IMusicSourceProvider> provider) = 0;
    virtual void unregisterProvider(QString sourceType) = 0;
    virtual QList<QString> registeredSourceTypes() const = 0;
    virtual std::shared_ptr<IMusicSourceProvider> providerFor(QString sourceType) const = 0;

    virtual void addSource(mf::core::models::StreamingSource source) = 0;
    virtual void updateSource(mf::core::models::StreamingSource source) = 0;
    virtual void removeSource(QString sourceId) = 0;
    virtual QList<mf::core::models::StreamingSource> allSources() const = 0;
    virtual mf::core::models::StreamingSource sourceById(QString sourceId) const = 0;

    virtual std::unique_ptr<IMusicSourceSession> createSession(QString sourceId) = 0;
};

} // namespace mf::core::interfaces

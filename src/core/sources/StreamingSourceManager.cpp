// StreamingSourceManager.cpp

#include "StreamingSourceManager.h"

#include <QDebug>
#include <QVariant>

#include "../models/StreamingSource.h"

#include <QHash>
#include <QUuid>

namespace mf::core::sources {

using mf::core::interfaces::IMusicSourceProvider;
using mf::core::interfaces::IMusicSourceSession;
using mf::core::models::StreamingSource;

StreamingSourceManager::StreamingSourceManager(QObject* parent)
    : QObject(parent)
{
}

StreamingSourceManager::~StreamingSourceManager() = default;

void StreamingSourceManager::registerProvider(std::shared_ptr<IMusicSourceProvider> provider) {
    if (!provider) return;
    QString t = provider->sourceType();
    providers_.insert(t, std::move(provider));
    emit providerRegistered(t);
}

void StreamingSourceManager::unregisterProvider(QString sourceType) {
    if (providers_.remove(sourceType) > 0) {
        emit providerUnregistered(sourceType);
    }
}

QList<QString> StreamingSourceManager::registeredSourceTypes() const {
    return providers_.keys();
}

std::shared_ptr<IMusicSourceProvider> StreamingSourceManager::providerFor(QString sourceType) const {
    return providers_.value(sourceType);
}

void StreamingSourceManager::addSource(StreamingSource source) {
    if (source.id().isEmpty()) {
        // Caller should set the id, but defensively generate one.
        source.setId(QUuid::createUuid().toString(QUuid::WithoutBraces));
    }
    sources_.insert(source.id(), source);
    emit sourceAdded(source.id(), source.type());
}

void StreamingSourceManager::updateSource(StreamingSource source) {
    if (source.id().isEmpty()) {
        return;
    }
    sources_.insert(source.id(), source);
    emit sourceUpdated(source.id());
}

void StreamingSourceManager::removeSource(QString sourceId) {
    if (sources_.remove(sourceId) > 0) {
        emit sourceRemoved(sourceId);
    }
}

QList<StreamingSource> StreamingSourceManager::allSources() const {
    return sources_.values();
}

StreamingSource StreamingSourceManager::sourceById(QString sourceId) const {
    return sources_.value(sourceId);
}

std::unique_ptr<IMusicSourceSession> StreamingSourceManager::createSession(QString sourceId) {
    StreamingSource src = sources_.value(sourceId);
    if (src.id().isEmpty()) {
        return nullptr;
    }
    auto provider = providers_.value(src.type());
    if (!provider) {
        return nullptr;
    }
    return provider->createSession(src);
}

} // namespace mf::core::sources
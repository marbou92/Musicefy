// StreamingSourceManager.h
// Registry of installed music source providers (Subsonic, YouTube, local
// folder, …) and configured StreamingSource instances. Persists sources
// via LibraryRepository::upsertSource / deleteBySourceId (when wired).

#pragma once

#include "../interfaces/IStreamingSourceManager.h"

#include <QObject>
#include <QHash>
#include <QList>
#include <QString>

#include <memory>

namespace mf::core::sources {

class StreamingSourceManager : public QObject,
                               public mf::core::interfaces::IStreamingSourceManager {
    Q_OBJECT
public:
    explicit StreamingSourceManager(QObject* parent = nullptr);
    ~StreamingSourceManager() override;

    // IStreamingSourceManager ───────────────────────────────────────────
    void registerProvider(std::shared_ptr<mf::core::interfaces::IMusicSourceProvider> provider) override;
    void unregisterProvider(QString sourceType) override;
    QList<QString> registeredSourceTypes() const override;
    std::shared_ptr<mf::core::interfaces::IMusicSourceProvider> providerFor(QString sourceType) const override;

    void addSource(mf::core::models::StreamingSource source) override;
    void updateSource(mf::core::models::StreamingSource source) override;
    void removeSource(QString sourceId) override;
    QList<mf::core::models::StreamingSource> allSources() const override;
    mf::core::models::StreamingSource sourceById(QString sourceId) const override;

    std::unique_ptr<mf::core::interfaces::IMusicSourceSession> createSession(QString sourceId) override;

signals:
    void providerRegistered(QString sourceType);
    void providerUnregistered(QString sourceType);
    void sourceAdded(QString sourceId, QString sourceType);
    void sourceUpdated(QString sourceId);
    void sourceRemoved(QString sourceId);

private:
    QHash<QString, std::shared_ptr<mf::core::interfaces::IMusicSourceProvider>> providers_;
    QHash<QString, mf::core::models::StreamingSource>                            sources_;
};

} // namespace mf::core::sources
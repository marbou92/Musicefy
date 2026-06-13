// ExtensionRepoService.h
// Fetches and caches extension manifests from remote repository URLs.
// Works with RepositoriesSettingsPanel which stores URLs in QSettings
// under "extensions/repos". This service provides the backend:
//   1. Fetches <repo-url>/manifest.json from each registered URL
//   2. Parses into ExtensionManifest objects
//   3. Caches in memory, exposed as Q_PROPERTY
//   4. Supports auto-refresh via QTimer
//   5. Provides merge/dedup across multiple repos
//
// Qt5 / MSVC v142 compatible, /WX clean.

#pragma once

#include <QObject>
#include <QTimer>
#include <QList>
#include <QHash>
#include <QStringList>
#include "../models/ExtensionManifest.h"

namespace mf::core::sources { class HttpClient; }
namespace mf::core::services { class SettingsControl; }

namespace mf::core::services {

class ExtensionRepoService : public QObject {
    Q_OBJECT
    Q_PROPERTY(QList<mf::core::models::ExtensionManifest> manifests READ manifests NOTIFY manifestsChanged)
    Q_PROPERTY(QStringList repoUrls READ repoUrls NOTIFY repoUrlsChanged)
    Q_PROPERTY(bool refreshing READ isRefreshing NOTIFY refreshingChanged)
    Q_PROPERTY(int manifestCount READ manifestCount NOTIFY manifestsChanged)
public:
    explicit ExtensionRepoService(mf::core::sources::HttpClient* http,
                                  SettingsControl* settings,
                                  QObject* parent = nullptr);
    ~ExtensionRepoService() override;

    QList<mf::core::models::ExtensionManifest> manifests() const { return manifests_; }
    QStringList repoUrls() const { return repoUrls_; }
    bool isRefreshing() const { return refreshing_; }
    int manifestCount() const { return manifests_.size(); }

    /// Get all manifests that provide the given sourceType.
    Q_INVOKABLE QList<mf::core::models::ExtensionManifest> manifestsForSourceType(const QString& sourceType) const;

    /// Get a manifest by its extension ID (returns empty manifest if not found).
    Q_INVOKABLE mf::core::models::ExtensionManifest manifestById(const QString& id) const;

public slots:
    /// Add a repo URL and persist it. No-op if already present or invalid.
    void addRepo(const QString& url);

    /// Remove a repo URL and persist the change.
    void removeRepo(const QString& url);

    /// Trigger an immediate refresh of all repo URLs.
    void refresh();

    /// Start the auto-refresh timer based on stored interval.
    void startAutoRefresh();

    /// Stop the auto-refresh timer.
    void stopAutoRefresh();

signals:
    void manifestsChanged();
    void repoUrlsChanged();
    void refreshingChanged();
    void refreshFinished(int totalManifests, int newManifests);
    void refreshError(const QString& repoUrl, const QString& error);

private slots:
    void onRepoFetched(const QString& repoUrl, const QByteArray& data);
    void onRepoFetchError(const QString& repoUrl, const QString& error);
    void onAutoRefreshTimer();

private:
    void loadRepoUrls();
    void saveRepoUrls();
    void mergeManifests(const QString& repoUrl, const QList<mf::core::models::ExtensionManifest>& parsed);
    static bool isValidUrl(const QString& url);
    static QList<mf::core::models::ExtensionManifest> parseManifestJson(const QByteArray& json);

    mf::core::sources::HttpClient* http_     = nullptr;
    SettingsControl* settings_ = nullptr;
    QTimer          autoRefreshTimer_;

    QStringList repoUrls_;
    QList<mf::core::models::ExtensionManifest> manifests_;
    QHash<QString, QList<mf::core::models::ExtensionManifest>> repoManifests_;
    int pendingFetches_ = 0;
    bool refreshing_ = false;
};

} // namespace mf::core::services

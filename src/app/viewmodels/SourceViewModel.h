// SourceViewModel.h
// Per-source wrapper with health/connection/expand state. Provides
// a unified UI surface for SourcesSettingsPanel to display source
// status with color-coded health indicators.

#pragma once

#include "../../core/models/StreamingSource.h"
#include "../../core/models/SourceHealthState.h"

#include <QObject>
#include <QString>
#include <QTimer>

namespace mf::core::interfaces { class IStreamingSourceManager; }

namespace mf::app::viewmodels {

class SourceViewModel : public QObject {
    Q_OBJECT
    using SourceHealthStatus = mf::core::models::SourceHealthStatus;
    Q_PROPERTY(QString  id               READ id               CONSTANT)
    Q_PROPERTY(QString  name             READ name             NOTIFY sourceChanged)
    Q_PROPERTY(QString  type             READ type             NOTIFY sourceChanged)
    Q_PROPERTY(QString  url              READ url              NOTIFY sourceChanged)
    Q_PROPERTY(bool     isConnected      READ isConnected      NOTIFY healthChanged)
    Q_PROPERTY(bool     isExpanded       READ isExpanded       WRITE setExpanded NOTIFY expandedChanged)
    Q_PROPERTY(int      healthState      READ healthState      NOTIFY healthChanged)
    Q_PROPERTY(QString  healthStatusText READ healthStatusText NOTIFY healthChanged)
    Q_PROPERTY(QString  errorMessage     READ errorMessage     NOTIFY healthChanged)
    Q_PROPERTY(QString  lastHealthCheck  READ lastHealthCheck  NOTIFY healthChanged)

public:
    SourceViewModel(mf::core::interfaces::IStreamingSourceManager* sourceMgr,
                    const mf::core::models::StreamingSource& source,
                    QObject* parent = nullptr);
    ~SourceViewModel() override = default;

    QString id()               const { return source_.id(); }
    QString name()             const { return source_.name(); }
    QString type()             const { return source_.type(); }
    QString url()              const { return source_.url(); }
    bool    isConnected()      const { return source_.isConnected(); }
    bool    isExpanded()       const { return expanded_; }
    int     healthState()      const { return static_cast<int>(healthState_); }
    QString healthStatusText() const { return healthStatusText_; }
    QString errorMessage()     const { return errorMessage_; }
    QString lastHealthCheck()  const { return lastHealthCheck_; }

    void setExpanded(bool v);

    /// Refresh the health state by pinging the source.
    Q_INVOKABLE void checkHealth();

signals:
    void sourceChanged();
    void healthChanged();
    void expandedChanged();

private slots:
    void onHealthCheckResult(bool ok, const QString& error);

private:
    void updateHealthDisplay();

    mf::core::interfaces::IStreamingSourceManager* sourceMgr_ = nullptr;
    mf::core::models::StreamingSource source_;
    bool expanded_ = false;
    SourceHealthStatus healthState_ = static_cast<SourceHealthStatus>(0);
    QString healthStatusText_;
    QString errorMessage_;
    QString lastHealthCheck_;
    QTimer* healthCheckTimer_ = nullptr;
};

} // namespace mf::app::viewmodels

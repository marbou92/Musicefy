// SourceViewModel.cpp

#include "SourceViewModel.h"

#include "../../core/interfaces/IStreamingSourceManager.h"
#include "../../core/interfaces/IMusicSourceSession.h"

#include <QDateTime>

namespace mf::app::viewmodels {

using mf::core::interfaces::IStreamingSourceManager;
using mf::core::interfaces::IMusicSourceSession;
using mf::core::models::SourceHealthStatus;
using mf::core::models::StreamingSource;

SourceViewModel::SourceViewModel(IStreamingSourceManager* sourceMgr,
                                 const StreamingSource& source,
                                 QObject* parent)
    : QObject(parent)
    , sourceMgr_(sourceMgr)
    , source_(source)
{
    updateHealthDisplay();

    // Periodic health check every 5 minutes
    healthCheckTimer_ = new QTimer(this);
    healthCheckTimer_->setInterval(5 * 60 * 1000);
    connect(healthCheckTimer_, &QTimer::timeout,
            this, &SourceViewModel::checkHealth);
    healthCheckTimer_->start();
}

// ──────────────────────────────────────────────────────────────────
void SourceViewModel::setExpanded(bool v)
{
    if (expanded_ == v) return;
    expanded_ = v;
    emit expandedChanged();
}

// ──────────────────────────────────────────────────────────────────
void SourceViewModel::checkHealth()
{
    if (!sourceMgr_) return;

    auto session = sourceMgr_->createSession(source_.id());
    if (!session) {
        healthState_ = SourceHealthStatus::Unhealthy;
        healthStatusText_ = QStringLiteral("No session");
        errorMessage_ = QStringLiteral("Could not create session");
        lastHealthCheck_ = QDateTime::currentDateTime().toString(QStringLiteral("hh:mm:ss"));
        emit healthChanged();
        return;
    }

    session->ping([this](bool ok, const QString& error) {
        QMetaObject::invokeMethod(this, [this, ok, error]() {
            onHealthCheckResult(ok, error);
        });
    });
}

// ──────────────────────────────────────────────────────────────────
void SourceViewModel::onHealthCheckResult(bool ok, const QString& error)
{
    if (ok) {
        healthState_ = SourceHealthStatus::Healthy;
        healthStatusText_ = QStringLiteral("Connected");
        errorMessage_.clear();
    } else {
        healthState_ = SourceHealthStatus::Unhealthy;
        healthStatusText_ = QStringLiteral("Error");
        errorMessage_ = error;
    }
    lastHealthCheck_ = QDateTime::currentDateTime().toString(QStringLiteral("hh:mm:ss"));
    emit healthChanged();
}

// ──────────────────────────────────────────────────────────────────
void SourceViewModel::updateHealthDisplay()
{
    if (source_.isConnected()) {
        healthState_ = SourceHealthStatus::Healthy;
        healthStatusText_ = QStringLiteral("Connected");
    } else {
        healthState_ = SourceHealthStatus::Unhealthy;
        healthStatusText_ = QStringLiteral("Not connected");
    }
}

} // namespace mf::app::viewmodels

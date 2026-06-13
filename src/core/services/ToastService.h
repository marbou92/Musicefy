// ToastService.h
// In-app notification manager. The actual visual rendering lives
// in a ToastOverlay QWidget; this object is the logical interface
// that view models and services call to publish a toast.
//
// Lifecycle:
//   1) MainWindow creates a ToastOverlay as a child widget that
//      covers its full rect, and calls bindOverlay().
//   2) Anyone with a reference to the service (resolved from the
//      DI container) calls show()/showError()/showSuccess().
//   3) The service emits a signal that the overlay consumes to
//      create a toast card, position it bottom-right, animate it
//      in, and remove it after the duration.

#pragma once

#include <QObject>
#include <QString>

class QWidget;

namespace mf::core::services {

class ToastService : public QObject {
    Q_OBJECT
public:
    enum class Level { Info, Success, Warning, Error };
    Q_ENUM(Level)

    explicit ToastService(QObject* parent = nullptr);
    ~ToastService() override = default;

    /// Called by the UI layer to attach the overlay widget. The
    /// overlay is a sibling of the page area, not a child of this
    /// service, so destruction ordering is clean.
    void bindOverlay(QWidget* overlay);

public slots:
    void show(const QString& title,
              const QString& message,
              int             durationMs = 3000);
    void showInfo(const QString& title, const QString& message);
    void showSuccess(const QString& title, const QString& message);
    void showWarning(const QString& title, const QString& message);
    void showError(const QString& title, const QString& message);

signals:
    /// Emitted when a new toast is requested. The bound overlay
    /// creates the visual card and self-manages its lifetime.
    void toastRequested(QString title,
                        QString message,
                        int     level,
                        int     durationMs);

private:
    QWidget* overlay_ = nullptr;
};

} // namespace mf::core::services

// ToastService.cpp
// See header.

#include "ToastService.h"

namespace mf::core::services {

ToastService::ToastService(QObject* parent)
    : QObject(parent)
{
}

void ToastService::bindOverlay(QWidget* overlay) {
    overlay_ = overlay;
}

void ToastService::show(const QString& title,
                        const QString& message,
                        int             durationMs) {
    emit toastRequested(title, message, int(Level::Info), durationMs);
}

void ToastService::showInfo(const QString& title, const QString& message) {
    emit toastRequested(title, message, int(Level::Info), 3000);
}

void ToastService::showSuccess(const QString& title, const QString& message) {
    emit toastRequested(title, message, int(Level::Success), 3000);
}

void ToastService::showWarning(const QString& title, const QString& message) {
    emit toastRequested(title, message, int(Level::Warning), 4000);
}

void ToastService::showError(const QString& title, const QString& message) {
    emit toastRequested(title, message, int(Level::Error), 5000);
}

} // namespace mf::core::services

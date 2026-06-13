// ToastOverlay.h
// Full-rect transparent QWidget that floats over the main window
// and renders a vertical stack of toast cards anchored to the
// bottom-right corner. Listens to ToastService::toastRequested and
// owns the per-toast QFrame + QTimer.
//
// Each toast card is a QFrame that:
//   * fades in via QGraphicsOpacityEffect (or QPropertyAnimation)
//   * auto-removes itself after durationMs via a QTimer
//   * recomputes its geometry on parent resize (so toasts stay
//     pinned to the bottom-right when the window resizes)
//
// Visual styling is theme-aware: the overlay pulls the current
// MusicefyColorScheme and rebuilds the toast card stylesheet on
// every schemeChanged signal.

#pragma once

#include <QFrame>
#include <QList>
#include <QWidget>

class QLabel;
class QTimer;
class QVBoxLayout;

namespace mf::core::services { class ToastService; }
namespace mf::core::theme     { class ThemeManager; }

namespace mf::app::widgets {

class ToastCard;
class ToastOverlay : public QWidget {
    Q_OBJECT
public:
    ToastOverlay(mf::core::services::ToastService* service,
                 mf::core::theme::ThemeManager*     theme,
                 QWidget* parent = nullptr);
    ~ToastOverlay() override = default;

protected:
    void resizeEvent(QResizeEvent* e) override;

private slots:
    void onToastRequested(QString title, QString message, int level, int durationMs);
    void onThemeChanged();

private:
    void applyTheme();
    void repositionCards();

    mf::core::services::ToastService* service_ = nullptr;
    mf::core::theme::ThemeManager*     theme_   = nullptr;

    QVBoxLayout* stack_ = nullptr;
    QList<ToastCard*> cards_;
};

class ToastCard : public QFrame {
    Q_OBJECT
    friend class ToastOverlay;
public:
    ToastCard(const QString& title,
              const QString& message,
              int             level,
              int             durationMs,
              mf::core::theme::ThemeManager* theme,
              QWidget* parent = nullptr);
    ~ToastCard() override = default;

    int level() const { return level_; }
    void startAutoDismiss();

private:
    void buildUi();
    void applyTheme();
    void animateIn();

    int  level_       = 0;
    int  durationMs_  = 0;
    mf::core::theme::ThemeManager* theme_ = nullptr;

    QLabel* titleLabel_   = nullptr;
    QLabel* messageLabel_ = nullptr;
    QTimer* dismissTimer_ = nullptr;
};

} // namespace mf::app::widgets

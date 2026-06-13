// OverlayStackAnimator.h
// Wraps the MainWindow's two QStackedWidgets (page stack + overlay
// stack) with a quick fade-to-black-then-back transition when
// switching between them. Rapid switches are coalesced: a new
// request arriving mid-animation updates the pending target
// without restarting the visible fade, so the UI doesn't strobe
// when the user mashes the sidebar.

#pragma once

#include <QObject>

class QStackedWidget;
class QGraphicsOpacityEffect;
class QPropertyAnimation;

namespace mf::app::widgets {

class OverlayStackAnimator : public QObject {
    Q_OBJECT
public:
    explicit OverlayStackAnimator(QStackedWidget* contentStack,
                                  QStackedWidget* overlayStack,
                                  QObject*        parent = nullptr);
    ~OverlayStackAnimator() override;

    // Fade to the page stack (overlay closing).
    void showPage();

    // Fade to the overlay stack, showing overlay index `index`.
    void showOverlay(int index);

    // True while a fade is in progress.
    bool isAnimating() const { return animating_; }

signals:
    // Emitted after the fade-in completes; signals the new state
    // is fully visible. (pageIndex = -1 means an overlay is now
    // shown; overlayIndex = -1 means the page is shown.)
    void transitionFinished(int pageIndex, int overlayIndex);

private slots:
    void onFadeOutFinished();
    void onFadeInFinished();

private:
    enum class Target { Page, Overlay };

    void beginTransition(Target target, int overlayIndex);
    void runFadeOut();
    void runFadeIn();

    QStackedWidget*       contentStack_ = nullptr;
    QStackedWidget*       overlayStack_ = nullptr;
    QGraphicsOpacityEffect* fx_         = nullptr;
    QPropertyAnimation*   fade_         = nullptr;

    // The committed state (what the user is currently looking at
    // after all pending animations have settled).
    Target committedTarget_ = Target::Page;
    int    committedOverlay_ = -1;

    // The pending state (what the next transition will commit to).
    // Updated on every request; read when the fade-out finishes.
    Target pendingTarget_  = Target::Page;
    int    pendingOverlay_ = -1;

    // True between beginTransition() and the fade-in's finished
    // signal. Used to coalesce rapid requests.
    bool animating_ = false;
};

} // namespace mf::app::widgets

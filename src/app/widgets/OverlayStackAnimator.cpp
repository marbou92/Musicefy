// OverlayStackAnimator.cpp
// See header.
//
// The transition is a two-stage fade:
//   1. opacity 1.0 → 0.0 (fadeOutMs), via QPropertyAnimation
//   2. swap contentStack_ + overlayStack_ indices
//   3. opacity 0.0 → 1.0 (fadeInMs)
//
// Rapid switch handling: every showPage/showOverlay call updates
// pendingTarget_/pendingOverlay_. The fade-out itself is allowed
// to complete (we don't strobe), but the stage-2 swap uses the
// latest pending target. The fade-in then starts from the same
// opacity (= 0.0) and animates back up.

#include "OverlayStackAnimator.h"

#include <QGraphicsOpacityEffect>
#include <QPropertyAnimation>
#include <QStackedWidget>

namespace mf::app::widgets {

namespace {
constexpr int kFadeMs = 150;
} // namespace

OverlayStackAnimator::OverlayStackAnimator(QStackedWidget* contentStack,
                                           QStackedWidget* overlayStack,
                                           QObject*        parent)
    : QObject(parent)
    , contentStack_(contentStack)
    , overlayStack_(overlayStack)
{
    if (!contentStack_) return;
    // Attach a single opacity effect to the content stack. Both
    // the page stack and the overlay stack sit inside contentStack_,
    // so animating it covers every cross-fade case.
    fx_ = new QGraphicsOpacityEffect(contentStack_);
    fx_->setOpacity(1.0);
    contentStack_->setGraphicsEffect(fx_);

    fade_ = new QPropertyAnimation(fx_, "opacity", this);
    fade_->setDuration(kFadeMs);
    fade_->setEasingCurve(QEasingCurve::InOutQuad);
    connect(fade_, &QPropertyAnimation::finished,
            this, [this]() {
        if (fade_->endValue().toDouble() < 0.5) {
            onFadeOutFinished();
        } else {
            onFadeInFinished();
        }
    });
}

OverlayStackAnimator::~OverlayStackAnimator() = default;

void OverlayStackAnimator::showPage() {
    beginTransition(Target::Page, -1);
}

void OverlayStackAnimator::showOverlay(int index) {
    beginTransition(Target::Overlay, index);
}

void OverlayStackAnimator::beginTransition(Target target, int overlayIndex) {
    if (!contentStack_ || !overlayStack_) return;

    // Coalesce: if the requested state is already the committed
    // state and we aren't animating, it's a no-op. (Catches the
    // common case of a stray signal during a rebuild.)
    if (!animating_
        && target == committedTarget_
        && (target == Target::Page || overlayIndex == committedOverlay_)) {
        return;
    }

    pendingTarget_  = target;
    pendingOverlay_ = overlayIndex;

    if (animating_) {
        // A fade is in progress. The visible fade-out will finish
        // on its own; when it does, onFadeOutFinished() will pick
        // up the latest pendingTarget_/pendingOverlay_. The same
        // is true for fade-in: if a new request comes in mid-fade-in,
        // the user sees the new state when the fade-in completes.
        // We do NOT restart the animation here — restarting
        // creates visible jitter when the user is rapidly switching.
        return;
    }

    // Nothing in flight: start the fade-out. If we're already at
    // opacity 0 (rare — only after a dtor race), just commit and
    // start the fade-in.
    if (qFuzzyCompare(fx_->opacity(), 0.0)) {
        runFadeIn();
    } else {
        runFadeOut();
    }
}

void OverlayStackAnimator::runFadeOut() {
    animating_ = true;
    fade_->stop();
    fade_->setStartValue(fx_->opacity());
    fade_->setEndValue(0.0);
    fade_->start();
}

void OverlayStackAnimator::runFadeIn() {
    animating_ = true;
    fade_->stop();
    fade_->setStartValue(fx_->opacity());
    fade_->setEndValue(1.0);
    fade_->start();
}

void OverlayStackAnimator::onFadeOutFinished() {
    // Commit the latest pending state.
    committedTarget_  = pendingTarget_;
    committedOverlay_ = pendingOverlay_;

    if (committedTarget_ == Target::Overlay) {
        if (committedOverlay_ >= 0
            && committedOverlay_ < overlayStack_->count()) {
            overlayStack_->setCurrentIndex(committedOverlay_);
        }
        contentStack_->setCurrentIndex(1);
    } else {
        contentStack_->setCurrentIndex(0);
    }

    // Stage 2: fade back up.
    runFadeIn();
}

void OverlayStackAnimator::onFadeInFinished() {
    animating_ = false;
    // If a new request came in during the fade-in, run another
    // transition. (Begin-transition's coalescing in showPage()
    // /showOverlay() will pick this up by re-entering beginTransition
    // which will now see animating_ == false and start a fresh
    // fade-out. To avoid restarting from opacity 1.0, we instead
    // accept that: opacity is at 1.0 so a fade-out is the natural
    // next step.)
    if (pendingTarget_  != committedTarget_
        || (pendingTarget_ == Target::Overlay
            && pendingOverlay_ != committedOverlay_)) {
        runFadeOut();
    } else {
        emit transitionFinished(
            committedTarget_ == Target::Page ? 0 : -1,
            committedOverlay_);
    }
}

} // namespace mf::app::widgets

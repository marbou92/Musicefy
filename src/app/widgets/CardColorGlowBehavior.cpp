// CardColorGlowBehavior.cpp

#include "CardColorGlowBehavior.h"

#include <QEvent>
#include <QGraphicsDropShadowEffect>
#include <QHoverEvent>
#include <QLabel>
#include <QPainter>
#include <QPropertyAnimation>
#include <QWidget>

namespace mf::app::widgets {

// ──────────────────────────────────────────────────────────────────
CardColorGlowBehavior* CardColorGlowBehavior::install(QWidget* target,
                                                       mf::core::services::ImageCache*)
{
    if (!target) return nullptr;
    auto* b = new CardColorGlowBehavior(target, target);
    target->installEventFilter(b);
    target->setAttribute(Qt::WA_Hover);
    return b;
}

// ──────────────────────────────────────────────────────────────────
CardColorGlowBehavior::CardColorGlowBehavior(QWidget* target, QObject* parent)
    : QObject(parent), target_(target)
{
    glowAnim_ = new QPropertyAnimation(this, "glowOpacity", this);
    glowAnim_->setDuration(250);
}

// ──────────────────────────────────────────────────────────────────
void CardColorGlowBehavior::setGlowOpacity(float v)
{
    if (qFuzzyCompare(glowOpacity_, v)) return;
    glowOpacity_ = v;
    emit glowOpacityChanged();
    applyGlowStyle();
}

// ──────────────────────────────────────────────────────────────────
bool CardColorGlowBehavior::eventFilter(QObject* obj, QEvent* event)
{
    if (obj != target_) return false;

    switch (event->type()) {
    case QEvent::HoverEnter:
        extractColorFromCover();
        startGlowIn();
        return false;
    case QEvent::HoverLeave:
        startGlowOut();
        return false;
    default:
        return false;
    }
}

// ──────────────────────────────────────────────────────────────────
void CardColorGlowBehavior::extractColorFromCover()
{
    // Look for a QLabel child with a pixmap (cover art)
    if (!target_) return;
    auto children = target_->findChildren<QLabel*>();
    for (auto* lbl : children) {
        auto pix = lbl->pixmap();
        if (pix && !pix->isNull()) {
            // Sample center pixel for dominant color
            QImage img = pix->toImage().scaled(1, 1, Qt::IgnoreAspectRatio, Qt::SmoothTransformation);
            if (!img.isNull()) {
                dominantColor_ = QColor(img.pixelColor(0, 0));
                return;
            }
        }
    }
    // Fallback: use palette highlight color
    dominantColor_ = target_->palette().highlight().color();
}

// ──────────────────────────────────────────────────────────────────
void CardColorGlowBehavior::startGlowIn()
{
    glowAnim_->stop();
    glowAnim_->setStartValue(glowOpacity_);
    glowAnim_->setEndValue(0.45f);
    glowAnim_->start();
}

// ──────────────────────────────────────────────────────────────────
void CardColorGlowBehavior::startGlowOut()
{
    glowAnim_->stop();
    glowAnim_->setStartValue(glowOpacity_);
    glowAnim_->setEndValue(0.0f);
    glowAnim_->start();
}

// ──────────────────────────────────────────────────────────────────
void CardColorGlowBehavior::applyGlowStyle()
{
    if (!target_ || !dominantColor_.isValid()) return;

    if (glowOpacity_ < 0.01f) {
        target_->setGraphicsEffect(nullptr);
        return;
    }

    auto* shadow = new QGraphicsDropShadowEffect(target_);
    shadow->setBlurRadius(30);
    shadow->setOffset(0, 0);
    shadow->setColor(QColor(dominantColor_.red(),
                            dominantColor_.green(),
                            dominantColor_.blue(),
                            static_cast<int>(glowOpacity_ * 255)));
    target_->setGraphicsEffect(shadow);
}

} // namespace mf::app::widgets

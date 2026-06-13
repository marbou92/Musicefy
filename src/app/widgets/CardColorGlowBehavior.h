// CardColorGlowBehavior.h
// Hover glow effect for cards. Extracts the dominant color from a
// card's cover art and applies a semi-transparent glow on mouse
// enter, fading out on mouse leave.

#pragma once

#include <QObject>
#include <QColor>
#include <QPropertyAnimation>
#include <QPointer>

class QWidget;

namespace mf::core::services { class ImageCache; }

namespace mf::app::widgets {

class CardColorGlowBehavior : public QObject {
    Q_OBJECT
    Q_PROPERTY(float glowOpacity READ glowOpacity WRITE setGlowOpacity NOTIFY glowOpacityChanged)
public:
    /// Install the glow behavior on a widget. The widget's stylesheet
    /// will be modified on hover; ensure it has a transparent background.
    static CardColorGlowBehavior* install(QWidget* target,
                                          mf::core::services::ImageCache* imageCache = nullptr);

    float glowOpacity() const { return glowOpacity_; }
    void setGlowOpacity(float v);

signals:
    void glowOpacityChanged();

protected:
    bool eventFilter(QObject* obj, QEvent* event) override;

private:
    explicit CardColorGlowBehavior(QWidget* target, QObject* parent = nullptr);

    QWidget* target_ = nullptr;
    QColor dominantColor_;
    float glowOpacity_ = 0.0f;
    QPropertyAnimation* glowAnim_ = nullptr;

    void extractColorFromCover();
    void startGlowIn();
    void startGlowOut();
    void applyGlowStyle();
};

} // namespace mf::app::widgets

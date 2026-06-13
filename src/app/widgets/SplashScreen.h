// SplashScreen.h
// Animated branded splash screen shown during application startup.
// Displays the Musicefy logo with a pulsing opacity animation and
// a progress bar with status text.

#pragma once

#include <QSplashScreen>
#include <QTimer>

class QLabel;
class QProgressBar;

namespace mf::app::widgets {

class SplashScreen : public QSplashScreen {
    Q_OBJECT
public:
    explicit SplashScreen(const QPixmap& logo, QWidget* parent = nullptr);
    ~SplashScreen() override;

    /// Update the status text.
    void setMessage(const QString& text);

    /// Advance the progress bar (0-100).
    void setProgress(int percent);

    /// Start the logo pulse animation.
    void startAnimation();

    /// Stop the pulse animation (call before hiding).
    void stopAnimation();

protected:
    void paintEvent(QPaintEvent* event) override;

private slots:
    void onPulse();

private:
    void buildUi(const QPixmap& logo);

    QLabel*       logoLabel_    = nullptr;
    QLabel*       titleLabel_   = nullptr;
    QLabel*       statusLabel_  = nullptr;
    QProgressBar* progressBar_  = nullptr;
    QTimer*       pulseTimer_   = nullptr;
    qreal         pulseOpacity_ = 1.0;
    int           pulseDirection_ = -1;
};

} // namespace mf::app::widgets

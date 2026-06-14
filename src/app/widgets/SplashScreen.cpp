// SplashScreen.cpp
// See header.

#include "SplashScreen.h"

#include <QFont>
#include <QLabel>
#include <QPainter>
#include <QPainterPath>
#include <QProgressBar>
#include <QPaintEvent>
#include <QVBoxLayout>

namespace mf::app::widgets {

static constexpr int kSplashWidth  = 480;
static constexpr int kSplashHeight = 340;

SplashScreen::SplashScreen(const QPixmap& logo, QWidget* parent)
    : QSplashScreen(logo)
{
    Q_UNUSED(parent);
    setFixedSize(kSplashWidth, kSplashHeight);
    setWindowFlags(Qt::SplashScreen | Qt::FramelessWindowHint | Qt::WindowStaysOnTopHint);
    setAttribute(Qt::WA_TranslucentBackground);

    buildUi(logo);

    pulseTimer_ = new QTimer(this);
    pulseTimer_->setInterval(50); // 20 FPS
    connect(pulseTimer_, &QTimer::timeout, this, &SplashScreen::onPulse);
}

SplashScreen::~SplashScreen() {
    stopAnimation();
}

void SplashScreen::buildUi(const QPixmap& logo) {
    auto* root = new QVBoxLayout(this);
    root->setContentsMargins(40, 30, 40, 30);
    root->setSpacing(0);

    // Logo
    logoLabel_ = new QLabel(this);
    if (!logo.isNull()) {
        QPixmap scaled = logo.scaled(80, 80, Qt::KeepAspectRatio, Qt::SmoothTransformation);
        logoLabel_->setPixmap(scaled);
    }
    logoLabel_->setAlignment(Qt::AlignCenter);
    root->addWidget(logoLabel_);

    root->addSpacing(16);

    // Title
    titleLabel_ = new QLabel(QStringLiteral("Musicefy"), this);
    QFont tf = titleLabel_->font();
    tf.setPointSize(22);
    tf.setBold(true);
    titleLabel_->setFont(tf);
    titleLabel_->setAlignment(Qt::AlignCenter);
    root->addWidget(titleLabel_);

    root->addSpacing(24);

    // Progress bar
    progressBar_ = new QProgressBar(this);
    progressBar_->setRange(0, 100);
    progressBar_->setValue(0);
    progressBar_->setFixedHeight(4);
    progressBar_->setTextVisible(false);
    progressBar_->setStyleSheet(QStringLiteral(
        "QProgressBar { background: rgba(255,255,255,40); border: none; border-radius: 2px; }"
        "QProgressBar::chunk { background: rgba(255,255,255,200); border-radius: 2px; }"
    ));
    root->addWidget(progressBar_);

    root->addSpacing(8);

    // Status
    statusLabel_ = new QLabel(QStringLiteral("Starting\u2026"), this);
    QFont sf = statusLabel_->font();
    sf.setPointSize(10);
    statusLabel_->setFont(sf);
    statusLabel_->setAlignment(Qt::AlignCenter);
    statusLabel_->setStyleSheet(QStringLiteral("color: rgba(255,255,255,180);"));
    root->addWidget(statusLabel_);
}

void SplashScreen::paintEvent(QPaintEvent* /*event*/) {
    QPainter painter(this);
    painter.setRenderHint(QPainter::Antialiasing);

    // Draw semi-transparent dark background with rounded corners.
    QPainterPath path;
    path.addRoundedRect(rect(), 16, 16);
    painter.setClipPath(path);

    QLinearGradient grad(0, 0, 0, height());
    grad.setColorAt(0.0, QColor(30, 30, 40, 240));
    grad.setColorAt(1.0, QColor(20, 20, 28, 240));
    painter.fillRect(rect(), grad);

    // Children (labels, progress bar) paint themselves via Qt's
    // standard widget paint machinery — no explicit call needed.
}

void SplashScreen::setMessage(const QString& text) {
    if (statusLabel_) statusLabel_->setText(text);
}

void SplashScreen::setProgress(int percent) {
    if (progressBar_) progressBar_->setValue(qBound(0, percent, 100));
}

void SplashScreen::startAnimation() {
    if (pulseTimer_ && !pulseTimer_->isActive()) {
        pulseOpacity_ = 1.0;
        pulseDirection_ = -1;
        pulseTimer_->start();
    }
}

void SplashScreen::stopAnimation() {
    if (pulseTimer_) pulseTimer_->stop();
}

void SplashScreen::onPulse() {
    pulseOpacity_ += pulseDirection_ * 0.02;
    if (pulseOpacity_ <= 0.5) {
        pulseOpacity_ = 0.5;
        pulseDirection_ = 1;
    } else if (pulseOpacity_ >= 1.0) {
        pulseOpacity_ = 1.0;
        pulseDirection_ = -1;
    }

    if (!logoLabel_) return;

    const QPixmap* currentPixmap = logoLabel_->pixmap();
    if (!currentPixmap || currentPixmap->isNull()) return;

    // Cache the original (non-faded) logo to avoid re-scaling each frame.
    if (cachedOriginal_.isNull()) {
        cachedOriginal_ = *currentPixmap;
    }
    if (cachedOriginal_.isNull()) return;

    QPixmap faded(cachedOriginal_.size());
    faded.fill(Qt::transparent);
    QPainter p(&faded);
    if (p.isActive()) {
        p.setOpacity(pulseOpacity_);
        p.drawPixmap(0, 0, cachedOriginal_);
        p.end();
        logoLabel_->setPixmap(faded);
    }
}

} // namespace mf::app::widgets

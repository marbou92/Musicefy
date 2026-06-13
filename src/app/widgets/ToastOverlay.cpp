// ToastOverlay.cpp
// See header.

#include "ToastOverlay.h"

#include "../core/services/ToastService.h"
#include "../core/theme/ThemeManager.h"
#include "../core/theme/MusicefyColorScheme.h"

#include <QFont>
#include <QGraphicsOpacityEffect>
#include <QHBoxLayout>
#include <QLabel>
#include <QPropertyAnimation>
#include <QResizeEvent>
#include <QTimer>
#include <QVBoxLayout>

namespace mf::app::widgets {

using mf::core::services::ToastService;
using mf::core::theme::ThemeManager;
using mf::core::theme::MusicefyColorScheme;

namespace {
constexpr int kMarginRight   = 24;
constexpr int kMarginBottom  = 96; // clear of the now-playing bar
constexpr int kCardSpacing   = 8;
constexpr int kCardMinWidth  = 280;
constexpr int kCardMaxWidth  = 420;
constexpr int kFadeMs        = 180;
}

// ── ToastCard ──────────────────────────────────────────────────────────

ToastCard::ToastCard(const QString& title,
                     const QString& message,
                     int             level,
                     int             durationMs,
                     ThemeManager*   theme,
                     QWidget*        parent)
    : QFrame(parent)
    , level_(level)
    , durationMs_(durationMs)
    , theme_(theme)
{
    setObjectName(QStringLiteral("ToastCard"));
    setFrameShape(QFrame::NoFrame);
    setMinimumWidth(kCardMinWidth);
    setMaximumWidth(kCardMaxWidth);
    setSizePolicy(QSizePolicy::Preferred, QSizePolicy::Maximum);
    buildUi();
    if (titleLabel_) titleLabel_->setText(title);
    if (messageLabel_) messageLabel_->setText(message);
    applyTheme();
    if (theme_) {
        connect(theme_, &ThemeManager::schemeChanged,
                this, &ToastCard::applyTheme);
    }
}

void ToastCard::buildUi() {
    auto* root = new QVBoxLayout(this);
    root->setContentsMargins(14, 10, 14, 10);
    root->setSpacing(2);

    titleLabel_ = new QLabel(this);
    QFont tf = titleLabel_->font();
    tf.setPointSize(10);
    tf.setBold(true);
    titleLabel_->setFont(tf);
    titleLabel_->setText(/*set in ctor*/ QString());
    root->addWidget(titleLabel_);

    messageLabel_ = new QLabel(this);
    messageLabel_->setWordWrap(true);
    QFont mf = messageLabel_->font();
    mf.setPointSize(9);
    messageLabel_->setFont(mf);
    root->addWidget(messageLabel_);

    // Text content is set after construction in the overlay so we can
    // pass it through cleanly with the level for accent color.
}

void ToastCard::applyTheme() {
    MusicefyColorScheme s;
    if (theme_) s = theme_->scheme();
    if (!s.primary.isValid()) {
        setStyleSheet(QString());
        return;
    }

    QString accent;
    QString accentText;
    switch (level_) {
        case int(ToastService::Level::Success):
            accent     = s.primary.name();
            accentText = s.onPrimary.name();
            break;
        case int(ToastService::Level::Warning):
            accent     = s.tertiary.isValid() ? s.tertiary.name() : s.primary.name();
            accentText = s.onTertiary.isValid() ? s.onTertiary.name() : s.onPrimary.name();
            break;
        case int(ToastService::Level::Error):
            accent     = s.error.name();
            accentText = s.onError.isValid() ? s.onError.name() : QLatin1String("#ffffff");
            break;
        case int(ToastService::Level::Info):
        default:
            accent     = s.surfaceContainerHigh.name();
            accentText = s.onSurface.name();
            break;
    }

    setStyleSheet(QStringLiteral(
        "QFrame#ToastCard {"
        "  background: %1; color: %2;"
        "  border: 1px solid %3; border-left: 4px solid %4;"
        "  border-radius: 10px;"
        "}"
        "QLabel { background: transparent; color: %2; }"
    )
    .arg(accent)
    .arg(accentText)
    .arg(s.outlineVariant.name())
    .arg(s.primary.name())
    );
}

void ToastCard::startAutoDismiss() {
    animateIn();
    if (durationMs_ <= 0) return;
    dismissTimer_ = new QTimer(this);
    dismissTimer_->setSingleShot(true);
    connect(dismissTimer_, &QTimer::timeout,
            this, &ToastCard::deleteLater);
    dismissTimer_->start(durationMs_);
}

void ToastCard::animateIn() {
    auto* fx = new QGraphicsOpacityEffect(this);
    setGraphicsEffect(fx);
    fx->setOpacity(0.0);
    auto* a = new QPropertyAnimation(fx, "opacity", this);
    a->setDuration(kFadeMs);
    a->setStartValue(0.0);
    a->setEndValue(1.0);
    a->start(QAbstractAnimation::DeleteWhenStopped);
}

// ── ToastOverlay ───────────────────────────────────────────────────────

ToastOverlay::ToastOverlay(ToastService* service,
                           ThemeManager* theme,
                           QWidget*      parent)
    : QWidget(parent)
    , service_(service)
    , theme_(theme)
{
    setAttribute(Qt::WA_TransparentForMouseEvents, false);
    setAttribute(Qt::WA_NoSystemBackground, true);
    setObjectName(QStringLiteral("ToastOverlay"));

    auto* root = new QVBoxLayout(this);
    root->setContentsMargins(0, 0, 0, 0);
    root->setSpacing(0);
    root->addStretch(1);

    stack_ = new QVBoxLayout;
    stack_->setContentsMargins(0, 0, 0, 0);
    stack_->setSpacing(kCardSpacing);
    stack_->setAlignment(Qt::AlignRight | Qt::AlignBottom);
    root->addLayout(stack_);

    if (service_) {
        connect(service_, &ToastService::toastRequested,
                this, &ToastOverlay::onToastRequested);
    }
    if (theme_) {
        connect(theme_, &ThemeManager::schemeChanged,
                this, &ToastOverlay::onThemeChanged);
    }
}

void ToastOverlay::onToastRequested(QString title, QString message, int level, int durationMs) {
    auto* card = new ToastCard(title, message, level, durationMs, theme_, this);
    card->titleLabel_->setText(title);
    card->messageLabel_->setText(message);
    card->applyTheme();
    stack_->addWidget(card);
    cards_.append(card);
    connect(card, &QObject::destroyed, this, [this, card]() {
        cards_.removeAll(card);
        repositionCards();
    });
    card->startAutoDismiss();
    repositionCards();
}

void ToastOverlay::onThemeChanged() {
    applyTheme();
    for (auto* c : cards_) c->applyTheme();
}

void ToastOverlay::applyTheme() {
    MusicefyColorScheme s;
    if (theme_) s = theme_->scheme();
    if (!s.primary.isValid()) {
        setStyleSheet(QString());
        return;
    }
    setStyleSheet(QStringLiteral(
        "QWidget#ToastOverlay { background: transparent; }"
    ));
}

void ToastOverlay::resizeEvent(QResizeEvent* e) {
    QWidget::resizeEvent(e);
    repositionCards();
}

void ToastOverlay::repositionCards() {
    // Anchor the bottom of the stack kMarginBottom above the bottom of
    // the overlay (clear of the now-playing bar). Width is constrained
    // by the overlay width minus margins.
    int usableW = std::max(kCardMinWidth, width() - 2 * kMarginRight);
    int cardW  = std::min(kCardMaxWidth, usableW);
    for (auto* c : cards_) {
        c->setFixedWidth(cardW);
    }
    int totalH = 0;
    for (auto* c : cards_) {
        totalH += c->sizeHint().height() + kCardSpacing;
    }
    int x = width() - cardW - kMarginRight;
    int y = height() - totalH - kMarginBottom;
    if (y < 0) y = 0;
    int offsetY = 0;
    for (auto* c : cards_) {
        c->move(x, y + offsetY);
        c->raise();
        offsetY += c->sizeHint().height() + kCardSpacing;
    }
}

} // namespace mf::app::widgets

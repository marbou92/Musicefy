// LyricsPanel.cpp

#include "LyricsPanel.h"

#include "../viewmodels/PlayerViewModel.h"
#include "../../core/theme/ThemeManager.h"
#include "../../core/theme/MusicefyColorScheme.h"

#include <QVBoxLayout>
#include <QScrollArea>
#include <QLabel>
#include <QPropertyAnimation>
#include <QEasingCurve>

namespace mf::app::widgets {

using mf::app::viewmodels::PlayerViewModel;
using mf::core::theme::ThemeManager;
using mf::core::theme::MusicefyColorScheme;

LyricsPanel::LyricsPanel(PlayerViewModel* vm,
                         ThemeManager*    theme,
                         QWidget*         parent)
    : QWidget(parent)
    , vm_(vm)
    , theme_(theme)
{
    buildUi();
    applyTheme();
    setMaximumHeight(0);
    setVisible(false);

    showAnim_ = new QPropertyAnimation(this, "panelHeight", this);
    showAnim_->setDuration(350);
    showAnim_->setEasingCurve(QEasingCurve::OutCubic);

    hideAnim_ = new QPropertyAnimation(this, "panelHeight", this);
    hideAnim_->setDuration(250);
    hideAnim_->setEasingCurve(QEasingCurve::InCubic);
    connect(hideAnim_, &QPropertyAnimation::finished, this, [this]() {
        setVisible(false);
        emit visibilityChanged(false);
    });

    if (vm_) {
        connect(vm_, &PlayerViewModel::currentTrackChanged,
                this, &LyricsPanel::onCurrentTrackChanged);
    }
    if (theme_) {
        connect(theme_, &ThemeManager::schemeChanged,
                this, &LyricsPanel::onThemeChanged);
    }
}

// ──────────────────────────────────────────────────────────────────
void LyricsPanel::buildUi()
{
    auto* root = new QVBoxLayout(this);
    root->setContentsMargins(0, 0, 0, 0);
    root->setSpacing(0);

    // Rounded top border
    auto* borderWidget = new QWidget;
    auto* borderLayout = new QVBoxLayout(borderWidget);
    borderLayout->setContentsMargins(24, 16, 24, 8);

    titleLabel_ = new QLabel(QStringLiteral("Lyrics"));
    QFont tf = titleLabel_->font();
    tf.setPointSize(14);
    tf.setBold(true);
    titleLabel_->setFont(tf);
    titleLabel_->setAlignment(Qt::AlignHCenter);
    borderLayout->addWidget(titleLabel_);

    scrollArea_ = new QScrollArea;
    scrollArea_->setWidgetResizable(true);
    scrollArea_->setHorizontalScrollBarPolicy(Qt::ScrollBarAlwaysOff);
    scrollArea_->setVerticalScrollBarPolicy(Qt::ScrollBarAlwaysOff);
    scrollArea_->setFrameShape(QFrame::NoFrame);

    contentWidget_ = new QWidget;
    contentLayout_ = new QVBoxLayout(contentWidget_);
    contentLayout_->setContentsMargins(0, 0, 0, 16);
    contentLayout_->setSpacing(10);

    // Placeholder for empty lyrics
    auto* placeholder = new QLabel(QStringLiteral("Instrumental"));
    placeholder->setAlignment(Qt::AlignCenter);
    placeholder->setProperty("role", QStringLiteral("secondary"));
    contentLayout_->addWidget(placeholder);

    scrollArea_->setWidget(contentWidget_);
    borderLayout->addWidget(scrollArea_, 1);

    root->addWidget(borderWidget, 1);
}

// ──────────────────────────────────────────────────────────────────
void LyricsPanel::applyTheme()
{
    MusicefyColorScheme s;
    if (theme_) s = theme_->scheme();
    if (!s.surface.isValid()) return;

    setStyleSheet(QStringLiteral(
        "QWidget { background: %1; }"
        "QLabel { color: %2; }"
        "QLabel[role=\"secondary\"] { color: %3; }"
        "QScrollArea { background: %1; border: none; }"
    )
    .arg(s.surfaceContainer.name(),
         s.onSurface.name(),
         s.onSurfaceVariant.name()));

    if (titleLabel_) {
        titleLabel_->setStyleSheet(QStringLiteral(
            "color: %1; font-size: 14px; font-weight: bold;"
        ).arg(s.onSurface.name()));
    }
}

// ──────────────────────────────────────────────────────────────────
void LyricsPanel::updateLyrics()
{
    // Clear existing labels
    QLayoutItem* item;
    while ((item = contentLayout_->takeAt(0)) != nullptr) {
        if (item->widget()) item->widget()->deleteLater();
        delete item;
    }

    if (!vm_) return;

    const QString lyrics = vm_->currentLyrics();
    if (lyrics.isEmpty()) {
        auto* placeholder = new QLabel(QStringLiteral("Instrumental"));
        placeholder->setAlignment(Qt::AlignCenter);
        placeholder->setProperty("role", QStringLiteral("secondary"));
        contentLayout_->addWidget(placeholder);
        return;
    }

    const QStringList lines = lyrics.split(QLatin1Char('\n'));
    MusicefyColorScheme s;
    if (theme_) s = theme_->scheme();

    for (const auto& line : lines) {
        auto* label = new QLabel(line.trimmed());
        QFont lf = label->font();
        lf.setPointSize(16);
        lf.setBold(true);
        label->setFont(lf);
        label->setWordWrap(true);
        label->setContentsMargins(8, 8, 8, 8);
        label->setStyleSheet(QStringLiteral(
            "color: %1; opacity: 0.4;"
        ).arg(s.onSurface.name()));
        contentLayout_->addWidget(label);
    }

    contentLayout_->addStretch();
}

// ──────────────────────────────────────────────────────────────────
void LyricsPanel::onCurrentTrackChanged()
{
    updateLyrics();
}

// ──────────────────────────────────────────────────────────────────
void LyricsPanel::onThemeChanged()
{
    applyTheme();
    updateLyrics();
}

// ──────────────────────────────────────────────────────────────────
void LyricsPanel::toggle()
{
    if (showing_)
        hidePanel();
    else
        showPanel();
}

// ──────────────────────────────────────────────────────────────────
void LyricsPanel::showPanel()
{
    if (showing_) return;
    showing_ = true;
    updateLyrics();
    animateShow();
    emit visibilityChanged(true);
}

// ──────────────────────────────────────────────────────────────────
void LyricsPanel::hidePanel()
{
    if (!showing_) return;
    showing_ = false;
    animateHide();
}

// ──────────────────────────────────────────────────────────────────
void LyricsPanel::animateShow()
{
    showAnim_->stop();
    hideAnim_->stop();
    setVisible(true);

    const int targetHeight = 440;
    showAnim_->setStartValue(maximumHeight());
    showAnim_->setEndValue(targetHeight);
    showAnim_->start();
}

// ──────────────────────────────────────────────────────────────────
void LyricsPanel::animateHide()
{
    showAnim_->stop();
    hideAnim_->stop();
    hideAnim_->setStartValue(maximumHeight());
    hideAnim_->setEndValue(0);
    hideAnim_->start();
}

} // namespace mf::app::widgets

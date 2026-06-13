// AppearancePanel.cpp
// See header. Replaces the Appearance stub from SettingsPage.

#include "AppearancePanel.h"

#include "../core/theme/AppTheme.h"
#include "../core/theme/AppThemeColorSchemes.h"
#include "../core/theme/MusicefyColorScheme.h"
#include "../core/theme/ThemeManager.h"
#include "../core/theme/ThemeMode.h"

#include <QButtonGroup>
#include <QColorDialog>
#include <QFont>
#include <QFrame>
#include <QGridLayout>
#include <QHBoxLayout>
#include <QLabel>
#include <QMouseEvent>
#include <QPainter>
#include <QPainterPath>
#include <QRadioButton>
#include <QToolButton>
#include <QVBoxLayout>

namespace mf::app::widgets {

using mf::core::theme::AppTheme;
using mf::core::theme::ThemeMode;
using mf::core::theme::ThemeManager;
using mf::core::theme::MusicefyColorScheme;
using mf::core::theme::appThemeAccentHex;
using mf::core::theme::appThemeDisplayName;
using mf::core::theme::themeModeDisplayName;

namespace {

// ─── ThemeSwatch ───────────────────────────────────────────────────────
// One swatch in the Theme grid. Square color tile on top, name label
// underneath. Selected state adds a thick primary-tinted outline.
class ThemeSwatch : public QFrame {
    Q_OBJECT
public:
    ThemeSwatch(AppTheme t, QWidget* parent = nullptr)
        : QFrame(parent), theme_(t) {
        setFixedSize(96, 100);
        setCursor(Qt::PointingHandCursor);
        setObjectName(QStringLiteral("ThemeSwatch"));
    }

    AppTheme theme() const { return theme_; }
    QString name() const { return appThemeDisplayName(theme_); }
    QColor accent() const { return QColor(appThemeAccentHex(theme_)); }

    void setSelected(bool s) {
        if (s == selected_) return;
        selected_ = s;
        update();
    }
    bool isSelected() const { return selected_; }

signals:
    void activated(int themeId);

protected:
    void paintEvent(QPaintEvent*) override {
        QPainter p(this);
        p.setRenderHint(QPainter::Antialiasing);

        QRect tileRect(8, 4, 80, 56);
        QRect labelRect(0, 64, width(), 32);

        // Tile.
        QPainterPath path;
        path.addRoundedRect(tileRect, 10, 10);
        p.fillPath(path, accent());
        if (selected_) {
            QPen pen(QColor(QStringLiteral("#FFFFFF")));
            pen.setWidth(3);
            p.setPen(pen);
            p.setBrush(Qt::NoBrush);
            p.drawPath(path);
        } else {
            QPen pen(QColor(0, 0, 0, 30));
            pen.setWidth(1);
            p.setPen(pen);
            p.drawPath(path);
        }

        // Label.
        p.setPen(QColor(selected_
                        ? QStringLiteral("#FFFFFF")
                        : QStringLiteral("#CCCCCC")));
        QFont f = p.font();
        f.setPointSize(9);
        p.setFont(f);
        p.drawText(labelRect, Qt::AlignHCenter | Qt::AlignTop, name());
    }

    void mousePressEvent(QMouseEvent* e) override {
        if (e->button() == Qt::LeftButton) {
            emit activated(int(theme_));
        }
        QFrame::mousePressEvent(e);
    }

private:
    AppTheme theme_;
    bool selected_ = false;
};

} // anonymous namespace

AppearancePanel::AppearancePanel(ThemeManager* theme, QWidget* parent)
    : QWidget(parent)
    , theme_(theme)
{
    accentPresets_ << QColor(QStringLiteral("#E53935"))   // Red
                   << QColor(QStringLiteral("#FB8C00"))   // Orange
                   << QColor(QStringLiteral("#FDD835"))   // Yellow
                   << QColor(QStringLiteral("#43A047"))   // Green
                   << QColor(QStringLiteral("#00ACC1"))   // Teal
                   << QColor(QStringLiteral("#1E88E5"))   // Blue
                   << QColor(QStringLiteral("#8E24AA"))   // Purple
                   << QColor(QStringLiteral("#D81B60"));  // Pink

    buildUi();
    applyTheme();
    syncModeSelection();
    syncSwatchSelection();
    syncAccentVisibility();

    if (theme_) {
        connect(theme_, &ThemeManager::themeChanged,
                this, &AppearancePanel::onThemeChanged);
        connect(theme_, &ThemeManager::modeChanged,
                this, &AppearancePanel::onModeChanged);
        connect(theme_, &ThemeManager::schemeChanged,
                this, &AppearancePanel::onSchemeChanged);
    }
}

void AppearancePanel::buildUi() {
    auto* root = new QVBoxLayout(this);
    root->setContentsMargins(32, 28, 32, 28);
    root->setSpacing(18);

    auto* title = new QLabel(QStringLiteral("Appearance"), this);
    QFont tf = title->font();
    tf.setPointSize(18);
    tf.setBold(true);
    title->setFont(tf);
    root->addWidget(title);

    auto* blurb = new QLabel(
        QStringLiteral("Pick a colour palette and a brightness mode. "
                       "Changes are applied instantly and saved on close."),
        this);
    blurb->setWordWrap(true);
    blurb->setProperty("role", QStringLiteral("secondary"));
    root->addWidget(blurb);

    // ── Mode ──────────────────────────────────────────────────────
    auto* modeHeader = new QLabel(QStringLiteral("Mode"), this);
    QFont mh = modeHeader->font();
    mh.setPointSize(12);
    mh.setBold(true);
    modeHeader->setFont(mh);
    root->addWidget(modeHeader);

    auto* modeRow = new QHBoxLayout();
    modeRow->setSpacing(18);

    modeGroup_ = new QButtonGroup(this);
    modeSystem_ = new QRadioButton(themeModeDisplayName(ThemeMode::System), this);
    modeLight_  = new QRadioButton(themeModeDisplayName(ThemeMode::Light),  this);
    modeDark_   = new QRadioButton(themeModeDisplayName(ThemeMode::Dark),   this);
    modeAmoled_ = new QRadioButton(themeModeDisplayName(ThemeMode::Amoled), this);
    modeGroup_->addButton(modeSystem_, int(ThemeMode::System));
    modeGroup_->addButton(modeLight_,  int(ThemeMode::Light));
    modeGroup_->addButton(modeDark_,   int(ThemeMode::Dark));
    modeGroup_->addButton(modeAmoled_, int(ThemeMode::Amoled));
    connect(modeGroup_, QOverload<int>::of(&QButtonGroup::buttonClicked),
            this, [this](int id) {
                if (theme_) theme_->setMode(ThemeMode(id));
            });
    modeRow->addWidget(modeSystem_);
    modeRow->addWidget(modeLight_);
    modeRow->addWidget(modeDark_);
    modeRow->addWidget(modeAmoled_);
    modeRow->addStretch(1);
    root->addLayout(modeRow);

    // ── Theme grid ────────────────────────────────────────────────
    auto* themeHeader = new QLabel(QStringLiteral("Theme"), this);
    QFont th = themeHeader->font();
    th.setPointSize(12);
    th.setBold(true);
    themeHeader->setFont(th);
    root->addWidget(themeHeader);

    auto* grid = new QGridLayout();
    grid->setHorizontalSpacing(10);
    grid->setVerticalSpacing(10);
    const int kCols = 6;
    const auto themes = mf::core::theme::allAppThemes();
    int row = 0, col = 0;
    for (AppTheme t : themes) {
        auto* sw = new ThemeSwatch(t, this);
        connect(sw, &ThemeSwatch::activated,
                this, &AppearancePanel::onSwatchActivated);
        grid->addWidget(sw, row, col);
        ++col;
        if (col >= kCols) { col = 0; ++row; }
    }
    root->addLayout(grid);

    // ── Accent (only visible when Dynamic) ────────────────────────
    auto* accentHeader = new QLabel(QStringLiteral("Accent (Dynamic theme)"), this);
    QFont ah = accentHeader->font();
    ah.setPointSize(12);
    ah.setBold(true);
    accentHeader->setFont(ah);
    root->addWidget(accentHeader);

    accentContainer_ = new QWidget(this);
    auto* accentLayout = new QHBoxLayout(accentContainer_);
    accentLayout->setContentsMargins(0, 0, 0, 0);
    accentLayout->setSpacing(10);

    accentSwatch_ = new QLabel(accentContainer_);
    accentSwatch_->setFixedSize(36, 36);
    accentSwatch_->setProperty("role", QStringLiteral("accentSwatch"));
    accentLayout->addWidget(accentSwatch_);

    auto* presetRow = new QHBoxLayout();
    presetRow->setSpacing(6);
    for (int i = 0; i < accentPresets_.size(); ++i) {
        auto* b = new QToolButton(accentContainer_);
        b->setFixedSize(28, 28);
        b->setProperty("accentId", i);
        b->setToolTip(QStringLiteral("Pick accent colour"));
        b->setCursor(Qt::PointingHandCursor);
        b->setProperty("role", QStringLiteral("accentPreset"));
        // Per-button background colour is applied via dynamic property;
        // stylesheet targets role="accentPreset" + dynamicProperty.
        QString qss = QStringLiteral(
            "QToolButton[role=\"accentPreset\"] {"
            "  background: %1; border: 1px solid rgba(0,0,0,40);"
            "  border-radius: 14px; }"
            "QToolButton[role=\"accentPreset\"]:hover {"
            "  border: 2px solid white; }"
        ).arg(accentPresets_[i].name());
        b->setStyleSheet(qss);
        connect(b, &QToolButton::clicked,
                this, &AppearancePanel::onAccentPresetClicked);
        presetRow->addWidget(b);
    }
    accentLayout->addLayout(presetRow);

    auto* customBtn = new QToolButton(accentContainer_);
    customBtn->setText(QStringLiteral("Pick custom…"));
    customBtn->setCursor(Qt::PointingHandCursor);
    customBtn->setProperty("role", QStringLiteral("accentCustom"));
    connect(customBtn, &QToolButton::clicked,
            this, &AppearancePanel::onPickCustomClicked);
    accentLayout->addWidget(customBtn);

    accentClear_ = new QToolButton(accentContainer_);
    accentClear_->setText(QStringLiteral("Reset to default"));
    accentClear_->setCursor(Qt::PointingHandCursor);
    accentClear_->setProperty("role", QStringLiteral("accentCustom"));
    connect(accentClear_, &QToolButton::clicked,
            this, &AppearancePanel::onAccentCleared);
    accentLayout->addWidget(accentClear_);

    accentLayout->addStretch(1);
    root->addWidget(accentContainer_);

    root->addStretch(1);
}

void AppearancePanel::applyTheme() {
    MusicefyColorScheme s;
    if (theme_) s = theme_->scheme();
    if (!s.primary.isValid()) {
        setStyleSheet(QString());
        return;
    }
    setStyleSheet(QStringLiteral(
        "QWidget { background: transparent; color: %1; }"
        "QLabel[role=\"secondary\"] { color: %2; }"
        "QRadioButton { color: %1; spacing: 6px; }"
        "QRadioButton::indicator { width: 16px; height: 16px; }"
        "QToolButton[role=\"accentCustom\"] {"
        "  background: %3; color: %1;"
        "  border: 1px solid %4; border-radius: 6px; padding: 4px 10px; }"
        "QToolButton[role=\"accentCustom\"]:hover { background: %5; }"
        "QLabel[role=\"accentSwatch\"] {"
        "  background: %6; border: 1px solid %4; border-radius: 6px; }"
    )
    .arg(s.onSurface.name())
    .arg(s.onSurfaceVariant.name())
    .arg(s.surfaceContainerHigh.name())
    .arg(s.outlineVariant.name())
    .arg(s.surfaceContainerHighest.name())
    .arg(s.primary.name())
    );
}

void AppearancePanel::onSchemeChanged() {
    applyTheme();
    // Update the accent swatch preview to current primary.
    if (theme_ && accentSwatch_) {
        QString qss = QStringLiteral(
            "QLabel[role=\"accentSwatch\"] {"
            "  background: %1; border: 1px solid %2; border-radius: 6px; }"
        ).arg(theme_->primary().name())
         .arg(QStringLiteral("#888888"));
        accentSwatch_->setStyleSheet(qss);
    }
}

void AppearancePanel::onThemeChanged() {
    syncSwatchSelection();
    syncAccentVisibility();
}

void AppearancePanel::onModeChanged() {
    syncModeSelection();
}

void AppearancePanel::syncModeSelection() {
    if (!theme_) return;
    ThemeMode m = theme_->mode();
    QRadioButton* target = nullptr;
    switch (m) {
    case ThemeMode::System: target = modeSystem_; break;
    case ThemeMode::Light:  target = modeLight_;  break;
    case ThemeMode::Dark:   target = modeDark_;   break;
    case ThemeMode::Amoled: target = modeAmoled_; break;
    }
    if (target) target->setChecked(true);
}

void AppearancePanel::syncSwatchSelection() {
    if (!theme_) return;
    AppTheme current = theme_->theme();
    const auto swatches = findChildren<ThemeSwatch*>();
    for (auto* sw : swatches) {
        sw->setSelected(sw->theme() == current);
    }
}

void AppearancePanel::syncAccentVisibility() {
    if (!theme_ || !accentContainer_) return;
    bool isDynamic = (theme_->theme() == AppTheme::Dynamic);
    accentContainer_->setVisible(isDynamic);
}

void AppearancePanel::onSwatchActivated(int themeId) {
    if (theme_) theme_->setTheme(AppTheme(themeId));
}

void AppearancePanel::onAccentPresetClicked() {
    auto* btn = qobject_cast<QToolButton*>(sender());
    if (!btn || !theme_) return;
    int id = btn->property("accentId").toInt();
    if (id < 0 || id >= accentPresets_.size()) return;
    theme_->setDynamicSeedColor(accentPresets_[id]);
}

void AppearancePanel::onPickCustomClicked() {
    if (!theme_) return;
    QColor initial = theme_->primary();
    QColor c = QColorDialog::getColor(initial, this,
                                       QStringLiteral("Pick accent colour"));
    if (c.isValid()) {
        theme_->setDynamicSeedColor(c);
    }
}

void AppearancePanel::onAccentCleared() {
    if (theme_) theme_->clearDynamicSeedColor();
}

} // namespace mf::app::widgets

#include "AppearancePanel.moc"

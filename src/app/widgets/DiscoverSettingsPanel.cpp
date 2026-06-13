// DiscoverSettingsPanel.cpp
// See header.

#include "DiscoverSettingsPanel.h"

#include "../core/services/SettingsControl.h"
#include "../core/theme/MusicefyColorScheme.h"
#include "../core/theme/ThemeManager.h"

#include <QCheckBox>
#include <QFont>
#include <QHBoxLayout>
#include <QLabel>
#include <QSpinBox>
#include <QVBoxLayout>

namespace mf::app::widgets {

using mf::core::services::SettingsControl;
using mf::core::theme::ThemeManager;
using mf::core::theme::MusicefyColorScheme;

namespace {
constexpr const char* kChartsKey    = "discover/show_charts";
constexpr const char* kMoodsKey     = "discover/show_moods";
constexpr const char* kNewRelKey    = "discover/show_new_releases";
constexpr const char* kPlaylistsKey = "discover/show_playlists";
constexpr const char* kAutoKey      = "discover/auto_refresh";
constexpr const char* kIntervalKey  = "discover/refresh_hours";

constexpr int kIntervalMin     = 1;
constexpr int kIntervalMax     = 24;
constexpr int kIntervalDefault = 6;
} // anonymous namespace

DiscoverSettingsPanel::DiscoverSettingsPanel(SettingsControl* settings,
                                             ThemeManager*    theme,
                                             QWidget*         parent)
    : QWidget(parent)
    , settings_(settings)
    , theme_(theme)
{
    buildUi();
    applyTheme();
    loadFromSettings();

    if (theme_) {
        connect(theme_, &ThemeManager::schemeChanged,
                this, [this]() { applyTheme(); });
    }
}

void DiscoverSettingsPanel::buildUi() {
    auto* root = new QVBoxLayout(this);
    root->setContentsMargins(32, 28, 32, 28);
    root->setSpacing(14);

    auto* title = new QLabel(QStringLiteral("Discover"), this);
    QFont tf = title->font();
    tf.setPointSize(18);
    tf.setBold(true);
    title->setFont(tf);
    root->addWidget(title);

    auto* blurb = new QLabel(
        QStringLiteral("Choose which home-feed sections to show and how often "
                       "they refresh."),
        this);
    blurb->setWordWrap(true);
    blurb->setProperty("role", QStringLiteral("secondary"));
    root->addWidget(blurb);

    // ── Home feed sections ───────────────────────────────────────
    auto* sectionsHeader = new QLabel(QStringLiteral("Home feed sections"), this);
    QFont shf = sectionsHeader->font();
    shf.setPointSize(12);
    shf.setBold(true);
    sectionsHeader->setFont(shf);
    root->addWidget(sectionsHeader);

    auto* sectionsBox = new QWidget(this);
    auto* sectionsLayout = new QVBoxLayout(sectionsBox);
    sectionsLayout->setContentsMargins(0, 0, 0, 0);
    sectionsLayout->setSpacing(6);

    auto makeSection = [this, sectionsLayout, sectionsBox](const QString& label) {
        auto* cb = new QCheckBox(label, sectionsBox);
        cb->setCursor(Qt::PointingHandCursor);
        connect(cb, &QCheckBox::toggled,
                this, &DiscoverSettingsPanel::onSectionToggled);
        sectionsLayout->addWidget(cb);
        return cb;
    };
    charts_    = makeSection(QStringLiteral("Charts"));
    moods_     = makeSection(QStringLiteral("Moods & Genres"));
    newRel_    = makeSection(QStringLiteral("New Releases"));
    playlists_ = makeSection(QStringLiteral("Playlists"));
    root->addWidget(sectionsBox);

    // ── Auto-refresh ─────────────────────────────────────────────
    auto* refreshHeader = new QLabel(QStringLiteral("Auto-refresh"), this);
    QFont rhf = refreshHeader->font();
    rhf.setPointSize(12);
    rhf.setBold(true);
    refreshHeader->setFont(rhf);
    root->addWidget(refreshHeader);

    auto* refreshRow = new QHBoxLayout();
    refreshRow->setSpacing(8);
    autoRefresh_ = new QCheckBox(QStringLiteral("Refresh every"), this);
    autoRefresh_->setCursor(Qt::PointingHandCursor);
    connect(autoRefresh_, &QCheckBox::stateChanged,
            this, &DiscoverSettingsPanel::onAutoRefreshToggled);
    refreshRow->addWidget(autoRefresh_);

    interval_ = new QSpinBox(this);
    interval_->setRange(kIntervalMin, kIntervalMax);
    interval_->setSuffix(QStringLiteral(" h"));
    interval_->setValue(kIntervalDefault);
    connect(interval_, QOverload<int>::of(&QSpinBox::valueChanged),
            this, &DiscoverSettingsPanel::onIntervalChanged);
    refreshRow->addWidget(interval_);
    refreshRow->addStretch(1);
    root->addLayout(refreshRow);

    root->addStretch(1);
}

void DiscoverSettingsPanel::applyTheme() {
    MusicefyColorScheme s;
    if (theme_) s = theme_->scheme();
    if (!s.primary.isValid()) {
        setStyleSheet(QString());
        return;
    }
    setStyleSheet(QStringLiteral(
        "QWidget { background: transparent; color: %1; }"
        "QLabel[role=\"secondary\"] { color: %2; }"
        "QCheckBox { color: %1; spacing: 8px; }"
        "QSpinBox { background: %3; color: %1;"
        "  border: 1px solid %4; border-radius: 6px; padding: 4px 8px; }"
    )
    .arg(s.onSurface.name())
    .arg(s.onSurfaceVariant.name())
    .arg(s.surfaceContainerHigh.name())
    .arg(s.outlineVariant.name())
    );
}

void DiscoverSettingsPanel::loadFromSettings() {
    if (!settings_) {
        // Sensible defaults when settings are unavailable.
        charts_->setChecked(true);
        moods_->setChecked(true);
        newRel_->setChecked(true);
        playlists_->setChecked(true);
        autoRefresh_->setChecked(false);
        interval_->setValue(kIntervalDefault);
        interval_->setEnabled(false);
        return;
    }

    charts_->setChecked(settings_->getOrDefault<bool>(QString::fromLatin1(kChartsKey),    true));
    moods_->setChecked( settings_->getOrDefault<bool>(QString::fromLatin1(kMoodsKey),     true));
    newRel_->setChecked(settings_->getOrDefault<bool>(QString::fromLatin1(kNewRelKey),    true));
    playlists_->setChecked(settings_->getOrDefault<bool>(QString::fromLatin1(kPlaylistsKey), true));

    bool autoOn = settings_->getOrDefault<bool>(QString::fromLatin1(kAutoKey), false);
    autoRefresh_->setChecked(autoOn);
    interval_->setEnabled(autoOn);
    int hours = settings_->getOrDefault<int>(QString::fromLatin1(kIntervalKey), kIntervalDefault);
    if (hours < kIntervalMin) hours = kIntervalMin;
    if (hours > kIntervalMax) hours = kIntervalMax;
    interval_->setValue(hours);
}

void DiscoverSettingsPanel::onSectionToggled() {
    auto* cb = qobject_cast<QCheckBox*>(sender());
    if (!cb || !settings_) return;
    QString key;
    if      (cb == charts_)    key = QString::fromLatin1(kChartsKey);
    else if (cb == moods_)     key = QString::fromLatin1(kMoodsKey);
    else if (cb == newRel_)    key = QString::fromLatin1(kNewRelKey);
    else if (cb == playlists_) key = QString::fromLatin1(kPlaylistsKey);
    else return;
    persistSection(key, cb->isChecked());
}

void DiscoverSettingsPanel::persistSection(const QString& key, bool on) {
    if (!settings_) return;
    settings_->set(key, on);
    settings_->sync();
}

void DiscoverSettingsPanel::onAutoRefreshToggled(int state) {
    if (interval_) interval_->setEnabled(state == Qt::Checked);
    if (settings_) {
        settings_->set(QString::fromLatin1(kAutoKey), state == Qt::Checked);
        settings_->sync();
    }
}

void DiscoverSettingsPanel::onIntervalChanged(int v) {
    if (!settings_) return;
    settings_->set(QString::fromLatin1(kIntervalKey), v);
    settings_->sync();
}

} // namespace mf::app::widgets

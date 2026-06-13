// RepositoriesSettingsPanel.cpp
// See header.

#include "RepositoriesSettingsPanel.h"

#include "../core/services/SettingsControl.h"
#include "../core/theme/MusicefyColorScheme.h"
#include "../core/theme/ThemeManager.h"

#include <QCheckBox>
#include <QFont>
#include <QHBoxLayout>
#include <QInputDialog>
#include <QLabel>
#include <QLineEdit>
#include <QListWidget>
#include <QPushButton>
#include <QSpinBox>
#include <QVBoxLayout>

namespace mf::app::widgets {

using mf::core::services::SettingsControl;
using mf::core::theme::ThemeManager;
using mf::core::theme::MusicefyColorScheme;

namespace {
constexpr const char* kReposKey     = "extensions/repos";
constexpr const char* kAutoKey      = "extensions/auto_refresh";
constexpr const char* kIntervalKey  = "extensions/refresh_hours";
constexpr const char* kSignedKey    = "extensions/require_signed";

constexpr int kIntervalMin     = 1;
constexpr int kIntervalMax     = 168;  // one week
constexpr int kIntervalDefault = 12;
} // anonymous namespace

RepositoriesSettingsPanel::RepositoriesSettingsPanel(SettingsControl* settings,
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

void RepositoriesSettingsPanel::buildUi() {
    auto* root = new QVBoxLayout(this);
    root->setContentsMargins(32, 28, 32, 28);
    root->setSpacing(14);

    auto* title = new QLabel(QStringLiteral("Repositories"), this);
    QFont tf = title->font();
    tf.setPointSize(18);
    tf.setBold(true);
    title->setFont(tf);
    root->addWidget(title);

    auto* blurb = new QLabel(
        QStringLiteral("Manage extension plugin repository URLs and how "
                       "often they're polled for new content."),
        this);
    blurb->setWordWrap(true);
    blurb->setProperty("role", QStringLiteral("secondary"));
    root->addWidget(blurb);

    // ── URLs ─────────────────────────────────────────────────────
    auto* urlsHeader = new QLabel(QStringLiteral("Repository URLs"), this);
    QFont uhf = urlsHeader->font();
    uhf.setPointSize(12);
    uhf.setBold(true);
    urlsHeader->setFont(uhf);
    root->addWidget(urlsHeader);

    urlList_ = new QListWidget(this);
    urlList_->setSelectionMode(QAbstractItemView::SingleSelection);
    urlList_->setUniformItemSizes(true);
    urlList_->setAlternatingRowColors(true);
    root->addWidget(urlList_, /*stretch=*/1);

    auto* btnRow = new QHBoxLayout();
    btnRow->setSpacing(8);
    addBtn_ = new QPushButton(QStringLiteral("Add…"), this);
    addBtn_->setCursor(Qt::PointingHandCursor);
    connect(addBtn_, &QPushButton::clicked,
            this, &RepositoriesSettingsPanel::onAddClicked);
    btnRow->addWidget(addBtn_);

    removeBtn_ = new QPushButton(QStringLiteral("Remove selected"), this);
    removeBtn_->setCursor(Qt::PointingHandCursor);
    removeBtn_->setProperty("role", QStringLiteral("secondaryButton"));
    connect(removeBtn_, &QPushButton::clicked,
            this, &RepositoriesSettingsPanel::onRemoveClicked);
    btnRow->addWidget(removeBtn_);
    btnRow->addStretch(1);
    root->addLayout(btnRow);

    // ── Refresh ──────────────────────────────────────────────────
    auto* refreshHeader = new QLabel(QStringLiteral("Refresh"), this);
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
            this, &RepositoriesSettingsPanel::onAutoRefreshToggled);
    refreshRow->addWidget(autoRefresh_);

    interval_ = new QSpinBox(this);
    interval_->setRange(kIntervalMin, kIntervalMax);
    interval_->setSuffix(QStringLiteral(" h"));
    interval_->setValue(kIntervalDefault);
    connect(interval_, QOverload<int>::of(&QSpinBox::valueChanged),
            this, &RepositoriesSettingsPanel::onIntervalChanged);
    refreshRow->addWidget(interval_);
    refreshRow->addStretch(1);
    root->addLayout(refreshRow);

    // ── Security ─────────────────────────────────────────────────
    auto* secHeader = new QLabel(QStringLiteral("Security"), this);
    QFont shf = secHeader->font();
    shf.setPointSize(12);
    shf.setBold(true);
    secHeader->setFont(shf);
    root->addWidget(secHeader);

    requireSigned_ = new QCheckBox(
        QStringLiteral("Require signed extensions"), this);
    requireSigned_->setCursor(Qt::PointingHandCursor);
    connect(requireSigned_, &QCheckBox::stateChanged,
            this, &RepositoriesSettingsPanel::onRequireSignedToggled);
    root->addWidget(requireSigned_);
}

void RepositoriesSettingsPanel::applyTheme() {
    MusicefyColorScheme s;
    if (theme_) s = theme_->scheme();
    if (!s.primary.isValid()) {
        setStyleSheet(QString());
        return;
    }
    setStyleSheet(QStringLiteral(
        "QWidget { background: transparent; color: %1; }"
        "QLabel[role=\"secondary\"] { color: %2; }"
        "QListWidget { background: %3; color: %1;"
        "  border: 1px solid %4; border-radius: 6px;"
        "  selection-background-color: %5; selection-color: %6;"
        "  alternate-background-color: %7; }"
        "QPushButton { background: %8; color: %1;"
        "  border: 1px solid %4; border-radius: 6px; padding: 6px 14px; }"
        "QPushButton:hover { background: %9; }"
        "QPushButton[role=\"secondaryButton\"] { background: %3; }"
        "QPushButton[role=\"secondaryButton\"]:hover { background: %9; }"
        "QCheckBox { color: %1; spacing: 8px; }"
        "QSpinBox { background: %3; color: %1;"
        "  border: 1px solid %4; border-radius: 6px; padding: 4px 8px; }"
    )
    .arg(s.onSurface.name())
    .arg(s.onSurfaceVariant.name())
    .arg(s.surfaceContainerHigh.name())
    .arg(s.outlineVariant.name())
    .arg(s.primaryContainer.name())
    .arg(s.onPrimaryContainer.name())
    .arg(s.surfaceContainer.name())
    .arg(s.primaryContainer.name())
    .arg(s.surfaceContainerHighest.name())
    );
}

void RepositoriesSettingsPanel::loadFromSettings() {
    if (!settings_) {
        urlList_->addItem(QStringLiteral("https://github.com/MarBou/Musicefy-extensions"));
        return;
    }

    QStringList repos = settings_->getOrDefault<QStringList>(
        QString::fromLatin1(kReposKey), QStringList{});
    if (repos.isEmpty()) {
        // Default repository shipped with the app.
        repos << QStringLiteral("https://github.com/MarBou/Musicefy-extensions");
        settings_->set(QString::fromLatin1(kReposKey), repos);
        settings_->sync();
    }
    urlList_->clear();
    urlList_->addItems(repos);

    bool autoOn = settings_->getOrDefault<bool>(QString::fromLatin1(kAutoKey), true);
    autoRefresh_->setChecked(autoOn);
    interval_->setEnabled(autoOn);
    int hours = settings_->getOrDefault<int>(QString::fromLatin1(kIntervalKey), kIntervalDefault);
    if (hours < kIntervalMin) hours = kIntervalMin;
    if (hours > kIntervalMax) hours = kIntervalMax;
    interval_->setValue(hours);

    bool signedRequired = settings_->getOrDefault<bool>(QString::fromLatin1(kSignedKey), true);
    requireSigned_->setChecked(signedRequired);
}

void RepositoriesSettingsPanel::onAddClicked() {
    bool ok = false;
    QString url = QInputDialog::getText(
        this,
        QStringLiteral("Add repository"),
        QStringLiteral("Repository URL:"),
        QLineEdit::Normal,
        QStringLiteral("https://"),
        &ok);
    if (!ok) return;
    url = url.trimmed();
    if (url.isEmpty()) return;

    // Basic sanity: must look like http(s)://…
    if (!url.startsWith(QStringLiteral("http://"),  Qt::CaseInsensitive) &&
        !url.startsWith(QStringLiteral("https://"), Qt::CaseInsensitive)) {
        return;
    }

    // De-duplicate.
    for (int i = 0; i < urlList_->count(); ++i) {
        if (urlList_->item(i)->text().compare(url, Qt::CaseInsensitive) == 0) {
            return;
        }
    }
    urlList_->addItem(url);
    persistRepos();
}

void RepositoriesSettingsPanel::onRemoveClicked() {
    int row = urlList_->currentRow();
    if (row < 0) return;
    delete urlList_->takeItem(row);
    persistRepos();
}

void RepositoriesSettingsPanel::onAutoRefreshToggled(int state) {
    if (interval_) interval_->setEnabled(state == Qt::Checked);
    if (settings_) {
        settings_->set(QString::fromLatin1(kAutoKey), state == Qt::Checked);
        settings_->sync();
    }
}

void RepositoriesSettingsPanel::onIntervalChanged(int v) {
    if (!settings_) return;
    settings_->set(QString::fromLatin1(kIntervalKey), v);
    settings_->sync();
}

void RepositoriesSettingsPanel::onRequireSignedToggled(int state) {
    Q_UNUSED(state);
    persistSignatureRequired();
}

void RepositoriesSettingsPanel::persistRepos() {
    if (!settings_) return;
    QStringList repos;
    for (int i = 0; i < urlList_->count(); ++i) {
        repos << urlList_->item(i)->text();
    }
    settings_->set(QString::fromLatin1(kReposKey), repos);
    settings_->sync();
}

void RepositoriesSettingsPanel::persistSignatureRequired() {
    if (!settings_ || !requireSigned_) return;
    settings_->set(QString::fromLatin1(kSignedKey), requireSigned_->isChecked());
    settings_->sync();
}

} // namespace mf::app::widgets

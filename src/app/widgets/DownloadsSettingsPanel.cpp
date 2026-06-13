// DownloadsSettingsPanel.cpp
// See header.

#include "DownloadsSettingsPanel.h"

#include "../core/services/DownloadService.h"
#include "../core/services/SettingsControl.h"
#include "../core/theme/MusicefyColorScheme.h"
#include "../core/theme/ThemeManager.h"

#include <QDir>
#include <QFileDialog>
#include <QFont>
#include <QHBoxLayout>
#include <QLabel>
#include <QPushButton>
#include <QSpinBox>
#include <QTimer>
#include <QVBoxLayout>

namespace mf::app::widgets {

using mf::core::services::DownloadService;
using mf::core::services::SettingsControl;
using mf::core::theme::ThemeManager;
using mf::core::theme::MusicefyColorScheme;

namespace {
constexpr const char* kDirKey        = "downloads/dir";
constexpr const char* kParallelKey   = "downloads/parallel_limit";
constexpr int         kParallelMin   = 1;
constexpr int         kParallelMax   = 8;
constexpr int         kParallelDefault = 3;
} // anonymous namespace

DownloadsSettingsPanel::DownloadsSettingsPanel(DownloadService* downloads,
                                               SettingsControl*  settings,
                                               ThemeManager*    theme,
                                               QWidget*         parent)
    : QWidget(parent)
    , downloads_(downloads)
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

    // 1 Hz poll for the active-downloads count. Cheap (single int
    // read from a QHash size), avoids having to wire progressQ /
    // completionQ all the way through to the UI.
    pollTimer_ = new QTimer(this);
    pollTimer_->setInterval(1000);
    connect(pollTimer_, &QTimer::timeout,
            this, &DownloadsSettingsPanel::onStatusPoll);
    pollTimer_->start();
    onStatusPoll();
}

void DownloadsSettingsPanel::buildUi() {
    auto* root = new QVBoxLayout(this);
    root->setContentsMargins(32, 28, 32, 28);
    root->setSpacing(14);

    auto* title = new QLabel(QStringLiteral("Downloads"), this);
    QFont tf = title->font();
    tf.setPointSize(18);
    tf.setBold(true);
    title->setFont(tf);
    root->addWidget(title);

    auto* blurb = new QLabel(
        QStringLiteral("Configure where cached tracks are stored and how "
                       "many concurrent downloads are allowed."),
        this);
    blurb->setWordWrap(true);
    blurb->setProperty("role", QStringLiteral("secondary"));
    root->addWidget(blurb);

    // ── Download directory ───────────────────────────────────────
    auto* dirHeader = new QLabel(QStringLiteral("Download directory"), this);
    QFont dhf = dirHeader->font();
    dhf.setPointSize(12);
    dhf.setBold(true);
    dirHeader->setFont(dhf);
    root->addWidget(dirHeader);

    auto* dirRow = new QHBoxLayout();
    dirRow->setSpacing(8);

    dirPath_ = new QLabel(this);
    dirPath_->setTextInteractionFlags(Qt::TextSelectableByMouse);
    dirPath_->setProperty("role", QStringLiteral("pathDisplay"));
    dirPath_->setWordWrap(true);
    dirRow->addWidget(dirPath_, /*stretch=*/1);

    dirChange_ = new QPushButton(QStringLiteral("Change…"), this);
    dirChange_->setCursor(Qt::PointingHandCursor);
    connect(dirChange_, &QPushButton::clicked,
            this, &DownloadsSettingsPanel::onChangeDirClicked);
    dirRow->addWidget(dirChange_);

    dirReset_ = new QPushButton(QStringLiteral("Reset"), this);
    dirReset_->setCursor(Qt::PointingHandCursor);
    dirReset_->setProperty("role", QStringLiteral("secondaryButton"));
    connect(dirReset_, &QPushButton::clicked,
            this, &DownloadsSettingsPanel::onResetDirClicked);
    dirRow->addWidget(dirReset_);

    root->addLayout(dirRow);

    // ── Parallel downloads ──────────────────────────────────────
    auto* parHeader = new QLabel(QStringLiteral("Parallel downloads"), this);
    QFont phf = parHeader->font();
    phf.setPointSize(12);
    phf.setBold(true);
    parHeader->setFont(phf);
    root->addWidget(parHeader);

    auto* parRow = new QHBoxLayout();
    parRow->setSpacing(8);
    parallelBox_ = new QSpinBox(this);
    parallelBox_->setRange(kParallelMin, kParallelMax);
    parallelBox_->setSuffix(QStringLiteral("  (1–8)"));
    parallelBox_->setValue(kParallelDefault);
    connect(parallelBox_, QOverload<int>::of(&QSpinBox::valueChanged),
            this, &DownloadsSettingsPanel::onParallelChanged);
    parRow->addWidget(parallelBox_);
    parRow->addStretch(1);
    root->addLayout(parRow);

    // ── Status ──────────────────────────────────────────────────
    auto* statHeader = new QLabel(QStringLiteral("Status"), this);
    QFont shf = statHeader->font();
    shf.setPointSize(12);
    shf.setBold(true);
    statHeader->setFont(shf);
    root->addWidget(statHeader);

    activeLabel_    = new QLabel(this);
    completedLabel_ = new QLabel(this);
    defaultLabel_   = new QLabel(this);
    for (auto* l : { activeLabel_, completedLabel_, defaultLabel_ }) {
        l->setProperty("role", QStringLiteral("statusLine"));
    }
    root->addWidget(activeLabel_);
    root->addWidget(completedLabel_);
    root->addWidget(defaultLabel_);

    root->addStretch(1);
}

void DownloadsSettingsPanel::applyTheme() {
    MusicefyColorScheme s;
    if (theme_) s = theme_->scheme();
    if (!s.primary.isValid()) {
        setStyleSheet(QString());
        return;
    }
    setStyleSheet(QStringLiteral(
        "QWidget { background: transparent; color: %1; }"
        "QLabel[role=\"secondary\"] { color: %2; }"
        "QLabel[role=\"pathDisplay\"] {"
        "  background: %3; color: %1;"
        "  border: 1px solid %4; border-radius: 6px;"
        "  padding: 6px 10px; }"
        "QLabel[role=\"statusLine\"] { color: %1; }"
        "QPushButton { background: %5; color: %1;"
        "  border: 1px solid %4; border-radius: 6px; padding: 6px 14px; }"
        "QPushButton:hover { background: %6; }"
        "QPushButton[role=\"secondaryButton\"] { background: %3; }"
        "QPushButton[role=\"secondaryButton\"]:hover { background: %6; }"
        "QSpinBox { background: %3; color: %1;"
        "  border: 1px solid %4; border-radius: 6px; padding: 4px 8px; }"
    )
    .arg(s.onSurface.name())
    .arg(s.onSurfaceVariant.name())
    .arg(s.surfaceContainerHigh.name())
    .arg(s.outlineVariant.name())
    .arg(s.primaryContainer.name())
    .arg(s.surfaceContainerHighest.name())
    );
}

void DownloadsSettingsPanel::loadFromSettings() {
    // Download directory: prefer the service's current value, then
    // the persisted setting, then the system default.
    QString dir;
    if (downloads_) {
        dir = downloads_->downloadDir();
    }
    if (dir.isEmpty() && settings_) {
        dir = settings_->getOrDefault<QString>(QString::fromLatin1(kDirKey), QString());
    }
    if (dir.isEmpty()) {
        dir = DownloadService::defaultDownloadDir();
    }
    if (downloads_) downloads_->setDownloadDir(dir);
    if (dirPath_) dirPath_->setText(QDir::toNativeSeparators(dir));

    int parallel = kParallelDefault;
    if (settings_) {
        parallel = settings_->getOrDefault<int>(QString::fromLatin1(kParallelKey),
                                                kParallelDefault);
    }
    if (parallel < kParallelMin) parallel = kParallelMin;
    if (parallel > kParallelMax) parallel = kParallelMax;
    if (parallelBox_) parallelBox_->setValue(parallel);

    if (defaultLabel_) {
        defaultLabel_->setText(
            QStringLiteral("Default location: %1")
            .arg(QDir::toNativeSeparators(DownloadService::defaultDownloadDir())));
    }
}

void DownloadsSettingsPanel::onChangeDirClicked() {
    QString start = downloads_ ? downloads_->downloadDir()
                               : DownloadService::defaultDownloadDir();
    QString chosen = QFileDialog::getExistingDirectory(
        this, QStringLiteral("Choose download folder"), start);
    if (chosen.isEmpty()) return;

    if (downloads_) downloads_->setDownloadDir(chosen);
    if (settings_)  settings_->set(QString::fromLatin1(kDirKey), chosen);
    if (settings_)  settings_->sync();
    if (dirPath_)   dirPath_->setText(QDir::toNativeSeparators(chosen));
}

void DownloadsSettingsPanel::onResetDirClicked() {
    QString d = DownloadService::defaultDownloadDir();
    if (downloads_) downloads_->setDownloadDir(d);
    if (settings_)  settings_->set(QString::fromLatin1(kDirKey), d);
    if (settings_)  settings_->sync();
    if (dirPath_)   dirPath_->setText(QDir::toNativeSeparators(d));
}

void DownloadsSettingsPanel::onParallelChanged(int v) {
    Q_UNUSED(v);
    persistParallel();
}

void DownloadsSettingsPanel::persistParallel() {
    if (!settings_ || !parallelBox_) return;
    settings_->set(QString::fromLatin1(kParallelKey), parallelBox_->value());
    settings_->sync();
}

void DownloadsSettingsPanel::onStatusPoll() {
    if (activeLabel_) {
        int n = downloads_ ? downloads_->activeCount() : 0;
        activeLabel_->setText(
            QStringLiteral("Active downloads: %1").arg(n));
    }
    if (completedLabel_) {
        int n = downloads_ ? downloads_->completedCount() : 0;
        completedLabel_->setText(
            QStringLiteral("Completed downloads: %1").arg(n));
    }
}

} // namespace mf::app::widgets

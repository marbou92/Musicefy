// SettingsPage.cpp
// See header.

#include "SettingsPage.h"

#include "AppearancePanel.h"
#include "DiscoverSettingsPanel.h"
#include "DownloadsSettingsPanel.h"
#include "ExtensionsSettingsPanel.h"
#include "LibrarySettingsPanel.h"
#include "RepositoriesSettingsPanel.h"
#include "SourcesSettingsPanel.h"

#include "../core/services/DownloadService.h"
#include "../core/services/ExtensionManager.h"
#include "../core/services/LibraryService.h"
#include "../core/services/SettingsControl.h"
#include "../core/services/ToastService.h"
#include "../core/sources/StreamingSourceManager.h"
#include "../core/theme/MusicefyColorScheme.h"
#include "../core/theme/ThemeManager.h"

#include <QFont>
#include <QHBoxLayout>
#include <QLabel>
#include <QListWidget>
#include <QStackedWidget>
#include <QVBoxLayout>

namespace mf::app::widgets {

using mf::core::services::DownloadService;
using mf::core::services::ExtensionManager;
using mf::core::services::LibraryService;
using mf::core::services::SettingsControl;
using mf::core::services::ToastService;
using mf::core::sources::StreamingSourceManager;
using mf::core::theme::ThemeManager;
using mf::core::theme::MusicefyColorScheme;

SettingsPage::SettingsPage(LibraryService*        libSvc,
                           ToastService*          toasts,
                           DownloadService*       downloads,
                           ExtensionManager*      extMgr,
                           StreamingSourceManager* sourceMgr,
                           SettingsControl*       settings,
                           ThemeManager*          theme,
                           QWidget*               parent)
    : QWidget(parent)
    , libSvc_(libSvc)
    , toasts_(toasts)
    , downloads_(downloads)
    , extMgr_(extMgr)
    , sourceMgr_(sourceMgr)
    , settings_(settings)
    , theme_(theme)
{
    buildUi();
    applyTheme();

    if (theme_) {
        connect(theme_, &ThemeManager::schemeChanged,
                this, &SettingsPage::applyTheme);
    }
    if (settings_ && toasts_) {
        // Debounce 400ms: a burst of slider/keyboard tweaks to the
        // same setting collapses into a single "Saved" toast.
        toastTimer_.setSingleShot(true);
        toastTimer_.setInterval(400);
        connect(&toastTimer_, &QTimer::timeout,
                this, &SettingsPage::flushPendingToast);
        connect(settings_, &SettingsControl::settingChanged,
                this, &SettingsPage::onSettingChanged);
    }
}

void SettingsPage::buildUi() {
    auto* root = new QHBoxLayout(this);
    root->setContentsMargins(0, 0, 0, 0);
    root->setSpacing(0);

    // ── Left sub-nav ──────────────────────────────────────────────
    nav_ = new QListWidget(this);
    nav_->setSelectionMode(QAbstractItemView::SingleSelection);
    nav_->setFocusPolicy(Qt::NoFocus);
    nav_->setUniformItemSizes(true);
    nav_->setHorizontalScrollBarPolicy(Qt::ScrollBarAlwaysOff);
    nav_->setFrameShape(QFrame::NoFrame);
    nav_->setSpacing(2);

    auto add = [this](const QString& label) {
        auto* item = new QListWidgetItem(nav_);
        item->setText(label);
        item->setSizeHint(QSize(0, 40));
        nav_->addItem(item);
    };
    add(QStringLiteral("Library / Folders"));
    add(QStringLiteral("Appearance"));
    add(QStringLiteral("Sources"));
    add(QStringLiteral("Downloads"));
    add(QStringLiteral("Discover"));
    add(QStringLiteral("Extensions"));
    add(QStringLiteral("Repositories"));

    // ── Right side: QStackedWidget of panels ──────────────────────
    pages_ = new QStackedWidget(this);

    if (libSvc_ && toasts_ && theme_) {
        foldersPanel_ = new LibrarySettingsPanel(libSvc_, toasts_, theme_, pages_);
        pages_->addWidget(foldersPanel_); // index 0
    } else {
        auto* placeholder = new QLabel(
            QStringLiteral("(library service not available)"), pages_);
        placeholder->setAlignment(Qt::AlignCenter);
        pages_->addWidget(placeholder);
    }

    // Appearance panel — real (Block 4.2).
    if (theme_) {
        appearancePanel_ = new AppearancePanel(theme_, pages_);
        pages_->addWidget(appearancePanel_); // index 1
    } else {
        buildStubPanel(QStringLiteral("Appearance"),
            QStringLiteral("Theme picker (Light / Dark / Amoled), accent colour, "
                           "now-playing overlay, mini-player position, font size.\n"
                           "Backed by ThemeManager which is already in the DI graph."),
            pages_->count());
    }

    // Sources — real (Block 4.7).
    if (sourceMgr_ && theme_) {
        sourcesPanel_ = new SourcesSettingsPanel(sourceMgr_, theme_, pages_);
        pages_->addWidget(sourcesPanel_); // index 2
    } else {
        buildStubPanel(QStringLiteral("Sources"),
            QStringLiteral("Subsonic / YouTube account configuration. "
                           "StreamingSourceManager + the four providers are already wired."),
            pages_->count());
    }

    // Downloads panel — real (Block 4.3).
    if (downloads_ && settings_ && theme_) {
        downloadsPanel_ = new DownloadsSettingsPanel(
            downloads_, settings_, theme_, pages_);
        pages_->addWidget(downloadsPanel_); // index 3
    } else {
        buildStubPanel(QStringLiteral("Downloads"),
            QStringLiteral("Download path, format preferences, quota, parallel-job limit. "
                           "DownloadService is already wired; the UI lands in Block 4.3."),
            pages_->count());
    }

    // Discover panel — real (Block 4.4).
    if (settings_ && theme_) {
        discoverPanel_ = new DiscoverSettingsPanel(settings_, theme_, pages_);
        pages_->addWidget(discoverPanel_); // index 4
    } else {
        buildStubPanel(QStringLiteral("Discover"),
            QStringLiteral("Recommendations, auto-playlists, browse-section sources. "
                           "BrowseService is already wired; the editor lands in Block 4.4."),
            pages_->count());
    }

    // Extensions panel — real (Block 4.5).
    if (extMgr_ && settings_ && theme_) {
        extensionsPanel_ = new ExtensionsSettingsPanel(
            extMgr_, settings_, theme_, pages_);
        pages_->addWidget(extensionsPanel_); // index 5
    } else {
        buildStubPanel(QStringLiteral("Extensions"),
            QStringLiteral("Loaded extensions list, per-extension settings, "
                           "load-on-startup, hot-reload. "
                           "ExtensionManager is already wired; the UI lands in Block 4.5."),
            pages_->count());
    }

    // Repositories — real (Block 4.6).
    if (settings_ && theme_) {
        repositoriesPanel_ = new RepositoriesSettingsPanel(
            settings_, theme_, pages_);
        pages_->addWidget(repositoriesPanel_); // index 6
    } else {
        buildStubPanel(QStringLiteral("Repositories"),
            QStringLiteral("Extension repository URLs, refresh interval, signature "
                           "verification. Lands in Block 4.6."),
            pages_->count());
    }

    connect(nav_, &QListWidget::currentRowChanged,
            pages_, &QStackedWidget::setCurrentIndex);

    root->addWidget(nav_, /*stretch=*/0);
    root->addWidget(pages_, /*stretch=*/1);
}

void SettingsPage::buildStubPanel(const QString& name,
                                  const QString& blurb,
                                  int            index) {
    Q_UNUSED(index);
    auto* panel = new QWidget(pages_);
    auto* layout = new QVBoxLayout(panel);
    layout->setContentsMargins(32, 28, 32, 28);
    layout->setSpacing(8);

    auto* title = new QLabel(name, panel);
    QFont tf = title->font();
    tf.setPointSize(18);
    tf.setBold(true);
    title->setFont(tf);
    layout->addWidget(title);

    auto* body = new QLabel(blurb, panel);
    body->setWordWrap(true);
    body->setProperty("role", QStringLiteral("secondary"));
    layout->addWidget(body);

    layout->addStretch(1);
    pages_->addWidget(panel);
}

void SettingsPage::applyTheme() {
    MusicefyColorScheme s;
    if (theme_) s = theme_->scheme();
    if (!s.primary.isValid()) {
        setStyleSheet(QString());
        return;
    }
    setStyleSheet(QStringLiteral(
        "QWidget { background: transparent; color: %1; }"
        "QListWidget { background: %2; border-right: 1px solid %3;"
        "  color: %1; padding: 12px 0; outline: 0; }"
        "QListWidget::item { padding: 10px 16px; border: none;"
        "  border-radius: 6px; margin: 1px 8px; }"
        "QListWidget::item:hover { background: %4; }"
        "QListWidget::item:selected { background: %5; color: %6; }"
        "QLabel[role=\"secondary\"] { color: %7; }"
    )
    .arg(s.onSurface.name())
    .arg(s.surfaceContainer.name())
    .arg(s.outlineVariant.name())
    .arg(s.surfaceContainerHigh.name())
    .arg(s.primaryContainer.name())
    .arg(s.onPrimaryContainer.name())
    .arg(s.onSurfaceVariant.name())
    );
}

QString SettingsPage::prettyKey(const QString& key) const {
    // Strip any leading "category/" prefix and humanise the rest:
    //   "appearance/mode"        -> "Mode"
    //   "library/scan_interval"  -> "Scan interval"
    //   "yt-dlp/timeout_seconds" -> "Timeout seconds"
    int slash = key.lastIndexOf(QLatin1Char('/'));
    QString tail = (slash >= 0) ? key.mid(slash + 1) : key;
    tail = tail.replace(QLatin1Char('_'), QLatin1Char(' '));
    if (tail.isEmpty()) return key;
    tail[0] = tail[0].toUpper();
    return tail;
}

void SettingsPage::onSettingChanged(QString key, QVariant value) {
    if (!toasts_ || key.isEmpty()) return;
    // Coalesce: every change within the debounce window collapses
    // into a single toast whose body is the LAST changed value.
    pendingKey_ = key;
    if (!toastTimer_.isActive()) {
        toastTimer_.start();
    }
    // Stash the latest value so flushPendingToast() can include it
    // (or, more importantly, dedup it).
    lastShownValue_[key] = value.toString();
}

void SettingsPage::flushPendingToast() {
    if (!toasts_ || pendingKey_.isEmpty()) return;
    const QString key   = pendingKey_;
    const QString value = lastShownValue_.value(key);
    pendingKey_.clear();

    // Dedup: skip the toast if the value hasn't actually changed
    // from the last one we showed for this key. (The QHash always
    // holds the latest value, so we need a separate "lastShown"
    // tracker to detect "no change since last toast".)
    static QHash<QString, QString> lastShown;
    if (lastShown.value(key) == value) {
        return;
    }
    lastShown.insert(key, value);

    if (value.isEmpty()) {
        toasts_->showInfo(QStringLiteral("Saved"), prettyKey(key));
    } else {
        toasts_->showInfo(QStringLiteral("Saved"),
                          prettyKey(key) + QStringLiteral(": ") + value);
    }
}

} // namespace mf::app::widgets

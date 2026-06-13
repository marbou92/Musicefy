// ExtensionsSettingsPanel.cpp
// See header.

#include "ExtensionsSettingsPanel.h"

#include "../core/models/ExtensionManifest.h"
#include "../core/services/ExtensionManager.h"
#include "../core/services/SettingsControl.h"
#include "../core/theme/MusicefyColorScheme.h"
#include "../core/theme/ThemeManager.h"

#include <QDir>
#include <QFileDialog>
#include <QFont>
#include <QHBoxLayout>
#include <QLabel>
#include <QListView>
#include <QPushButton>
#include <QStandardItem>
#include <QStandardItemModel>
#include <QVBoxLayout>

namespace mf::app::widgets {

using mf::core::models::ExtensionManifest;
using mf::core::services::ExtensionManager;
using mf::core::services::SettingsControl;
using mf::core::theme::ThemeManager;
using mf::core::theme::MusicefyColorScheme;

namespace {
constexpr const char* kFolderKey = "extensions/folder";
QString defaultExtensionsDir() {
    // <config>/Musicefy/extensions  (QStandardPaths::AppConfigLocation
    // is what QSettings uses internally; on Windows that maps to
    // %APPDATA%/Musicefy.)
    QString cfg = QDir::homePath() + QStringLiteral("/AppData/Roaming/Musicefy/extensions");
    if (!QDir(cfg).exists()) QDir().mkpath(cfg);
    return cfg;
}
} // anonymous namespace

ExtensionsSettingsPanel::ExtensionsSettingsPanel(ExtensionManager* extMgr,
                                                 SettingsControl*  settings,
                                                 ThemeManager*    theme,
                                                 QWidget*         parent)
    : QWidget(parent)
    , extMgr_(extMgr)
    , settings_(settings)
    , theme_(theme)
{
    buildUi();
    applyTheme();
    loadFromSettings();
    refreshList();

    if (extMgr_) {
        connect(extMgr_, &ExtensionManager::extensionsLoadedQ,
                this, &ExtensionsSettingsPanel::onExtensionsLoaded);
        connect(extMgr_, &ExtensionManager::extensionEnabledQ,
                this, &ExtensionsSettingsPanel::onExtensionToggled);
        connect(extMgr_, &ExtensionManager::extensionDisabledQ,
                this, &ExtensionsSettingsPanel::onExtensionToggled);
    }
    if (theme_) {
        connect(theme_, &ThemeManager::schemeChanged,
                this, [this]() { applyTheme(); });
    }
}

void ExtensionsSettingsPanel::buildUi() {
    auto* root = new QVBoxLayout(this);
    root->setContentsMargins(32, 28, 32, 28);
    root->setSpacing(14);

    auto* title = new QLabel(QStringLiteral("Extensions"), this);
    QFont tf = title->font();
    tf.setPointSize(18);
    tf.setBold(true);
    title->setFont(tf);
    root->addWidget(title);

    auto* blurb = new QLabel(
        QStringLiteral("Manage third-party music source plugins. "
                       "Drop a .dll / .so / .dylib into the folder, click "
                       "Reload, and toggle to enable."),
        this);
    blurb->setWordWrap(true);
    blurb->setProperty("role", QStringLiteral("secondary"));
    root->addWidget(blurb);

    // ── Folder picker ───────────────────────────────────────────
    auto* folderHeader = new QLabel(QStringLiteral("Extensions folder"), this);
    QFont fhf = folderHeader->font();
    fhf.setPointSize(12);
    fhf.setBold(true);
    folderHeader->setFont(fhf);
    root->addWidget(folderHeader);

    auto* folderRow = new QHBoxLayout();
    folderRow->setSpacing(8);
    folderPath_ = new QLabel(this);
    folderPath_->setTextInteractionFlags(Qt::TextSelectableByMouse);
    folderPath_->setWordWrap(true);
    folderPath_->setProperty("role", QStringLiteral("pathDisplay"));
    folderRow->addWidget(folderPath_, /*stretch=*/1);

    auto* changeBtn = new QPushButton(QStringLiteral("Change…"), this);
    changeBtn->setCursor(Qt::PointingHandCursor);
    connect(changeBtn, &QPushButton::clicked,
            this, &ExtensionsSettingsPanel::onChangeDirClicked);
    folderRow->addWidget(changeBtn);

    reloadBtn_ = new QPushButton(QStringLiteral("Reload"), this);
    reloadBtn_->setCursor(Qt::PointingHandCursor);
    connect(reloadBtn_, &QPushButton::clicked,
            this, &ExtensionsSettingsPanel::onReloadClicked);
    folderRow->addWidget(reloadBtn_);

    root->addLayout(folderRow);

    // ── Loaded extensions list ─────────────────────────────────
    auto* listHeader = new QLabel(QStringLiteral("Loaded extensions"), this);
    QFont lhf = listHeader->font();
    lhf.setPointSize(12);
    lhf.setBold(true);
    listHeader->setFont(lhf);
    root->addWidget(listHeader);

    list_  = new QListView(this);
    model_ = new QStandardItemModel(this);
    model_->setHorizontalHeaderLabels(
        {QStringLiteral("Name"),
         QStringLiteral("Version"),
         QStringLiteral("Author"),
         QStringLiteral("Status")});
    list_->setModel(model_);
    list_->setUniformItemSizes(true);
    list_->setEditTriggers(QAbstractItemView::NoEditTriggers);
    list_->setSelectionMode(QAbstractItemView::SingleSelection);
    list_->setAlternatingRowColors(true);
    connect(model_, &QStandardItemModel::itemChanged,
            this, &ExtensionsSettingsPanel::onItemChanged);
    root->addWidget(list_, /*stretch=*/1);

    statusLabel_ = new QLabel(this);
    statusLabel_->setProperty("role", QStringLiteral("secondary"));
    root->addWidget(statusLabel_);
}

void ExtensionsSettingsPanel::applyTheme() {
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
        "QPushButton { background: %5; color: %1;"
        "  border: 1px solid %4; border-radius: 6px; padding: 6px 14px; }"
        "QPushButton:hover { background: %6; }"
        "QListView { background: %3; color: %1;"
        "  border: 1px solid %4; border-radius: 6px;"
        "  selection-background-color: %5; selection-color: %7;"
        "  alternate-background-color: %8; }"
        "QHeaderView::section { background: %3; color: %2; padding: 6px;"
        "  border: none; border-bottom: 1px solid %4; }"
    )
    .arg(s.onSurface.name())
    .arg(s.onSurfaceVariant.name())
    .arg(s.surfaceContainerHigh.name())
    .arg(s.outlineVariant.name())
    .arg(s.primaryContainer.name())
    .arg(s.surfaceContainerHighest.name())
    .arg(s.onPrimaryContainer.name())
    .arg(s.surfaceContainer.name())
    );
}

void ExtensionsSettingsPanel::loadFromSettings() {
    QString dir;
    if (settings_) {
        dir = settings_->getOrDefault<QString>(QString::fromLatin1(kFolderKey),
                                               QString());
    }
    if (dir.isEmpty()) dir = defaultExtensionsDir();
    if (folderPath_) folderPath_->setText(QDir::toNativeSeparators(dir));
}

void ExtensionsSettingsPanel::refreshList() {
    if (!model_) return;
    model_->removeRows(0, model_->rowCount());
    if (!extMgr_) {
        if (statusLabel_) statusLabel_->setText(
            QStringLiteral("Extension manager not available."));
        return;
    }
    const auto all = extMgr_->allExtensions();
    for (const auto& m : all) {
        auto* nameItem    = new QStandardItem(m.name());
        auto* versionItem = new QStandardItem(m.version());
        auto* authorItem  = new QStandardItem(m.author());
        auto* statusItem  = new QStandardItem(
            m.isEnabled() ? QStringLiteral("Enabled")
                          : QStringLiteral("Disabled"));
        // Stash the extension id on the first column for later lookup.
        nameItem->setData(m.id(), Qt::UserRole);
        // Make the status column checkable so the user can toggle
        // enable/disable via a click in the cell.
        statusItem->setCheckable(true);
        statusItem->setCheckState(m.isEnabled() ? Qt::Checked : Qt::Unchecked);
        model_->appendRow({nameItem, versionItem, authorItem, statusItem});
    }
    if (statusLabel_) {
        statusLabel_->setText(
            QStringLiteral("%1 loaded (%2 enabled)")
            .arg(all.size())
            .arg(extMgr_->enabledExtensions().size()));
    }
}

void ExtensionsSettingsPanel::onChangeDirClicked() {
    QString start = folderPath_ ? folderPath_->text() : defaultExtensionsDir();
    QString chosen = QFileDialog::getExistingDirectory(
        this, QStringLiteral("Choose extensions folder"), start);
    if (chosen.isEmpty()) return;
    if (settings_) {
        settings_->set(QString::fromLatin1(kFolderKey), chosen);
        settings_->sync();
    }
    if (folderPath_) folderPath_->setText(QDir::toNativeSeparators(chosen));
}

void ExtensionsSettingsPanel::onReloadClicked() {
    if (!extMgr_) return;
    QString dir = folderPath_ ? folderPath_->text() : defaultExtensionsDir();
    if (dir.isEmpty()) dir = defaultExtensionsDir();
    extMgr_->loadExtensions(dir, [this](QList<ExtensionManifest> manifests) {
        Q_UNUSED(manifests);
        // Refresh happens via the extensionsLoadedQ signal.
    });
    if (folderPath_) folderPath_->setText(QDir::toNativeSeparators(dir));
}

void ExtensionsSettingsPanel::onItemChanged(QStandardItem* item) {
    if (!item || !extMgr_) return;
    // Only react to status column toggles.
    if (!item->isCheckable()) return;
    int row = item->index().row();
    if (row < 0 || row >= model_->rowCount()) return;
    auto* nameItem = model_->item(row, 0);
    if (!nameItem) return;
    QString id = nameItem->data(Qt::UserRole).toString();
    if (id.isEmpty()) return;

    bool nowEnabled = item->checkState() == Qt::Checked;
    if (nowEnabled) extMgr_->enableExtension(id);
    else            extMgr_->disableExtension(id);

    // Update the textual "Enabled/Disabled" cell so it stays in sync
    // (the signal handler will re-run refreshList, but this avoids a
    // momentary inconsistency for the click itself).
    item->setText(nowEnabled ? QStringLiteral("Enabled")
                             : QStringLiteral("Disabled"));
}

void ExtensionsSettingsPanel::onExtensionsLoaded() {
    refreshList();
}

void ExtensionsSettingsPanel::onExtensionToggled(const QString& /*id*/) {
    refreshList();
}

} // namespace mf::app::widgets

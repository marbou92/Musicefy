// SourcesSettingsPanel.cpp
// See header.

#include "SourcesSettingsPanel.h"
#include "AddSourceDialog.h"

#include "../core/models/StreamingSource.h"
#include "../core/sources/StreamingSourceManager.h"
#include "../core/theme/MusicefyColorScheme.h"
#include "../core/theme/ThemeManager.h"

#include <QFont>
#include <QHBoxLayout>
#include <QInputDialog>
#include <QLabel>
#include <QLineEdit>
#include <QListView>
#include <QPushButton>
#include <QStandardItem>
#include <QStandardItemModel>
#include <QUuid>
#include <QVBoxLayout>

namespace mf::app::widgets {

using mf::core::models::StreamingSource;
using mf::core::sources::StreamingSourceManager;
using mf::core::theme::ThemeManager;
using mf::core::theme::MusicefyColorScheme;

namespace {
// Helper: ask the user for N text fields in sequence, return a
// QStringList of values. Empty QVariant -> user cancelled.
QStringList promptFields(QWidget* parent,
                         const QString&    title,
                         const QStringList& labels,
                         const QStringList& defaults) {
    QStringList result;
    if (labels.size() != defaults.size()) return result;
    for (int i = 0; i < labels.size(); ++i) {
        bool ok = false;
        QString text = QInputDialog::getText(
            parent,
            title,
            QStringLiteral("%1 %2").arg(i + 1).arg(labels[i]),
            QLineEdit::Normal,
            defaults[i],
            &ok);
        if (!ok) return QStringList{};
        result << text.trimmed();
    }
    return result;
}
} // anonymous namespace

SourcesSettingsPanel::SourcesSettingsPanel(StreamingSourceManager* sourceMgr,
                                           ThemeManager*          theme,
                                           QWidget*               parent)
    : QWidget(parent)
    , sourceMgr_(sourceMgr)
    , theme_(theme)
{
    buildUi();
    applyTheme();
    refreshList();

    if (sourceMgr_) {
        connect(sourceMgr_, &StreamingSourceManager::sourceAdded,
                this, &SourcesSettingsPanel::onSourceAdded);
        connect(sourceMgr_, &StreamingSourceManager::sourceUpdated,
                this, &SourcesSettingsPanel::onSourceUpdated);
        connect(sourceMgr_, &StreamingSourceManager::sourceRemoved,
                this, &SourcesSettingsPanel::onSourceRemoved);
    }
    if (theme_) {
        connect(theme_, &ThemeManager::schemeChanged,
                this, [this]() { applyTheme(); });
    }
}

void SourcesSettingsPanel::buildUi() {
    auto* root = new QVBoxLayout(this);
    root->setContentsMargins(32, 28, 32, 28);
    root->setSpacing(14);

    auto* title = new QLabel(QStringLiteral("Sources"), this);
    QFont tf = title->font();
    tf.setPointSize(18);
    tf.setBold(true);
    title->setFont(tf);
    root->addWidget(title);

    auto* blurb = new QLabel(
        QStringLiteral("Configure streaming accounts. Drop a Subsonic-"
                       "compatible server, link YouTube, or load an "
                       "extension to add a new source type."),
        this);
    blurb->setWordWrap(true);
    blurb->setProperty("role", QStringLiteral("secondary"));
    root->addWidget(blurb);

    auto* listHeader = new QLabel(QStringLiteral("Configured sources"), this);
    QFont lhf = listHeader->font();
    lhf.setPointSize(12);
    lhf.setBold(true);
    listHeader->setFont(lhf);
    root->addWidget(listHeader);

    list_  = new QListView(this);
    model_ = new QStandardItemModel(this);
    model_->setHorizontalHeaderLabels(
        {QStringLiteral("Name"),
         QStringLiteral("Type"),
         QStringLiteral("URL / API key"),
         QStringLiteral("Status")});
    list_->setModel(model_);
    list_->setUniformItemSizes(true);
    list_->setEditTriggers(QAbstractItemView::NoEditTriggers);
    list_->setSelectionMode(QAbstractItemView::SingleSelection);
    list_->setAlternatingRowColors(true);
    root->addWidget(list_, /*stretch=*/1);

    auto* btnRow = new QHBoxLayout();
    btnRow->setSpacing(8);
    addBtn_ = new QPushButton(QStringLiteral("Add Subsonic…"), this);
    addBtn_->setCursor(Qt::PointingHandCursor);
    connect(addBtn_, &QPushButton::clicked,
            this, &SourcesSettingsPanel::onAddSubsonicClicked);
    btnRow->addWidget(addBtn_);

    addOtherBtn_ = new QPushButton(QStringLiteral("Add Other…"), this);
    addOtherBtn_->setCursor(Qt::PointingHandCursor);
    addOtherBtn_->setProperty("role", QStringLiteral("secondaryButton"));
    connect(addOtherBtn_, &QPushButton::clicked,
            this, &SourcesSettingsPanel::onAddOtherClicked);
    btnRow->addWidget(addOtherBtn_);

    removeBtn_ = new QPushButton(QStringLiteral("Remove selected"), this);
    removeBtn_->setCursor(Qt::PointingHandCursor);
    removeBtn_->setProperty("role", QStringLiteral("secondaryButton"));
    connect(removeBtn_, &QPushButton::clicked,
            this, &SourcesSettingsPanel::onRemoveClicked);
    btnRow->addWidget(removeBtn_);
    btnRow->addStretch(1);
    root->addLayout(btnRow);

    auto* note = new QLabel(
        QStringLiteral("Note: credentials are stored in QSettings in plain "
                       "text. Block 7 (security) will add encryption."),
        this);
    note->setWordWrap(true);
    note->setProperty("role", QStringLiteral("secondary"));
    root->addWidget(note);
}

void SourcesSettingsPanel::applyTheme() {
    MusicefyColorScheme s;
    if (theme_) s = theme_->scheme();
    if (!s.primary.isValid()) {
        setStyleSheet(QString());
        return;
    }
    setStyleSheet(QStringLiteral(
        "QWidget { background: transparent; color: %1; }"
        "QLabel[role=\"secondary\"] { color: %2; }"
        "QPushButton { background: %3; color: %1;"
        "  border: 1px solid %4; border-radius: 6px; padding: 6px 14px; }"
        "QPushButton:hover { background: %5; }"
        "QPushButton[role=\"secondaryButton\"] { background: %6; }"
        "QPushButton[role=\"secondaryButton\"]:hover { background: %5; }"
        "QListView { background: %6; color: %1;"
        "  border: 1px solid %4; border-radius: 6px;"
        "  selection-background-color: %3; selection-color: %7;"
        "  alternate-background-color: %8; }"
        "QHeaderView::section { background: %6; color: %2; padding: 6px;"
        "  border: none; border-bottom: 1px solid %4; }"
    )
    .arg(s.onSurface.name())
    .arg(s.onSurfaceVariant.name())
    .arg(s.primaryContainer.name())
    .arg(s.outlineVariant.name())
    .arg(s.surfaceContainerHighest.name())
    .arg(s.surfaceContainerHigh.name())
    .arg(s.onPrimaryContainer.name())
    .arg(s.surfaceContainer.name())
    );
}

void SourcesSettingsPanel::refreshList() {
    if (!model_) return;
    model_->removeRows(0, model_->rowCount());
    if (!sourceMgr_) {
        return;
    }
    const auto all = sourceMgr_->allSources();
    for (const auto& src : all) {
        auto* nameItem  = new QStandardItem(src.name());
        auto* typeItem  = new QStandardItem(src.type());
        auto* urlItem   = new QStandardItem(
            src.type() == QStringLiteral("YouTube")
                ? QStringLiteral("(api key)")
                : src.url());
        auto* statusItem = new QStandardItem(
            src.isConnected() ? QStringLiteral("Connected")
                              : QStringLiteral("Not connected"));
        // Stash the source id for later removal.
        nameItem->setData(src.id(), Qt::UserRole);
        model_->appendRow({nameItem, typeItem, urlItem, statusItem});
    }
}

void SourcesSettingsPanel::onAddSubsonicClicked() {
    if (!sourceMgr_) return;
    QStringList fields = promptFields(
        this,
        QStringLiteral("Add Subsonic source"),
        {QStringLiteral("Display name (e.g. \"My Navidrome\")"),
         QStringLiteral("Server URL (e.g. https://music.example.com)"),
         QStringLiteral("Username"),
         QStringLiteral("Password")},
        {QStringLiteral("My Subsonic"),
         QStringLiteral("https://"),
         QString(),
         QString()});
    if (fields.size() != 4) return;
    if (fields[0].isEmpty() || fields[1].isEmpty()) return;
    if (!fields[1].startsWith(QStringLiteral("http://"),  Qt::CaseInsensitive) &&
        !fields[1].startsWith(QStringLiteral("https://"), Qt::CaseInsensitive)) {
        return;
    }

    StreamingSource src;
    src.setId(QUuid::createUuid().toString(QUuid::WithoutBraces));
    src.setName(fields[0]);
    src.setType(QStringLiteral("Subsonic"));
    src.setUrl(fields[1]);
    src.setUsername(fields[2]);
    src.setPassword(fields[3]);
    src.setIsConnected(false);
    src.setClientVersion(QStringLiteral("1.0"));
    sourceMgr_->addSource(src);
}

void SourcesSettingsPanel::onRemoveClicked() {
    if (!list_ || !sourceMgr_) return;
    int row = list_->currentIndex().row();
    if (row < 0 || row >= model_->rowCount()) return;
    auto* nameItem = model_->item(row, 0);
    if (!nameItem) return;
    QString id = nameItem->data(Qt::UserRole).toString();
    if (id.isEmpty()) return;
    sourceMgr_->removeSource(id);
}

void SourcesSettingsPanel::onAddOtherClicked() {
    if (!sourceMgr_) return;
    AddSourceDialog dlg(sourceMgr_, theme_, this);
    if (dlg.exec() != QDialog::Accepted) return;
    StreamingSource src = dlg.resultSource();
    if (src.name().isEmpty() || src.type().isEmpty()) return;
    sourceMgr_->addSource(src);
}

void SourcesSettingsPanel::onSourceAdded()    { refreshList(); }
void SourcesSettingsPanel::onSourceUpdated()  { refreshList(); }
void SourcesSettingsPanel::onSourceRemoved()  { refreshList(); }

} // namespace mf::app::widgets

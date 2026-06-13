// Sidebar.cpp
// See header. QListWidget in iconMode=false, single-selection.

#include "Sidebar.h"

#include "SvgIcon.h"
#include "viewmodels/MainViewModel.h"
#include "../core/theme/ThemeManager.h"
#include "../core/theme/MusicefyColorScheme.h"

#include <QFont>
#include <QListWidget>
#include <QListWidgetItem>

namespace mf::app::widgets {

using mf::app::viewmodels::MainViewModel;
using mf::core::theme::ThemeManager;
using mf::core::theme::MusicefyColorScheme;

Sidebar::Sidebar(MainViewModel* vm,
                 ThemeManager*   theme,
                 QWidget*        parent)
    : QWidget(parent)
    , vm_(vm)
    , theme_(theme)
{
    setObjectName(QStringLiteral("Sidebar"));
    buildUi();
    populate();
    applyTheme();

    if (vm_) {
        connect(vm_, &MainViewModel::currentPageChanged,
                this, &Sidebar::onVmPageChanged);
    }
    if (theme_) {
        connect(theme_, &ThemeManager::schemeChanged,
                this, &Sidebar::applyTheme);
    }
}

void Sidebar::buildUi() {
    list_ = new QListWidget(this);
    list_->setSelectionMode(QAbstractItemView::SingleSelection);
    list_->setFocusPolicy(Qt::NoFocus);
    list_->setUniformItemSizes(true);
    list_->setHorizontalScrollBarPolicy(Qt::ScrollBarAlwaysOff);
    list_->setFrameShape(QFrame::NoFrame);
    list_->setSpacing(2);
    list_->setIconSize(QSize(20, 20));

    connect(list_, &QListWidget::currentRowChanged,
            this, &Sidebar::onRowChanged);
}

void Sidebar::populate() {
    list_->clear();
    auto add = [this](const QString& label, const QString& iconName) {
        auto* item = new QListWidgetItem(list_);
        item->setText(QStringLiteral("  %1").arg(label));
        item->setData(int(Qt::UserRole), iconName);
        item->setTextAlignment(Qt::AlignLeft | Qt::AlignVCenter);
        item->setSizeHint(QSize(0, 44));
        list_->addItem(item);
    };
    add(QStringLiteral("Home"),     QStringLiteral("home"));
    add(QStringLiteral("Search"),   QStringLiteral("search"));
    add(QStringLiteral("Library"),  QStringLiteral("library"));
    add(QStringLiteral("Settings"), QStringLiteral("settings"));
    add(QStringLiteral("Discover"), QStringLiteral("sparkles"));
    add(QStringLiteral("Folders"),  QStringLiteral("folder"));

    if (vm_) {
        list_->setCurrentRow(vm_->currentPage());
    }
}

void Sidebar::onRowChanged(int currentRow) {
    if (!vm_) return;
    if (currentRow < 0) return;
    vm_->setPage(currentRow);
}

void Sidebar::onVmPageChanged() {
    if (!vm_) return;
    int row = vm_->currentPage();
    if (row >= 0 && row < list_->count() && list_->currentRow() != row) {
        QSignalBlocker block(list_);
        list_->setCurrentRow(row);
    }
}

void Sidebar::onThemeChanged() {
    applyTheme();
}

void Sidebar::applyTheme() {
    MusicefyColorScheme s;
    if (theme_) s = theme_->scheme();
    if (!s.primary.isValid()) {
        setStyleSheet(QString());
        return;
    }

    QFont f = list_->font();
    f.setPointSize(10);
    list_->setFont(f);

    setStyleSheet(QStringLiteral(
        "QWidget#Sidebar { background: %1; border-right: 1px solid %2; }"
        "QListWidget { background: transparent; border: none; outline: 0;"
        "  color: %3; padding: 8px 0; }"
        "QListWidget::item { padding: 10px 16px; border: none;"
        "  border-radius: 8px; margin: 2px 8px; }"
        "QListWidget::item:hover { background: %4; }"
        "QListWidget::item:selected { background: %5; color: %6; }"
    )
    .arg(s.surfaceContainer.name())
    .arg(s.outlineVariant.name())
    .arg(s.onSurface.name())
    .arg(s.surfaceContainerHigh.name())
    .arg(s.primaryContainer.name())
    .arg(s.onPrimaryContainer.name())
    );
    // Re-render icons with the current text color.
    const QColor iconColor = s.onSurface;
    const QColor iconColorSelected = s.onPrimaryContainer;
    for (int i = 0; i < list_->count(); ++i) {
        auto* item = list_->item(i);
        const QString iconName = item->data(int(Qt::UserRole)).toString();
        if (iconName.isEmpty()) continue;
        // Default icon + selected icon (Qt picks the right one based
        // on item state). For simplicity, use a single icon and let
        // the stylesheet recolor it via selection-background-color.
        item->setIcon(SvgIcon::get(iconName, iconColor, 20));
        Q_UNUSED(iconColorSelected);
    }
}

} // namespace mf::app::widgets

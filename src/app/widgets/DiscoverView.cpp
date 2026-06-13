// DiscoverView.cpp
// See header. The widget binds to DiscoverViewModel and renders
// the three feeds (charts / moods / new releases) as vertical
// blocks. Each block has a header and a QListView in IconMode for
// horizontal scrolling. All queue work is delegated to the view
// model; the widget handles styling, layout, and signal routing.

#include "DiscoverView.h"

#include "SvgIcon.h"

#include "../core/models/BrowseSection.h"
#include "../core/models/MusicFile.h"
#include "../core/theme/MusicefyColorScheme.h"
#include "../core/theme/ThemeManager.h"
#include "../viewmodels/DiscoverViewModel.h"

#include <QFont>
#include <QFrame>
#include <QHBoxLayout>
#include <QLabel>
#include <QListView>
#include <QListWidgetItem>
#include <QPushButton>
#include <QScrollArea>
#include <QStackedWidget>
#include <QStandardItem>
#include <QStandardItemModel>
#include <QVBoxLayout>

namespace mf::app::widgets {

using mf::core::models::BrowseSection;
using mf::core::models::MusicFile;
using mf::core::theme::MusicefyColorScheme;
using mf::core::theme::ThemeManager;
using mf::app::viewmodels::DiscoverViewModel;

DiscoverView::DiscoverView(DiscoverViewModel* vm,
                           ThemeManager*      theme,
                           QWidget*           parent)
    : QWidget(parent)
    , vm_(vm)
    , theme_(theme)
{
    buildUi();
    applyTheme();

    if (theme_) {
        connect(theme_, &ThemeManager::schemeChanged,
                this, &DiscoverView::onThemeChanged);
    }
    if (vm_) {
        connect(vm_, &DiscoverViewModel::contentChanged,
                this, &DiscoverView::onContentChanged);
        connect(vm_, &DiscoverViewModel::loadingChanged,
                this, &DiscoverView::onLoadingChanged);
        // Trigger an initial fetch if we have a VM.
        vm_->refreshIfStale();
        rebuildFeeds();
    }
}

void DiscoverView::buildUi() {
    auto* root = new QVBoxLayout(this);
    root->setContentsMargins(0, 0, 0, 0);
    root->setSpacing(0);

    // ── Stacked: content (page with feeds) vs. loading placeholder
    stack_ = new QStackedWidget(this);
    root->addWidget(stack_, /*stretch=*/1);

    // Content page: scrollable, with header + 3 feed blocks.
    auto* scroller = new QScrollArea(stack_);
    scroller->setWidgetResizable(true);
    scroller->setFrameShape(QFrame::NoFrame);

    contentPage_ = new QWidget(scroller);
    auto* col = new QVBoxLayout(contentPage_);
    col->setContentsMargins(28, 18, 28, 28);
    col->setSpacing(16);

    // Header row: title + subtitle + refresh button.
    auto* headerRow = new QHBoxLayout();
    headerRow->setSpacing(8);
    auto* titleCol = new QVBoxLayout();
    titleCol->setSpacing(2);
    auto* title = new QLabel(QStringLiteral("Discover"), contentPage_);
    QFont tf = title->font();
    tf.setPointSize(22);
    tf.setBold(true);
    title->setFont(tf);
    titleCol->addWidget(title);
    auto* subtitle = new QLabel(
        QStringLiteral("Curated picks from your sources."), contentPage_);
    subtitle->setProperty("role", QStringLiteral("secondary"));
    titleCol->addWidget(subtitle);
    headerRow->addLayout(titleCol, /*stretch=*/1);

    refreshBtn_ = new QPushButton(contentPage_);
    refreshBtn_->setText(QStringLiteral("  Refresh"));
    refreshBtn_->setCursor(Qt::PointingHandCursor);
    refreshBtn_->setObjectName(QStringLiteral("refresh"));
    refreshBtn_->setIconSize(QSize(16, 16));
    connect(refreshBtn_, &QPushButton::clicked,
            this, &DiscoverView::onRefreshClicked);
    headerRow->addWidget(refreshBtn_, /*stretch=*/0);
    col->addLayout(headerRow);

    contentBody_ = new QVBoxLayout();
    contentBody_->setSpacing(20);
    col->addLayout(contentBody_, /*stretch=*/1);

    scroller->setWidget(contentPage_);
    stack_->addWidget(scroller);

    // Loading placeholder.
    auto* loadingPage = new QWidget(stack_);
    auto* loadingLayout = new QVBoxLayout(loadingPage);
    loadingLayout->setAlignment(Qt::AlignCenter);
    auto* loadingLabel = new QLabel(
        QStringLiteral("Loading discover feeds\u2026"), loadingPage);
    QFont lf = loadingLabel->font();
    lf.setPointSize(14);
    loadingLabel->setFont(lf);
    loadingLabel->setProperty("role", QStringLiteral("secondary"));
    loadingLayout->addWidget(loadingLabel, /*stretch=*/0, Qt::AlignCenter);
    stack_->addWidget(loadingPage);

    // Empty placeholder.
    auto* emptyPage = new QWidget(stack_);
    auto* emptyLayout = new QVBoxLayout(emptyPage);
    emptyLayout->setAlignment(Qt::AlignCenter);
    emptyLabel_ = new QLabel(
        QStringLiteral("Nothing to discover yet.\n"
                       "Add a source to see curated picks here."),
        emptyPage);
    emptyLabel_->setAlignment(Qt::AlignCenter);
    emptyLabel_->setProperty("role", QStringLiteral("secondary"));
    QFont ef = emptyLabel_->font();
    ef.setPointSize(13);
    emptyLabel_->setFont(ef);
    emptyLayout->addWidget(emptyLabel_, /*stretch=*/0, Qt::AlignCenter);
    stack_->addWidget(emptyPage);
}

QFrame* DiscoverView::buildFeedFrame(const QString& title,
                                     QListView**      listOut,
                                     QStandardItemModel** modelOut,
                                     void (DiscoverView::*activated)(const QModelIndex&),
                                     void (DiscoverView::*doubleClicked)(const QModelIndex&)) {
    auto* frame = new QFrame(contentPage_);
    frame->setObjectName(QStringLiteral("feedFrame"));

    auto* col = new QVBoxLayout(frame);
    col->setContentsMargins(12, 10, 12, 14);
    col->setSpacing(8);

    auto* header = new QLabel(title, frame);
    QFont hf = header->font();
    hf.setPointSize(13);
    hf.setBold(true);
    header->setFont(hf);
    col->addWidget(header);

    auto* list = new QListView(frame);
    auto* model = new QStandardItemModel(list);
    list->setModel(model);
    list->setViewMode(QListView::IconMode);
    list->setIconSize(QSize(140, 140));
    list->setGridSize(QSize(160, 200));
    list->setResizeMode(QListView::Adjust);
    list->setMovement(QListView::Static);
    list->setFlow(QListView::LeftToRight);
    list->setWrapping(false);
    list->setHorizontalScrollBarPolicy(Qt::ScrollBarAsNeeded);
    list->setVerticalScrollBarPolicy(Qt::ScrollBarAlwaysOff);
    list->setEditTriggers(QAbstractItemView::NoEditTriggers);
    list->setSelectionMode(QAbstractItemView::SingleSelection);
    list->setUniformItemSizes(true);
    list->setFrameShape(QFrame::NoFrame);
    list->setMinimumHeight(210);

    connect(list, &QListView::clicked,
            this, [this, activated](const QModelIndex& idx) {
                (this->*activated)(idx);
            });
    connect(list, &QListView::doubleClicked,
            this, [this, doubleClicked](const QModelIndex& idx) {
                (this->*doubleClicked)(idx);
            });

    col->addWidget(list, /*stretch=*/1);

    *listOut  = list;
    *modelOut = model;
    return frame;
}

void DiscoverView::rebuildFeeds() {
    if (!vm_) return;
    if (contentBody_) clearLayout(contentBody_);

    if (vm_->isLoading()) {
        if (stack_) stack_->setCurrentIndex(1);
        return;
    }
    if (!vm_->hasContent()) {
        if (stack_) stack_->setCurrentIndex(2);
        return;
    }
    if (stack_) stack_->setCurrentIndex(0);

    if (vm_->chartCount() > 0) {
        QFrame* frame = buildFeedFrame(QStringLiteral("Charts"),
                                       &chartList_, &chartModel_,
                                       &DiscoverView::onChartActivated,
                                       &DiscoverView::onChartDoubleClicked);
        contentBody_->addWidget(frame);
        for (const BrowseSection& s : vm_->charts()) {
            for (const MusicFile& m : s.items()) {
                auto* item = new QStandardItem(m.title());
                item->setData(m.id(), Qt::UserRole);
                item->setToolTip(m.title());
                chartModel_->appendRow(item);
            }
        }
    }
    if (vm_->moodCount() > 0) {
        QFrame* frame = buildFeedFrame(QStringLiteral("Moods & Genres"),
                                       &moodList_, &moodModel_,
                                       &DiscoverView::onMoodActivated,
                                       &DiscoverView::onMoodDoubleClicked);
        contentBody_->addWidget(frame);
        for (const BrowseSection& s : vm_->moods()) {
            for (const MusicFile& m : s.items()) {
                auto* item = new QStandardItem(m.title());
                item->setData(m.id(), Qt::UserRole);
                item->setToolTip(m.title());
                moodModel_->appendRow(item);
            }
        }
    }
    if (vm_->newReleaseCount() > 0) {
        QFrame* frame = buildFeedFrame(QStringLiteral("New releases"),
                                       &newReleaseList_, &newReleaseModel_,
                                       &DiscoverView::onNewReleaseActivated,
                                       &DiscoverView::onNewReleaseDoubleClicked);
        contentBody_->addWidget(frame);
        for (const BrowseSection& s : vm_->newReleases()) {
            for (const MusicFile& m : s.items()) {
                auto* item = new QStandardItem(m.title());
                item->setData(m.id(), Qt::UserRole);
                item->setToolTip(m.title());
                newReleaseModel_->appendRow(item);
            }
        }
    }
    contentBody_->addStretch(1);
}

void DiscoverView::clearLayout(QLayout* layout) {
    if (!layout) return;
    QLayoutItem* item;
    while ((item = layout->takeAt(0)) != nullptr) {
        if (item->widget()) {
            item->widget()->deleteLater();
        } else if (item->layout()) {
            clearLayout(item->layout());
            delete item->layout();
        }
        delete item;
    }
}

void DiscoverView::onContentChanged() {
    rebuildFeeds();
}

void DiscoverView::onLoadingChanged() {
    rebuildFeeds();
}

void DiscoverView::onThemeChanged() {
    applyTheme();
}

void DiscoverView::onRefreshClicked() {
    if (vm_) vm_->refresh();
}

void DiscoverView::onChartActivated(const QModelIndex& idx) {
    if (!vm_ || !idx.isValid()) return;
    vm_->playAllFromCharts(0);
}

void DiscoverView::onMoodActivated(const QModelIndex& idx) {
    Q_UNUSED(idx);
    if (!vm_) return;
    vm_->playAllFromMoods(0);
}

void DiscoverView::onNewReleaseActivated(const QModelIndex& idx) {
    Q_UNUSED(idx);
    if (!vm_) return;
    vm_->playAllFromNewReleases(0);
}

void DiscoverView::onChartDoubleClicked(const QModelIndex& idx) {
    if (!vm_ || !idx.isValid()) return;
    vm_->playTrackInCharts(0, idx.row());
}

void DiscoverView::onMoodDoubleClicked(const QModelIndex& idx) {
    if (!vm_ || !idx.isValid()) return;
    vm_->playTrackInMoods(0, idx.row());
}

void DiscoverView::onNewReleaseDoubleClicked(const QModelIndex& idx) {
    if (!vm_ || !idx.isValid()) return;
    vm_->playTrackInNewReleases(0, idx.row());
}

void DiscoverView::applyTheme() {
    MusicefyColorScheme s;
    if (theme_) s = theme_->scheme();
    if (!s.surface.isValid()) {
        setStyleSheet(QString());
        return;
    }
    setStyleSheet(QStringLiteral(
        "QWidget { background: %1; color: %2; }"
        "QFrame#feedFrame { background: %3; border: 1px solid %4;"
        "  border-radius: 8px; }"
        "QPushButton#refresh {"
        "  background: transparent; color: %5; border: 1px solid %4;"
        "  border-radius: 6px; padding: 6px 12px; }"
        "QPushButton#refresh:hover { background: %6; color: %7; }"
        "QListView { background: transparent; color: %2; border: none; }"
        "QListView::item { padding: 6px; border-radius: 6px; }"
        "QListView::item:hover { background: %6; }"
        "QListView::item:selected { background: %7; color: %8; }"
        "QLabel[role=\"secondary\"] { color: %5; }"
    )
    .arg(s.surface.name())                  // 1  bg
    .arg(s.onSurface.name())                // 2  text
    .arg(s.surfaceContainer.name())         // 3  feed frame
    .arg(s.outlineVariant.name())           // 4  border
    .arg(s.onSurfaceVariant.name())         // 5  secondary
    .arg(s.surfaceContainerHigh.name())     // 6  hover
    .arg(s.primary.name())                  // 7  accent
    .arg(s.onPrimary.name())                // 8  selected text
    );

    // Re-render the refresh icon with the current theme color.
    if (refreshBtn_) {
        refreshBtn_->setIcon(SvgIcon::get("refresh-cw", s.onSurfaceVariant, 16));
    }
}

} // namespace mf::app::widgets

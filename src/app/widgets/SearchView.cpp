// SearchView.cpp

#include "SearchView.h"
#include "../viewmodels/SearchViewModel.h"
#include "../viewmodels/LibraryViewModel.h"
#include "../../core/services/NavigationService.h"
#include "../../core/theme/ThemeManager.h"
#include "../../core/theme/MusicefyColorScheme.h"
#include "Icons.h"

#include <QVBoxLayout>
#include <QHBoxLayout>
#include <QLineEdit>
#include <QListView>
#include <QStandardItemModel>
#include <QPushButton>
#include <QLabel>
#include <QTimer>
#include <QKeyEvent>

namespace mf::app::widgets {

// ──────────────────────────────────────────────────────────────────
SearchView::SearchView(mf::app::viewmodels::SearchViewModel*  vm,
                       mf::core::services::NavigationService* nav,
                       mf::core::theme::ThemeManager*         theme,
                       QWidget* parent)
    : QWidget(parent), vm_(vm), nav_(nav), theme_(theme)
{
    buildUi();
    applyTheme();

    connect(theme_, &mf::core::theme::ThemeManager::themeChanged,
            this, &SearchView::onThemeChanged);
}

// ──────────────────────────────────────────────────────────────────
void SearchView::buildUi()
{
    auto* root = new QVBoxLayout(this);
    root->setContentsMargins(0, 0, 0, 0);
    root->setSpacing(0);

    // ── Search bar ──────────────────────────────────────────────
    auto* searchBar = new QWidget;
    auto* sbLayout = new QHBoxLayout(searchBar);
    sbLayout->setContentsMargins(16, 12, 16, 8);
    sbLayout->setSpacing(8);

    searchInput_ = new QLineEdit;
    searchInput_->setPlaceholderText(QStringLiteral("Search tracks, artists, albums…"));
    searchInput_->setClearButtonEnabled(false);
    sbLayout->addWidget(searchInput_, 1);

    clearBtn_ = new QPushButton(QStringLiteral("✕"));
    clearBtn_->setFixedSize(32, 32);
    clearBtn_->setToolTip(QStringLiteral("Clear search"));
    sbLayout->addWidget(clearBtn_);

    modeBtn_ = new QPushButton(QStringLiteral("Online"));
    modeBtn_->setCheckable(true);
    modeBtn_->setChecked(true);
    modeBtn_->setToolTip(QStringLiteral("Toggle local/online search"));
    sbLayout->addWidget(modeBtn_);

    root->addWidget(searchBar);

    // ── Suggestion list (shown during Suggestions state) ────────
    suggestionList_ = new QListView;
    suggestionModel_ = new QStandardItemModel(this);
    suggestionList_->setModel(suggestionModel_);
    suggestionList_->setMaximumHeight(240);
    suggestionList_->setUniformItemSizes(true);
    suggestionList_->setVisible(false);
    root->addWidget(suggestionList_);

    // ── Filter bar ──────────────────────────────────────────────
    filterBar_ = new QWidget;
    auto* fbLayout = new QHBoxLayout(filterBar_);
    fbLayout->setContentsMargins(16, 4, 16, 4);
    fbLayout->setSpacing(4);

    const QStringList filterNames = {
        QStringLiteral("All"),
        QStringLiteral("Songs"),
        QStringLiteral("Albums"),
        QStringLiteral("Artists"),
        QStringLiteral("Playlists")
    };

    for (int i = 0; i < filterNames.size(); ++i) {
        auto* btn = new QPushButton(filterNames[i]);
        btn->setCheckable(true);
        btn->setChecked(i == 0);
        btn->setObjectName(QStringLiteral("filterBtn"));
        fbLayout->addWidget(btn);
        filterButtons_.append(btn);
        connect(btn, &QPushButton::clicked, this, [this, i]() { onFilterClicked(i); });
    }
    fbLayout->addStretch();
    filterBar_->setVisible(false);
    root->addWidget(filterBar_);

    // ── Results list ────────────────────────────────────────────
    resultsList_ = new QListView;
    resultsModel_ = new QStandardItemModel(this);
    resultsList_->setModel(resultsModel_);
    resultsList_->setUniformItemSizes(true);
    root->addWidget(resultsList_, 1);

    // ── Empty state ─────────────────────────────────────────────
    emptyState_ = new QLabel(QStringLiteral("Type to search…"));
    emptyState_->setAlignment(Qt::AlignCenter);
    root->addWidget(emptyState_);

    // ── Connections ─────────────────────────────────────────────
    connect(searchInput_, &QLineEdit::textChanged,
            this, &SearchView::onQueryChanged);
    connect(searchInput_, &QLineEdit::returnPressed,
            this, &SearchView::onSearchSubmitted);
    connect(clearBtn_, &QPushButton::clicked,
            this, &SearchView::onClearClicked);
    connect(modeBtn_, &QPushButton::clicked,
            this, &SearchView::onModeToggled);
    connect(suggestionList_, &QListView::clicked,
            this, &SearchView::onSuggestionClicked);
    connect(resultsList_, &QListView::doubleClicked,
            this, &SearchView::onResultDoubleClicked);

    connect(vm_, &mf::app::viewmodels::SearchViewModel::stateChanged,
            this, &SearchView::onStateChanged);
    connect(vm_, &mf::app::viewmodels::SearchViewModel::resultsChanged,
            this, &SearchView::onResultsChanged);
    connect(vm_, &mf::app::viewmodels::SearchViewModel::suggestionsChanged,
            this, &SearchView::onSuggestionsChanged);
}

// ──────────────────────────────────────────────────────────────────
void SearchView::applyTheme()
{
    const auto c = mf::core::theme::lightDefault();

    setStyleSheet(QStringLiteral(
        "SearchView { background: %1; }"
        "QLineEdit {"
        "  background: %2; color: %3; border: none;"
        "  border-radius: 12px; padding: 10px 14px; font-size: 14px;"
        "}"
        "QLineEdit:focus { outline: none; }"
        "QPushButton {"
        "  background: %2; color: %3; border: none;"
        "  border-radius: 8px; padding: 6px 12px; font-size: 12px;"
        "}"
        "QPushButton:hover { background: %4; }"
        "QPushButton:checked { background: %5; color: %6; }"
        "QListView {"
        "  background: transparent; color: %3; border: none;"
        "  outline: none; padding: 4px 0;"
        "}"
        "QListView::item {"
        "  padding: 8px 16px; border-radius: 8px; margin: 2px 8px;"
        "}"
        "QListView::item:hover { background: %4; }"
        "QListView::item:selected { background: %5; color: %6; }"
        "QLabel { color: %7; font-size: 13px; }"
    )
    .arg(c.surface.name(), c.surfaceContainer.name(), c.onSurface.name(),
         c.surfaceContainerHigh.name(), c.primary.name(), c.onPrimary.name(),
         c.onSurfaceVariant.name()));

    // Style filter buttons
    for (auto* btn : filterButtons_) {
        btn->setStyleSheet(QStringLiteral(
            "QPushButton {"
            "  background: transparent; color: %1; border: none;"
            "  border-radius: 16px; padding: 6px 16px; font-size: 12px;"
            "}"
            "QPushButton:hover { background: %2; }"
            "QPushButton:checked { background: %3; color: %4; }"
        )
        .arg(c.onSurfaceVariant.name(), c.surfaceContainerHigh.name(),
             c.primary.name(), c.onPrimary.name()));
    }
}

// ──────────────────────────────────────────────────────────────────
void SearchView::onQueryChanged(const QString& text)
{
    vm_->setQuery(text);
}

// ──────────────────────────────────────────────────────────────────
void SearchView::onSearchSubmitted()
{
    vm_->search();
}

// ──────────────────────────────────────────────────────────────────
void SearchView::onClearClicked()
{
    searchInput_->clear();
    vm_->clearQuery();
    searchInput_->setFocus();
}

// ──────────────────────────────────────────────────────────────────
void SearchView::onModeToggled()
{
    const int mode = modeBtn_->isChecked() ? 1 : 0; // Online=1, Local=0
    modeBtn_->setText(modeBtn_->isChecked() ? QStringLiteral("Online")
                                            : QStringLiteral("Local"));
    vm_->setSourceMode(mode);
}

// ──────────────────────────────────────────────────────────────────
void SearchView::onFilterClicked(int filter)
{
    for (int i = 0; i < filterButtons_.size(); ++i)
        filterButtons_[i]->setChecked(i == filter);
    vm_->selectFilter(filter);
}

// ──────────────────────────────────────────────────────────────────
void SearchView::onSuggestionClicked(const QModelIndex& idx)
{
    const QString text = suggestionModel_->itemFromIndex(idx)->text();
    searchInput_->setText(text);
    vm_->selectSuggestion(text);
}

// ──────────────────────────────────────────────────────────────────
void SearchView::onResultDoubleClicked(const QModelIndex& idx)
{
    const auto item = resultsModel_->itemFromIndex(idx);
    if (!item) return;

    const int kind = item->data(Qt::UserRole + 1).toInt();
    const QString value = item->data(Qt::UserRole + 2).toString();

    switch (kind) {
    case 0: // Track
        vm_->playTrack(value);
        break;
    case 1: // Artist
        vm_->navigateToArtist(value);
        break;
    case 2: { // Album
        // Album needs artist too; store as "album|artist"
        const auto parts = value.split(QStringLiteral("|"));
        if (parts.size() == 2)
            vm_->navigateToAlbum(parts[0], parts[1]);
        break;
    }
    }
}

// ──────────────────────────────────────────────────────────────────
void SearchView::onStateChanged()
{
    const auto s = static_cast<mf::app::viewmodels::SearchState>(vm_->state());
    suggestionList_->setVisible(s == mf::app::viewmodels::SearchState::Suggestions);
    filterBar_->setVisible(s == mf::app::viewmodels::SearchState::Results);
    updateEmptyState();
}

// ──────────────────────────────────────────────────────────────────
void SearchView::onResultsChanged()
{
    rebuildResultsModel();
    updateEmptyState();
}

// ──────────────────────────────────────────────────────────────────
void SearchView::onSuggestionsChanged()
{
    suggestionModel_->clear();
    for (const auto& s : vm_->suggestions()) {
        auto* item = new QStandardItem(s);
        item->setData(0, Qt::UserRole + 1); // suggestion type
        suggestionModel_->appendRow(item);
    }
}

// ──────────────────────────────────────────────────────────────────
void SearchView::onThemeChanged()
{
    applyTheme();
}

// ──────────────────────────────────────────────────────────────────
void SearchView::rebuildResultsModel()
{
    resultsModel_->clear();

    const auto groups = vm_->resultGroups();
    for (const auto& group : groups) {
        // Section header
        auto* header = new QStandardItem(group.header().toUpper());
        header->setEditable(false);
        header->setData(-1, Qt::UserRole + 1); // header marker
        QFont f = header->font();
        f.setBold(true);
        f.setPointSize(11);
        header->setFont(f);
        resultsModel_->appendRow(header);

        // Items
        for (const auto& track : group.results()) {
            auto* item = new QStandardItem(
                QStringLiteral("%1 — %2").arg(track.title(), track.artist()));
            item->setEditable(false);
            item->setData(0, Qt::UserRole + 1); // track
            item->setData(track.filePath(), Qt::UserRole + 2);
            resultsModel_->appendRow(item);
        }
    }
}

// ──────────────────────────────────────────────────────────────────
void SearchView::updateEmptyState()
{
    const auto s = static_cast<mf::app::viewmodels::SearchState>(vm_->state());
    const bool showEmpty = (s == mf::app::viewmodels::SearchState::Idle &&
                            vm_->query().isEmpty()) ||
                           (s == mf::app::viewmodels::SearchState::Results &&
                            vm_->totalResultCount() == 0 && !vm_->hasError());

    if (showEmpty) {
        emptyState_->setVisible(true);
        resultsList_->setVisible(false);
        if (vm_->query().isEmpty())
            emptyState_->setText(QStringLiteral("Type to search…"));
        else
            emptyState_->setText(QStringLiteral("No matches for \"%1\"").arg(vm_->query()));
    } else {
        emptyState_->setVisible(false);
        resultsList_->setVisible(true);
    }
}

} // namespace mf::app::widgets

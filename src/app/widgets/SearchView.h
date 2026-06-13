// SearchView.h
// Search page with dual Local/Online mode, autocomplete suggestions,
// search history, and filter tabs.
//
//   ┌──────────────────────────────────────────────────────────┐
//   │ 🔍 [ search…                                  ] [✕]      │
//   │    [Local] [Online]                                     │
//   ├──────────────────────────────────────────────────────────┤
//   │ Suggestions / Recent Searches                           │
//   │  • suggestion 1                                         │
//   │  • suggestion 2                                         │
//   ├──────────────────────────────────────────────────────────┤
//   │ [All] [Songs] [Albums] [Artists] [Playlists]            │
//   │                                                         │
//   │ TRACKS                                                  │
//   │  • title — artist — album                       3:42   │
//   │ ARTISTS                                                 │
//   │  • Artist Name                                          │
//   │ ALBUMS                                                  │
//   │  • Album — Artist                                       │
//   └──────────────────────────────────────────────────────────┘

#pragma once

#include <QList>
#include <QWidget>

class QLabel;
class QLineEdit;
class QListView;
class QPushButton;
class QStandardItemModel;
class QTimer;

namespace mf::app::viewmodels { class SearchViewModel; class LibraryViewModel; }
namespace mf::core::services { class NavigationService; }
namespace mf::core::theme     { class ThemeManager; }

namespace mf::app::widgets {

class SearchView : public QWidget {
    Q_OBJECT
public:
    SearchView(mf::app::viewmodels::SearchViewModel*  vm,
               mf::core::services::NavigationService* nav,
               mf::core::theme::ThemeManager*         theme,
               QWidget* parent = nullptr);
    ~SearchView() override = default;

private slots:
    void onQueryChanged(const QString& text);
    void onSearchSubmitted();
    void onClearClicked();
    void onModeToggled();
    void onFilterClicked(int filter);
    void onSuggestionClicked(const QModelIndex& idx);
    void onResultDoubleClicked(const QModelIndex& idx);
    void onStateChanged();
    void onResultsChanged();
    void onSuggestionsChanged();
    void onThemeChanged();

private:
    void buildUi();
    void applyTheme();
    void rebuildResultsModel();
    void updateEmptyState();

    mf::app::viewmodels::SearchViewModel* vm_    = nullptr;
    mf::core::services::NavigationService* nav_  = nullptr;
    mf::core::theme::ThemeManager*         theme_= nullptr;

    QLineEdit*           searchInput_    = nullptr;
    QPushButton*         clearBtn_       = nullptr;
    QPushButton*         modeBtn_        = nullptr;
    QListView*           suggestionList_ = nullptr;
    QStandardItemModel*  suggestionModel_= nullptr;
    QWidget*             filterBar_      = nullptr;
    QList<QPushButton*>  filterButtons_;
    QListView*           resultsList_    = nullptr;
    QStandardItemModel*  resultsModel_   = nullptr;
    QLabel*              emptyState_     = nullptr;
};

} // namespace mf::app::widgets

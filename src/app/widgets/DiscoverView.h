// DiscoverView.h
// The Discover page. Renders the three DiscoverViewModel feeds
// (charts / moods / new releases) as horizontal carousels over a
// refresh control, or shows a placeholder while loading.
//
// Layout:
//   ┌────────────────────────────────────────┐
//   │  Discover                          [↻] │
//   │  Curated picks from your sources.      │
//   ├────────────────────────────────────────┤
//   │  Charts                                │
//   │   ▸  ▸  ▸  ▸  ▸                         │
//   ├────────────────────────────────────────┤
//   │  Moods & Genres                        │
//   │   ▸  ▸  ▸  ▸                            │
//   ├────────────────────────────────────────┤
//   │  New releases                          │
//   │   ▸  ▸  ▸                               │
//   └────────────────────────────────────────┘
//
// Click a card → play that feed (via the view model's playAll*).
// Double-click a card → play just that one track.

#pragma once

#include <QWidget>

class QFrame;
class QLabel;
class QListView;
class QPushButton;
class QScrollArea;
class QStackedWidget;
class QStandardItemModel;
class QVBoxLayout;

namespace mf::core::theme        { class ThemeManager; }
namespace mf::app::viewmodels    { class DiscoverViewModel; }

namespace mf::app::widgets {

class DiscoverView : public QWidget {
    Q_OBJECT
public:
    DiscoverView(mf::app::viewmodels::DiscoverViewModel* vm,
                 mf::core::theme::ThemeManager*          theme,
                 QWidget* parent = nullptr);
    ~DiscoverView() override = default;

private slots:
    void onContentChanged();
    void onLoadingChanged();
    void onThemeChanged();
    void onRefreshClicked();
    void onChartActivated(const QModelIndex& idx);
    void onMoodActivated(const QModelIndex& idx);
    void onNewReleaseActivated(const QModelIndex& idx);
    void onChartDoubleClicked(const QModelIndex& idx);
    void onMoodDoubleClicked(const QModelIndex& idx);
    void onNewReleaseDoubleClicked(const QModelIndex& idx);

private:
    void buildUi();
    void applyTheme();
    void rebuildFeeds();
    void clearLayout(QLayout* layout);
    QFrame* buildFeedFrame(const QString& title,
                           QListView**      listOut,
                           QStandardItemModel** modelOut,
                           void (DiscoverView::*activated)(const QModelIndex&),
                           void (DiscoverView::*doubleClicked)(const QModelIndex&));

    mf::app::viewmodels::DiscoverViewModel* vm_    = nullptr;
    mf::core::theme::ThemeManager*          theme_ = nullptr;

    QStackedWidget*    stack_      = nullptr;
    QWidget*           contentPage_ = nullptr;
    QVBoxLayout*       contentBody_ = nullptr;
    QPushButton*       refreshBtn_  = nullptr;
    QLabel*            emptyLabel_  = nullptr;
    QListView*         chartList_      = nullptr;
    QListView*         moodList_       = nullptr;
    QListView*         newReleaseList_ = nullptr;
    QStandardItemModel* chartModel_    = nullptr;
    QStandardItemModel* moodModel_     = nullptr;
    QStandardItemModel* newReleaseModel_ = nullptr;
};

} // namespace mf::app::widgets

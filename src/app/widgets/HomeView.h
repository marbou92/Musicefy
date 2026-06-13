// HomeView.h
// The real Home page. Renders the four HomeViewModel sections as
// horizontal carousels (Quick access / Recently played / Favourites
// / Playlists) over a friendly greeting header, OR shows a one-tap
// "Add a folder to get started" empty state if the library has
// nothing to show.
//
// Layout:
//   ┌────────────────────────────────────────┐
//   │  Good morning,                          │
//   │  Welcome back.                          │
//   ├────────────────────────────────────────┤
//   │  Quick access         ▸  ▸  ▸  ▸        │
//   ├────────────────────────────────────────┤
//   │  Recently played      ▸  ▸  ▸  ▸  ▸  ▸  │
//   ├────────────────────────────────────────┤
//   │  Favourites           ▸  ▸  ▸  ▸        │
//   ├────────────────────────────────────────┤
//   │  Your playlists       ▸  ▸  ▸           │
//   └────────────────────────────────────────┘
//
// All cards are clickable: tracks enqueue + play, playlists fire
// NavigationService::requestPlaylist so the overlay system can take
// over.
//
// Visuals are driven by the active MusicefyColorScheme — a single
// applyTheme() rebuilds every stylesheet on schemeChanged.

#pragma once

#include <QFrame>
#include <QList>
#include <QPointer>
#include <QString>
#include <QWidget>

class QHBoxLayout;
class QLabel;
class QPushButton;
class QStackedWidget;
class QVBoxLayout;

namespace mf::core::models { class MusicFile; class PlaylistInfo; }
namespace mf::core::services { class LibraryService; class ImageCache; }
namespace mf::core::theme { class ThemeManager; }

namespace mf::app::viewmodels { class HomeViewModel; }

namespace mf::app::widgets {

class HomeView : public QWidget {
    Q_OBJECT
public:
    HomeView(mf::app::viewmodels::HomeViewModel* vm,
             mf::core::services::LibraryService*  libSvc,
             mf::core::services::ImageCache*      imageCache,
             mf::core::theme::ThemeManager*       theme,
             QWidget* parent = nullptr);
    ~HomeView() override = default;

private slots:
    void onContentChanged();
    void onThemeChanged();
    void onAddFolderClicked();
    void onRescanClicked();
    void onPlayAllQuick();
    void onPlayAllRecent();
    void onPlayAllFav();
    void onPlayAllMostPlayed();
    void onPlayAllRecentlyAdded();
    void onTrackActivated(const QString& filePath);
    void onPlaylistActivated(int index);

private:
    void buildUi();
    void applyTheme();
    void rebuildContent();
    void clearLayout(QLayout* layout);

    QFrame* buildCarouselFrame(const QString& title,
                               const QString& playAllLabel,
                               QHBoxLayout**   bodyOut);
    QFrame* buildTrackCard(const mf::core::models::MusicFile& track);
    QFrame* buildPlaylistCard(const mf::core::models::PlaylistInfo& pl,
                              int index);

    mf::app::viewmodels::HomeViewModel* vm_      = nullptr;
    mf::core::services::LibraryService* libSvc_  = nullptr;
    mf::core::services::ImageCache*     imageCache_ = nullptr;
    mf::core::theme::ThemeManager*      theme_   = nullptr;

    QStackedWidget* stack_       = nullptr;
    QWidget*        contentPage_ = nullptr;
    QWidget*        emptyPage_   = nullptr;
    QVBoxLayout*    contentBody_ = nullptr;
    QLabel*         greeting_    = nullptr;
    QLabel*         subtitle_    = nullptr;
    QPushButton*    rescanBtn_   = nullptr;
};

} // namespace mf::app::widgets

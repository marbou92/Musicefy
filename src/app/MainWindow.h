// MainWindow.h
// Top-level application shell. Layout:
//   ┌─────────┬────────────────────────────┐
//   │ Sidebar │  QStackedWidget (pages)    │   ← contentStack_ index 0
//   │         │   - HomeView                │
//   │         │   - SearchView              │
//   │         │   - LibraryView             │
//   │         │   - SettingsPage            │
//   │         │   - DiscoverView            │
//   │         ├────────────────────────────┤
//   │         │  QStackedWidget (overlays) │   ← contentStack_ index 1
//   │         │   - ArtistView (1)          │
//   │         │   - AlbumView (2)           │
//   │         │   - PlaylistView (3)        │
//   ├─────────┴────────────────────────────┤
//   │            NowPlayingBar             │
//   └──────────────────────────────────────┘
//
// A transparent ToastOverlay covers the whole rect and pins
// notification cards to the bottom-right (above the bar).

#pragma once

#include "AppContainer.h"

#include <QMainWindow>

#include <memory>

class QStackedWidget;
class QHBoxLayout;

namespace mf::core::models { class AlbumInfo; class ArtistInfo;
                             class PlaylistInfo; }
namespace mf::core::services { class NavigationService; }
namespace mf::core::theme     { class ThemeManager; }
namespace mf::app::viewmodels { class MainViewModel; }
namespace mf::app::widgets {
    class Sidebar;
    class HomeView;
    class SearchView;
    class LibraryView;
    class SettingsPage;
    class NowPlayingBar;
    class NowPlayingView;
    class ToastOverlay;
    class ArtistView;
    class AlbumView;
    class PlaylistView;
    class DiscoverView;
    class OverlayStackAnimator;
    class FolderLibraryControl;
}

namespace mf::app {

class MainWindow : public QMainWindow {
    Q_OBJECT
public:
    explicit MainWindow(AppContainer& container, QWidget* parent = nullptr);
    ~MainWindow() override;

private:
    void buildUi();
    void wireNavigation();
    void applyTheme();

    // Overlay slots — react to NavigationService typed entity signals.
    void onArtistRequested(mf::core::models::ArtistInfo info);
    void onAlbumRequested(mf::core::models::AlbumInfo info);
    void onPlaylistRequested(mf::core::models::PlaylistInfo info);
    void onCloseOverlay();
    void onNowPlayingRequested();

    AppContainer& container_;

    mf::app::viewmodels::MainViewModel* mainVm_ = nullptr;
    mf::core::services::NavigationService* nav_  = nullptr;
    mf::core::theme::ThemeManager* theme_         = nullptr;

    // Top-level content switcher: index 0 = pages, index 1 = overlay.
    QStackedWidget* contentStack_   = nullptr;
    widgets::Sidebar*        sidebar_        = nullptr;

    // Page stack (index 0 of contentStack_).
    QStackedWidget* stack_          = nullptr;
    widgets::HomeView*       homeView_       = nullptr;
    widgets::SearchView*     searchView_     = nullptr;
    widgets::LibraryView*    libraryView_    = nullptr;
    widgets::SettingsPage*   settingsPage_   = nullptr;
    widgets::DiscoverView*   discoverView_   = nullptr;
    widgets::FolderLibraryControl* folderControl_ = nullptr;

    // Overlay stack (index 1 of contentStack_).
    QStackedWidget* overlayStack_   = nullptr;
    widgets::ArtistView*     artistView_     = nullptr;
    widgets::AlbumView*      albumView_      = nullptr;
    widgets::PlaylistView*   playlistView_   = nullptr;
    widgets::NowPlayingView* nowPlayingView_ = nullptr;

    widgets::NowPlayingBar*  nowPlayingBar_  = nullptr;
    widgets::ToastOverlay*   toastOverlay_   = nullptr;
    std::unique_ptr<mf::app::widgets::OverlayStackAnimator> overlayAnimator_;
};

} // namespace mf::app

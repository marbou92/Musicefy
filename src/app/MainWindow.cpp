// MainWindow.cpp
// See header. The full UI shell: sidebar + page router + overlay
// stack + now-playing bar + toast overlay. Re-pulls the theme +
// the main view-model from the AppContainer passed in by main.cpp.

#include "MainWindow.h"

#include "../core/models/AlbumInfo.h"
#include "../core/models/ArtistInfo.h"
#include "../core/models/PlaylistInfo.h"
#include "../core/services/NavigationService.h"
#include "../core/services/ToastService.h"
#include "../core/theme/MusicefyColorScheme.h"
#include "../core/theme/ThemeManager.h"

#include "viewmodels/HomeViewModel.h"
#include "viewmodels/LibraryViewModel.h"
#include "viewmodels/MainViewModel.h"
#include "viewmodels/PlayerViewModel.h"
#include "viewmodels/SearchViewModel.h"

#include "widgets/AlbumView.h"
#include "widgets/ArtistView.h"
#include "widgets/DiscoverView.h"
#include "widgets/HomeView.h"
#include "widgets/LibraryView.h"
#include "widgets/NowPlayingBar.h"
#include "widgets/NowPlayingView.h"
#include "widgets/OverlayStackAnimator.h"
#include "widgets/PlaylistView.h"
#include "widgets/SearchView.h"
#include "widgets/SettingsPage.h"
#include "widgets/Sidebar.h"
#include "widgets/ToastOverlay.h"
#include "widgets/FolderLibraryControl.h"

#include <QApplication>
#include <QHBoxLayout>
#include <QLabel>
#include <QPalette>
#include <QStackedWidget>
#include <QStyleFactory>
#include <QVBoxLayout>
#include <QWidget>

namespace mf::app {

using mf::app::viewmodels::HomeViewModel;
using mf::app::viewmodels::LibraryViewModel;
using mf::app::viewmodels::MainViewModel;
using mf::app::viewmodels::PlayerViewModel;
using mf::app::widgets::AlbumView;
using mf::app::widgets::ArtistView;
using mf::app::widgets::HomeView;
using mf::app::widgets::LibraryView;
using mf::app::widgets::NowPlayingBar;
using mf::app::widgets::NowPlayingView;
using mf::app::widgets::PlaylistView;
using mf::app::widgets::SearchView;
using mf::app::widgets::SettingsPage;
using mf::app::widgets::Sidebar;
using mf::app::widgets::ToastOverlay;
using mf::app::widgets::FolderLibraryControl;
using mf::app::widgets::DiscoverView;
using mf::app::widgets::OverlayStackAnimator;
using mf::core::models::AlbumInfo;
using mf::core::models::ArtistInfo;
using mf::core::models::PlaylistInfo;
using mf::core::services::NavigationService;
using mf::core::services::ToastService;
using mf::core::theme::ThemeManager;
using mf::core::theme::ThemeMode;
using mf::core::theme::MusicefyColorScheme;

MainWindow::MainWindow(AppContainer& container, QWidget* parent)
    : QMainWindow(parent)
    , container_(container)
{
    mainVm_ = container_.mainVm().get();
    nav_    = container_.nav().get();
    theme_  = container_.theme().get();

    setWindowTitle(QStringLiteral("Musicefy"));
    resize(1280, 800);

    buildUi();
    wireNavigation();
    applyTheme();

    if (theme_) {
        connect(theme_, &ThemeManager::schemeChanged,
                this, &MainWindow::applyTheme);
    }
}

MainWindow::~MainWindow() = default;

void MainWindow::buildUi() {
    auto* central = new QWidget(this);
    auto* root    = new QVBoxLayout(central);
    root->setContentsMargins(0, 0, 0, 0);
    root->setSpacing(0);

    // ── Body: sidebar + (page stack / overlay stack) ────────────
    auto* body = new QWidget(central);
    auto* bodyLayout = new QHBoxLayout(body);
    bodyLayout->setContentsMargins(0, 0, 0, 0);
    bodyLayout->setSpacing(0);

    sidebar_ = new Sidebar(mainVm_, theme_, body);
    bodyLayout->addWidget(sidebar_, /*stretch=*/0);

    // contentStack_ swaps between the normal page stack (0) and the
    // overlay stack (1). The sidebar stays visible in both modes.
    contentStack_ = new QStackedWidget(body);
    bodyLayout->addWidget(contentStack_, /*stretch=*/1);

    // Page stack (contentStack_ index 0).
    stack_ = new QStackedWidget(contentStack_);
    auto homeVm  = container_.homeVm();
    auto libSvc  = container_.libraryService();
    auto* toasts  = container_.toasts().get();
    auto libVm   = container_.libraryVm();
    auto* queue   = container_.queue().get();

    homeView_     = new HomeView(homeVm.get(), libSvc.get(),
                                 container_.imageCache().get(),
                                 theme_, stack_);
    if (libVm && nav_ && theme_) {
        auto searchVm = container_.searchVm();
        searchView_ = new SearchView(searchVm.get(), nav_, theme_, stack_);
    } else {
        searchView_ = nullptr;
    }
    auto* downloads = container_.downloads().get();
    auto* extMgr    = container_.extensions().get();
    auto* sourceMgr = container_.sourceManager().get();
    auto* settings  = container_.settings().get();
    settingsPage_ = new SettingsPage(libSvc.get(), toasts, downloads, extMgr, sourceMgr, settings, theme_, stack_);

    if (libVm && theme_ && nav_) {
        libraryView_ = new LibraryView(libVm.get(), nav_, theme_, stack_);
    } else {
        libraryView_ = nullptr;
    }

    stack_->addWidget(homeView_);      // index 0
    if (searchView_) {
        stack_->addWidget(searchView_);  // index 1
    } else {
        auto* placeholder = new QLabel(
            QStringLiteral("(search unavailable — DI not built)"),
            stack_);
        placeholder->setAlignment(Qt::AlignCenter);
        stack_->addWidget(placeholder);
    }
    if (libraryView_) {
        stack_->addWidget(libraryView_); // index 2
    } else {
        auto* placeholder = new QLabel(
            QStringLiteral("(library unavailable — DI not built)"),
            stack_);
        placeholder->setAlignment(Qt::AlignCenter);
        stack_->addWidget(placeholder);
    }
    stack_->addWidget(settingsPage_);  // index 3
    if (theme_) {
        auto discVm = container_.discoverVm();
        if (discVm) {
            discoverView_ = new DiscoverView(discVm.get(), theme_, stack_);
        } else {
            discoverView_ = nullptr;
        }
    } else {
        discoverView_ = nullptr;
    }
    if (discoverView_) {
        stack_->addWidget(discoverView_);  // index 4
    } else {
        auto* placeholder = new QLabel(
            QStringLiteral("(discover unavailable — DI not built)"),
            stack_);
        placeholder->setAlignment(Qt::AlignCenter);
        stack_->addWidget(placeholder);
    }

    // Folders page (index 5)
    auto libSvcFolders = container_.libraryService();
    auto navFolders    = container_.nav();
    auto toastsFolders = container_.toasts();
    if (libSvcFolders && navFolders && toastsFolders && theme_) {
        folderControl_ = new FolderLibraryControl(
            libSvcFolders.get(), navFolders.get(),
            toastsFolders.get(), theme_, stack_);
    } else {
        folderControl_ = nullptr;
    }
    if (folderControl_) {
        stack_->addWidget(folderControl_);  // index 5
    } else {
        auto* placeholder = new QLabel(
            QStringLiteral("(folders unavailable — DI not built)"),
            stack_);
        placeholder->setAlignment(Qt::AlignCenter);
        stack_->addWidget(placeholder);
    }

    if (mainVm_) {
        stack_->setCurrentIndex(mainVm_->currentPage());
    }

    contentStack_->addWidget(stack_);  // contentStack_ index 0

    // Overlay stack (contentStack_ index 1).
    overlayStack_ = new QStackedWidget(contentStack_);

    // Index 0 of overlayStack_ is a transparent placeholder that
    // shouldn't actually be visible (contentStack_ index 1 implies
    // an overlay is open). We add a no-op widget anyway so the
    // indices of artistView/albumView/playlistView stay >= 1.
    auto* overlayPlaceholder = new QWidget(overlayStack_);
    overlayPlaceholder->setAttribute(Qt::WA_TransparentForMouseEvents);
    overlayStack_->addWidget(overlayPlaceholder);

    if (nav_ && queue && theme_) {
        auto* imgCache = container_.imageCache().get();
        auto* albumVm  = container_.albumVm().get();
        auto* plVm     = container_.playlistVm().get();
        auto* artVm    = container_.artistVm().get();
        auto* libRepo  = container_.library().get();
        auto* libVmForPl    = container_.libraryVm().get();
        artistView_   = new ArtistView(artVm, nav_, queue, imgCache, theme_, overlayStack_);
        albumView_    = new AlbumView(albumVm, nav_, queue, imgCache, theme_, overlayStack_);
        playlistView_ = new PlaylistView(plVm, nav_, queue, imgCache, libRepo, theme_, libVmForPl, overlayStack_);
        overlayStack_->addWidget(artistView_);    // index 1
        overlayStack_->addWidget(albumView_);     // index 2
        overlayStack_->addWidget(playlistView_);  // index 3
    } else {
        artistView_ = nullptr;
        albumView_ = nullptr;
        playlistView_ = nullptr;
    }
    // NowPlayingView (Block 2.7) — needs PlayerViewModel.
    auto playerVmForNp = container_.playerVm();
    if (nav_ && queue && theme_ && playerVmForNp) {
        auto* imgCache = container_.imageCache().get();
        auto* sleepTimer = container_.sleepTimer().get();
        nowPlayingView_ = new NowPlayingView(
            playerVmForNp.get(), queue, nav_, imgCache, theme_,
            sleepTimer, overlayStack_);
        overlayStack_->addWidget(nowPlayingView_);  // index 4
    } else {
        nowPlayingView_ = nullptr;
    }
    contentStack_->addWidget(overlayStack_); // contentStack_ index 1

    // Wire the cross-fade animator. It owns its own QGraphicsOpacityEffect
    // and QPropertyAnimation, attached to contentStack_ itself.
    if (contentStack_ && overlayStack_) {
        overlayAnimator_ = std::make_unique<OverlayStackAnimator>(
            contentStack_, overlayStack_, this);
    }

    root->addWidget(body, /*stretch=*/1);

    // ── NowPlayingBar pinned at the bottom ──────────────────────────
    auto playerVm = container_.playerVm();
    if (playerVm && theme_) {
        auto* imgCache = container_.imageCache().get();
        auto* sleepTimer = container_.sleepTimer().get();
        nowPlayingBar_ = new NowPlayingBar(
            playerVm.get(), container_.queue().get(), imgCache, theme_,
            sleepTimer, central);
        connect(nowPlayingBar_, &NowPlayingBar::expandRequested,
                this, &MainWindow::onNowPlayingRequested);
        root->addWidget(nowPlayingBar_, /*stretch=*/0);
    }

    // ── ToastOverlay: transparent, on top of everything ─────────────
    auto toast = container_.toasts();
    if (toast && theme_) {
        toastOverlay_ = new ToastOverlay(toast.get(), theme_, central);
        toast->bindOverlay(toastOverlay_);
    }

    setCentralWidget(central);
}

void MainWindow::wireNavigation() {
    if (mainVm_) {
        connect(mainVm_, &MainViewModel::pageChangeRequested,
                this, [this](int index) {
            if (stack_) stack_->setCurrentIndex(index);
        });
    }
    if (nav_ && mainVm_) {
        connect(nav_, &NavigationService::pageRequested,
                mainVm_, &MainViewModel::setPageByName);
    }
    if (nav_) {
        // Typed entity navigation → overlay stack.
        connect(nav_, &NavigationService::artistNavigationRequested,
                this, &MainWindow::onArtistRequested);
        connect(nav_, &NavigationService::albumNavigationRequested,
                this, &MainWindow::onAlbumRequested);
        connect(nav_, &NavigationService::playlistNavigationRequested,
                this, &MainWindow::onPlaylistRequested);
        connect(nav_, &NavigationService::overlayCloseRequested,
                this, &MainWindow::onCloseOverlay);
    }
}

void MainWindow::onArtistRequested(ArtistInfo info) {
    if (!artistView_ || !overlayAnimator_) return;
    artistView_->setArtist(info);
    overlayAnimator_->showOverlay(1);
}

void MainWindow::onAlbumRequested(AlbumInfo info) {
    if (!albumView_ || !overlayAnimator_) return;
    albumView_->setAlbum(info);
    overlayAnimator_->showOverlay(2);
}

void MainWindow::onPlaylistRequested(PlaylistInfo info) {
    if (!playlistView_ || !overlayAnimator_) return;
    playlistView_->setPlaylist(info);
    overlayAnimator_->showOverlay(3);
}

void MainWindow::onCloseOverlay() {
    if (overlayAnimator_) overlayAnimator_->showPage();
}

void MainWindow::onNowPlayingRequested() {
    if (!nowPlayingView_ || !overlayAnimator_) return;
    overlayAnimator_->showOverlay(4);
}

void MainWindow::applyTheme() {
    if (!theme_ || !qApp) return;

    MusicefyColorScheme scheme = theme_->scheme();
    ThemeMode mode = theme_->effectiveMode();

    qApp->setStyle(QStyleFactory::create(QStringLiteral("Fusion")));

    QPalette p;
    if (mode == ThemeMode::Dark || mode == ThemeMode::Amoled) {
        p.setColor(QPalette::Window,          scheme.background);
        p.setColor(QPalette::WindowText,      scheme.onBackground);
        p.setColor(QPalette::Base,            scheme.surfaceContainer);
        p.setColor(QPalette::AlternateBase,   scheme.surfaceContainerHigh);
        p.setColor(QPalette::ToolTipBase,     scheme.surfaceContainerHighest);
        p.setColor(QPalette::ToolTipText,     scheme.onSurface);
        p.setColor(QPalette::Text,            scheme.onSurface);
        p.setColor(QPalette::Button,          scheme.surfaceContainerHigh);
        p.setColor(QPalette::ButtonText,      scheme.onSurface);
        p.setColor(QPalette::BrightText,      scheme.error);
        p.setColor(QPalette::Link,            scheme.primary);
        p.setColor(QPalette::Highlight,       scheme.primary);
        p.setColor(QPalette::HighlightedText, scheme.onPrimary);
    } else {
        p.setColor(QPalette::Window,          scheme.background);
        p.setColor(QPalette::WindowText,      scheme.onBackground);
        p.setColor(QPalette::Base,            scheme.surface);
        p.setColor(QPalette::AlternateBase,   scheme.surfaceContainer);
        p.setColor(QPalette::ToolTipBase,     scheme.surfaceContainer);
        p.setColor(QPalette::ToolTipText,     scheme.onSurface);
        p.setColor(QPalette::Text,            scheme.onSurface);
        p.setColor(QPalette::Button,          scheme.surfaceContainer);
        p.setColor(QPalette::ButtonText,      scheme.onSurface);
        p.setColor(QPalette::BrightText,      scheme.error);
        p.setColor(QPalette::Link,            scheme.primary);
        p.setColor(QPalette::Highlight,       scheme.primary);
        p.setColor(QPalette::HighlightedText, scheme.onPrimary);
    }
    qApp->setPalette(p);
}

} // namespace mf::app

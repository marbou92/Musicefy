// NavigationService.h
// Cross-cutting navigation dispatcher. Lives in core/services so
// view models anywhere in the app can request a navigation event
// without depending on the UI layer.
//
// Two flavours of API:
//   1) Page-level routing (requestPage / navigateBack) for normal
//      sidebar-driven navigation. These are simple signals that the
//      MainViewModel subscribes to.
//   2) Typed entity events (ArtistNavigationRequested etc.) for the
//      overlay system. The MainWindow watches these to slide an
//      ArtistView/AlbumView/PlaylistView over the current page.

#pragma once

#include "../models/ArtistInfo.h"
#include "../models/AlbumInfo.h"
#include "../models/PlaylistInfo.h"

#include <QObject>
#include <QString>

namespace mf::core::services {

class NavigationService : public QObject {
    Q_OBJECT
public:
    explicit NavigationService(QObject* parent = nullptr);
    ~NavigationService() override = default;

public slots:
    // Page-level routing. The MainViewModel listens and updates the
    // QStackedWidget.
    void requestPage(const QString& name);
    void requestHome();
    void requestSearch();
    void requestLibrary();
    void requestSettings();
    void navigateBack();

    // Typed entity navigation — overlay system.
    void requestArtist(mf::core::models::ArtistInfo info);
    void requestAlbum(mf::core::models::AlbumInfo info);
    void requestPlaylist(mf::core::models::PlaylistInfo info);

    // Overlay close. MainWindow reacts by collapsing the overlay.
    void closeOverlay();

signals:
    void pageRequested(const QString& name);
    void backRequested();

    void artistNavigationRequested(mf::core::models::ArtistInfo info);
    void albumNavigationRequested(mf::core::models::AlbumInfo info);
    void playlistNavigationRequested(mf::core::models::PlaylistInfo info);
    void overlayCloseRequested();
};

} // namespace mf::core::services

// LibraryView.h
// Local-library browser. Hosts a QTabWidget with four tabs:
//   * Tracks     — full QListView (existing behaviour, preserved)
//   * Artists    — QListView over all artists with track counts
//   * Albums     — QListView over all albums with track counts
//   * Playlists  — QListView over all playlists
//
// Double-clicking a track plays it (existing behaviour). Double-
// clicking an artist / album / playlist fires the matching typed
// signal on NavigationService so the MainWindow overlay system
// can take over and show the dedicated view.
//
// All four tabs read from a single LibraryViewModel and re-populate
// whenever the matching property-change signal fires.

#pragma once

#include <QWidget>

class QLabel;
class QListView;
class QStandardItemModel;
class QTabWidget;

namespace mf::core::services { class NavigationService; }
namespace mf::core::theme     { class ThemeManager; }

namespace mf::app::viewmodels { class LibraryViewModel; }

namespace mf::app::widgets {

class LibraryView : public QWidget {
    Q_OBJECT
public:
    LibraryView(mf::app::viewmodels::LibraryViewModel*  vm,
                mf::core::services::NavigationService*  nav,
                mf::core::theme::ThemeManager*          theme,
                QWidget* parent = nullptr);
    ~LibraryView() override = default;

private slots:
    void onTracksChanged();
    void onArtistsChanged();
    void onAlbumsChanged();
    void onPlaylistsChanged();
    void onLibraryChanged();
    void onThemeChanged();

    void onTrackDoubleClicked(const QModelIndex& proxyIdx);
    void onArtistDoubleClicked(const QModelIndex& proxyIdx);
    void onAlbumDoubleClicked(const QModelIndex& proxyIdx);
    void onPlaylistDoubleClicked(const QModelIndex& proxyIdx);
    void onMostPlayedDoubleClicked(const QModelIndex& proxyIdx);
    void onRecentAddedDoubleClicked(const QModelIndex& proxyIdx);

    void onPlayAllClicked();

private:
    void buildUi();
    void applyTheme();
    void populateTracks();
    void populateArtists();
    void populateAlbums();
    void populatePlaylists();
    void populateMostPlayed();
    void populateRecentAdded();

    mf::app::viewmodels::LibraryViewModel*  vm_    = nullptr;
    mf::core::services::NavigationService*  nav_   = nullptr;
    mf::core::theme::ThemeManager*          theme_ = nullptr;

    QTabWidget* tabs_ = nullptr;

    // Tracks tab.
    QLabel*              header_     = nullptr;
    QListView*           tracksList_ = nullptr;
    QStandardItemModel*  tracksModel_ = nullptr;
    QLabel*              tracksEmpty_ = nullptr;

    // Artists tab.
    QListView*           artistsList_  = nullptr;
    QStandardItemModel*  artistsModel_ = nullptr;
    QLabel*              artistsEmpty_ = nullptr;

    // Albums tab.
    QListView*           albumsList_   = nullptr;
    QStandardItemModel*  albumsModel_  = nullptr;
    QLabel*              albumsEmpty_  = nullptr;

    // Playlists tab.
    QListView*           playlistsList_  = nullptr;
    QStandardItemModel*  playlistsModel_ = nullptr;
    QLabel*              playlistsEmpty_ = nullptr;

    // Most Played tab.
    QListView*           mostPlayedList_  = nullptr;
    QStandardItemModel*  mostPlayedModel_ = nullptr;
    QLabel*              mostPlayedEmpty_ = nullptr;

    // Recently Added tab.
    QListView*           recentAddedList_  = nullptr;
    QStandardItemModel*  recentAddedModel_ = nullptr;
    QLabel*              recentAddedEmpty_ = nullptr;
};

} // namespace mf::app::widgets

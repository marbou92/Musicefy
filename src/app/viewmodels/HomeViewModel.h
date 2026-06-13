// HomeViewModel.h
// State for the Home page. Surfaces curated sections that the
// HomeView renders as horizontal carousels:
//   * Quick access       — the four tracks the user played most
//                          recently (one-tap resume).
//   * Recently played    — top 10 most recently played.
//   * Favourites         — every track marked is_favourite = 1.
//   * Your playlists     — every saved playlist.
//
// The data is sourced from the LibraryRepository (for tracks and
// playlists) and kept in sync with the LibraryService so that
// finishing a scan, or starring a track, refreshes the view
// automatically.
//
// `libraryIsEmpty` is a single bool that tells the view to swap the
// content layout for the "Add a folder to get started" empty state.
// It folds in "no folders added" AND "no tracks indexed" because
// both feel the same to the user.

#pragma once

#include "../../core/models/MusicFile.h"
#include "../../core/models/PlaylistInfo.h"

#include <QObject>
#include <QList>

namespace mf::core::database  { class LibraryRepository; }
namespace mf::core::playback   { class QueueManager; }
namespace mf::core::services   { class LibraryService; class ToastService;
                                  class NavigationService; }

namespace mf::app::viewmodels {

class HomeViewModel : public QObject {
    Q_OBJECT
    Q_PROPERTY(bool   libraryIsEmpty  READ libraryIsEmpty  NOTIFY contentChanged)
    Q_PROPERTY(int    recentlyPlayedCount READ recentlyPlayedCount NOTIFY contentChanged)
    Q_PROPERTY(int    favouritesCount  READ favouritesCount  NOTIFY contentChanged)
    Q_PROPERTY(int    playlistsCount   READ playlistsCount   NOTIFY contentChanged)
    Q_PROPERTY(QList<mf::core::models::MusicFile>    quickAccess    READ quickAccess    NOTIFY contentChanged)
    Q_PROPERTY(QList<mf::core::models::MusicFile>    recentlyPlayed READ recentlyPlayed NOTIFY contentChanged)
    Q_PROPERTY(QList<mf::core::models::MusicFile>    favourites     READ favourites     NOTIFY contentChanged)
    Q_PROPERTY(QList<mf::core::models::PlaylistInfo> playlists      READ playlists      NOTIFY contentChanged)
    Q_PROPERTY(QList<mf::core::models::MusicFile>    mostPlayed      READ mostPlayed     NOTIFY contentChanged)
    Q_PROPERTY(QList<mf::core::models::MusicFile>    recentlyAdded   READ recentlyAdded  NOTIFY contentChanged)
public:
    HomeViewModel(mf::core::database::LibraryRepository* repo,
                  mf::core::playback::QueueManager*      queue,
                  mf::core::services::LibraryService*    libSvc,
                  mf::core::services::ToastService*      toasts,
                  mf::core::services::NavigationService* nav,
                  QObject* parent = nullptr);
    ~HomeViewModel() override = default;

    bool   libraryIsEmpty()      const;
    int    recentlyPlayedCount() const { return recentlyPlayed_.size(); }
    int    favouritesCount()     const { return favourites_.size(); }
    int    playlistsCount()      const { return playlists_.size(); }

    QList<mf::core::models::MusicFile>    quickAccess()    const { return quickAccess_; }
    QList<mf::core::models::MusicFile>    recentlyPlayed() const { return recentlyPlayed_; }
    QList<mf::core::models::MusicFile>    favourites()     const { return favourites_; }
    QList<mf::core::models::PlaylistInfo> playlists()      const { return playlists_; }
    QList<mf::core::models::MusicFile>    mostPlayed()     const { return mostPlayed_; }
    QList<mf::core::models::MusicFile>    recentlyAdded()  const { return recentlyAdded_; }

    /// The size of the quick-access row. Capped at 4 so the layout
    /// stays tidy even with a long history.
    static constexpr int kQuickAccessMax = 4;
    /// Recently-played carousel length.
    static constexpr int kRecentlyPlayedMax = 10;
    /// Favourites carousel length.
    static constexpr int kFavouritesMax = 12;
    static constexpr int kMostPlayedMax = 12;
    static constexpr int kRecentlyAddedMax = 12;

    Q_INVOKABLE void refresh();
    Q_INVOKABLE void playTrack(const QString& filePath);
    Q_INVOKABLE void playAllFromQuickAccess();
    Q_INVOKABLE void playAllFromRecentlyPlayed();
    Q_INVOKABLE void playAllFromFavourites();
    Q_INVOKABLE void playAllFromMostPlayed();
    Q_INVOKABLE void playAllFromRecentlyAdded();
    Q_INVOKABLE void toggleFavourite(const QString& filePath);
    Q_INVOKABLE void openPlaylistAt(int index);
    Q_INVOKABLE QString greeting() const;

signals:
    void contentChanged();
    void navigationRequested(const QString& name);

private slots:
    void onLibraryServiceTracksChanged();

private:
    void loadAll();
    int  findTrackIndex(const QString& filePath) const;
    void enqueueAndPlay(int startIndex, const QList<mf::core::models::MusicFile>& list);

    mf::core::database::LibraryRepository* repo_  = nullptr;
    mf::core::playback::QueueManager*      queue_ = nullptr;
    mf::core::services::LibraryService*    libSvc_ = nullptr;
    mf::core::services::ToastService*      toasts_ = nullptr;
    mf::core::services::NavigationService* nav_    = nullptr;

    QList<mf::core::models::MusicFile>    quickAccess_;
    QList<mf::core::models::MusicFile>    recentlyPlayed_;
    QList<mf::core::models::MusicFile>    favourites_;
    QList<mf::core::models::PlaylistInfo> playlists_;
    QList<mf::core::models::MusicFile>    mostPlayed_;
    QList<mf::core::models::MusicFile>    recentlyAdded_;
};

} // namespace mf::app::viewmodels

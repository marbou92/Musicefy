// LibraryViewModel.h
// Bindable view of the local music library. Wraps LibraryRepository
// and re-publishes the lists as Q_PROPERTYs. The view (a QListView
// or a custom Widgets delegate) just reads tracks() and refreshes
// on tracksChanged.

#pragma once

#include "../../core/models/MusicFile.h"
#include "../../core/models/ArtistInfo.h"
#include "../../core/models/AlbumInfo.h"
#include "../../core/models/PlaylistInfo.h"

#include <QObject>
#include <QList>
#include <memory>

namespace mf::core::database { class LibraryRepository; }
namespace mf::core::playback  { class QueueManager; }

namespace mf::app::viewmodels {

class LibraryViewModel : public QObject {
    Q_OBJECT
    Q_PROPERTY(int trackCount    READ trackCount    NOTIFY libraryChanged)
    Q_PROPERTY(int artistCount   READ artistCount   NOTIFY libraryChanged)
    Q_PROPERTY(int albumCount    READ albumCount    NOTIFY libraryChanged)
    Q_PROPERTY(int playlistCount READ playlistCount NOTIFY libraryChanged)
    Q_PROPERTY(QList<mf::core::models::MusicFile>      tracks    READ tracks    NOTIFY tracksChanged)
    Q_PROPERTY(QList<mf::core::models::ArtistInfo>     artists   READ artists   NOTIFY artistsChanged)
    Q_PROPERTY(QList<mf::core::models::AlbumInfo>      albums    READ albums    NOTIFY albumsChanged)
    Q_PROPERTY(QList<mf::core::models::PlaylistInfo>   playlists READ playlists NOTIFY playlistsChanged)
    Q_PROPERTY(QList<mf::core::models::MusicFile>      favouriteTracks READ favouriteTracks NOTIFY tracksChanged)

public:
    LibraryViewModel(mf::core::database::LibraryRepository* repo,
                     mf::core::playback::QueueManager*      queue,
                     QObject* parent = nullptr);
    ~LibraryViewModel() override = default;

    int trackCount()    const { return tracks_.size(); }
    int artistCount()   const { return artists_.size(); }
    int albumCount()    const { return albums_.size(); }
    int playlistCount() const { return playlists_.size(); }

    QList<mf::core::models::MusicFile>    tracks()          const { return tracks_; }
    QList<mf::core::models::ArtistInfo>   artists()         const { return artists_; }
    QList<mf::core::models::AlbumInfo>    albums()          const { return albums_; }
    QList<mf::core::models::PlaylistInfo> playlists()       const { return playlists_; }
    QList<mf::core::models::MusicFile>    favouriteTracks() const;

    // ── Commands ────────────────────────────────────────────────────────
    Q_INVOKABLE void refresh();
    Q_INVOKABLE void playTrack(const QString& filePath);
    Q_INVOKABLE void playAll();
    Q_INVOKABLE void toggleFavourite(const QString& filePath);
    Q_INVOKABLE void deleteTrack(const QString& filePath);
    Q_INVOKABLE void createPlaylist(const QString& name);

    // ── Lookup helpers for the view ─────────────────────────────────────
    Q_INVOKABLE mf::core::models::MusicFile trackAt(int row) const;
    Q_INVOKABLE int rowForTrack(const QString& filePath) const;

signals:
    void libraryChanged();      // any of the counts changed
    void tracksChanged();
    void artistsChanged();
    void albumsChanged();
    void playlistsChanged();

private:
    void loadAll();

    mf::core::database::LibraryRepository* repo_  = nullptr;
    mf::core::playback::QueueManager*      queue_ = nullptr;

    QList<mf::core::models::MusicFile>    tracks_;
    QList<mf::core::models::ArtistInfo>   artists_;
    QList<mf::core::models::AlbumInfo>    albums_;
    QList<mf::core::models::PlaylistInfo> playlists_;
};

} // namespace mf::app::viewmodels
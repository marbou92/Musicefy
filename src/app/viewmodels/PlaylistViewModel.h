// PlaylistViewModel.h
// Bindable view of a single PlaylistInfo. Exposes metadata as
// Q_PROPERTYs, forwards play / shuffle commands to the QueueManager,
// and supports local edit operations (add / remove / reorder /
// rename). The model state is held in-memory; persistence to the
// library / remote source is the responsibility of the caller
// (LibraryViewModel for local playlists; SubsonicSession for remote).

#pragma once

#include "../../core/models/MusicFile.h"
#include "../../core/models/PlaylistInfo.h"

#include <QList>
#include <QObject>
#include <QString>
#include <memory>

namespace mf::core::playback { class QueueManager; }
namespace mf::core::database { class LibraryRepository; }

namespace mf::app::viewmodels {

class PlaylistViewModel : public QObject {
    Q_OBJECT
    Q_PROPERTY(QString  id              READ id              NOTIFY infoChanged)
    Q_PROPERTY(QString  name            READ name            NOTIFY infoChanged)
    Q_PROPERTY(QString  description     READ description     NOTIFY infoChanged)
    Q_PROPERTY(QString  coverPath       READ coverPath       NOTIFY infoChanged)
    Q_PROPERTY(int      trackCount      READ trackCount      NOTIFY tracksChanged)
    Q_PROPERTY(qint64   totalDurationMs READ totalDurationMs NOTIFY tracksChanged)
    Q_PROPERTY(QList<mf::core::models::MusicFile> tracks READ tracks NOTIFY tracksChanged)
    Q_PROPERTY(bool     canPlay         READ canPlay         NOTIFY tracksChanged)
    Q_PROPERTY(bool     canEdit         READ canEdit         CONSTANT)

public:
    explicit PlaylistViewModel(mf::core::playback::QueueManager* queue,
                               mf::core::database::LibraryRepository* repo = nullptr,
                               QObject* parent = nullptr);
    ~PlaylistViewModel() override = default;

    void setInfo(const mf::core::models::PlaylistInfo& info);
    const mf::core::models::PlaylistInfo& info() const { return info_; }

    QString id()              const { return info_.id(); }
    QString name()            const { return info_.name(); }
    QString description()     const { return info_.description(); }
    QString coverPath()       const { return info_.coverPath(); }
    int     trackCount()      const { return info_.trackCount(); }
    qint64  totalDurationMs() const;
    QList<mf::core::models::MusicFile> tracks() const { return info_.tracks(); }
    bool    canPlay()         const { return !info_.tracks().isEmpty(); }
    bool    canEdit()         const { return !info_.youTubePlaylistId().isEmpty()
                                              ? false
                                              : true; }

    Q_INVOKABLE void playAll();
    Q_INVOKABLE void shufflePlay();
    Q_INVOKABLE void playTrackAt(int row);

    Q_INVOKABLE void addTrack(const mf::core::models::MusicFile& track);
    Q_INVOKABLE void removeTrackAt(int row);
    Q_INVOKABLE bool reorder(int from, int to);
    Q_INVOKABLE void rename(const QString& newName);
    Q_INVOKABLE void save();
    Q_INVOKABLE void loadFromDb(const QString& playlistId);
    Q_INVOKABLE void deletePlaylist();

    // Lookup helpers for views that need to render a row.
    Q_INVOKABLE mf::core::models::MusicFile trackAt(int row) const;
    Q_INVOKABLE int rowForTrackId(const QString& trackId) const;

signals:
    void infoChanged();
    void tracksChanged();
    void errorReported(const QString& message);
    void trackAdded(int row);
    void trackRemoved(int row);
    void trackMoved(int from, int to);
    void nameChanged(const QString& name);
    void saved();
    void deleted();

private:
    void recomputeDuration();

    mf::core::models::PlaylistInfo  info_;
    mf::core::playback::QueueManager* queue_ = nullptr;
    mf::core::database::LibraryRepository* repo_ = nullptr;
};

} // namespace mf::app::viewmodels

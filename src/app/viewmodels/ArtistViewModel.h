// ArtistViewModel.h
// Bindable view of a single ArtistInfo. Surfaces the artist's
// metadata, top tracks, and discography as Q_PROPERTYs and forwards
// play / shuffle / follow commands to the QueueManager. The view
// (ArtistView) binds to these properties and invokes the
// Q_INVOKABLE methods in response to UI events.
//
// The model is populated lazily: call loadById() to fetch from the
// BrowseService. While the fetch is in flight, isLoading is true.
// On completion the metadata + top-tracks + albums signals fire.
// If the service is null or the callback returns an empty result
// the view-model still emits errorReported so the view can show a
// placeholder.

#pragma once

#include "../../core/models/AlbumInfo.h"
#include "../../core/models/ArtistInfo.h"
#include "../../core/models/BrowseSection.h"
#include "../../core/models/MusicFile.h"

#include <QList>
#include <QObject>
#include <QString>

namespace mf::core::playback     { class QueueManager; }
namespace mf::core::services     { class BrowseService; class ToastService; }

namespace mf::app::viewmodels {

class ArtistViewModel : public QObject {
    Q_OBJECT
    Q_PROPERTY(QString id              READ id              NOTIFY infoChanged)
    Q_PROPERTY(QString name            READ name            NOTIFY infoChanged)
    Q_PROPERTY(QString coverPath       READ coverPath       NOTIFY infoChanged)
    Q_PROPERTY(QString description     READ description     NOTIFY infoChanged)
    Q_PROPERTY(qint64  subscriberCount READ subscriberCount NOTIFY infoChanged)
    Q_PROPERTY(bool    isFollowed      READ isFollowed      NOTIFY followedChanged)
    Q_PROPERTY(bool    isLoading       READ isLoading       NOTIFY loadingChanged)
    Q_PROPERTY(QList<mf::core::models::MusicFile> topTracks READ topTracks NOTIFY contentChanged)
    Q_PROPERTY(QList<mf::core::models::MusicFile> albums   READ albums   NOTIFY contentChanged)
    Q_PROPERTY(bool    canPlay         READ canPlay         NOTIFY contentChanged)

public:
    explicit ArtistViewModel(mf::core::services::BrowseService* browse,
                             mf::core::playback::QueueManager*  queue,
                             mf::core::services::ToastService*  toasts,
                             QObject* parent = nullptr);
    ~ArtistViewModel() override = default;

    void        setInfo(const mf::core::models::ArtistInfo& info);
    const mf::core::models::ArtistInfo& info() const { return info_; }

    QString id()              const { return info_.id(); }
    QString name()            const { return info_.name(); }
    QString coverPath()       const { return info_.coverPath(); }
    QString description()     const { return info_.description(); }
    qint64  subscriberCount() const { return info_.subscriberCount().value_or(0); }
    bool    isFollowed()      const { return info_.isFollowed(); }
    bool    isLoading()       const { return isLoading_; }

    QList<mf::core::models::MusicFile> topTracks() const { return topTracks_; }
    QList<mf::core::models::MusicFile> albums()    const { return albums_;    }
    bool    canPlay() const { return !topTracks_.isEmpty(); }

    Q_INVOKABLE void loadById(const QString& id);
    Q_INVOKABLE void playAll();
    Q_INVOKABLE void shufflePlay();
    Q_INVOKABLE void playTrackAt(int row);
    Q_INVOKABLE void toggleFollowed();

    Q_INVOKABLE void openAlbumAt(int index);
    Q_INVOKABLE int     albumCount()          const { return albums_.size(); }

signals:
    void infoChanged();
    void followedChanged();
    void contentChanged();
    void loadingChanged();
    void errorReported(const QString& message);
    void openAlbumRequested(const mf::core::models::AlbumInfo& album);

private:
    void setLoading(bool v);
    void flattenAlbums(const QList<mf::core::models::BrowseSection>& sections);
    void enqueueAndPlay(int startIndex, const QList<mf::core::models::MusicFile>& list);

    mf::core::services::BrowseService* browse_ = nullptr;
    mf::core::playback::QueueManager*  queue_  = nullptr;
    mf::core::services::ToastService*  toasts_ = nullptr;

    mf::core::models::ArtistInfo       info_;
    QList<mf::core::models::MusicFile> topTracks_;
    QList<mf::core::models::MusicFile> albums_;
    bool                               isLoading_ = false;
    QString                            pendingId_;
};

} // namespace mf::app::viewmodels

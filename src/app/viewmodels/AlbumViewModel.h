// AlbumViewModel.h
// Bindable view of a single AlbumInfo. Exposes the album's metadata
// as Q_PROPERTYs and forwards play / shuffle / save commands to the
// QueueManager. The view (AlbumView) binds to these properties and
// invokes the Q_INVOKABLE methods in response to UI events.

#pragma once

#include "../../core/models/AlbumInfo.h"
#include "../../core/models/MusicFile.h"

#include <QList>
#include <QObject>
#include <QString>

namespace mf::core::playback { class QueueManager; }

namespace mf::app::viewmodels {

class AlbumViewModel : public QObject {
    Q_OBJECT
    Q_PROPERTY(QString  id              READ id              NOTIFY infoChanged)
    Q_PROPERTY(QString  name            READ name            NOTIFY infoChanged)
    Q_PROPERTY(QString  artist          READ artist          NOTIFY infoChanged)
    Q_PROPERTY(QString  artistId        READ artistId        NOTIFY infoChanged)
    Q_PROPERTY(int      year            READ year            NOTIFY infoChanged)
    Q_PROPERTY(QString  coverPath       READ coverPath       NOTIFY infoChanged)
    Q_PROPERTY(QString  coverUrl        READ coverUrl        NOTIFY infoChanged)
    Q_PROPERTY(QString  genre           READ genre           NOTIFY infoChanged)
    Q_PROPERTY(QString  description     READ description     NOTIFY infoChanged)
    Q_PROPERTY(int      trackCount      READ trackCount      NOTIFY infoChanged)
    Q_PROPERTY(qint64   totalDurationMs READ totalDurationMs NOTIFY infoChanged)
    Q_PROPERTY(bool     isSaved         READ isSaved         NOTIFY savedChanged)
    Q_PROPERTY(QList<mf::core::models::MusicFile> tracks READ tracks NOTIFY infoChanged)
    Q_PROPERTY(bool     canPlay         READ canPlay         NOTIFY infoChanged)

public:
    explicit AlbumViewModel(mf::core::playback::QueueManager* queue,
                            QObject* parent = nullptr);
    ~AlbumViewModel() override = default;

    void setInfo(const mf::core::models::AlbumInfo& info);
    const mf::core::models::AlbumInfo& info() const { return info_; }

    QString id()              const { return info_.id(); }
    QString name()            const { return info_.name(); }
    QString artist()          const { return info_.artist(); }
    QString artistId()        const { return info_.artistId(); }
    int     year()            const { return info_.year(); }
    QString coverPath()       const { return info_.coverPath(); }
    QString coverUrl()        const;
    QString genre()           const { return info_.genre(); }
    QString description()     const { return info_.description(); }
    int     trackCount()      const { return info_.trackCount(); }
    qint64  totalDurationMs() const;
    bool    isSaved()         const { return info_.isSaved(); }
    QList<mf::core::models::MusicFile> tracks() const { return info_.tracks(); }
    bool    canPlay()         const { return !info_.tracks().isEmpty(); }

    Q_INVOKABLE void playAll();
    Q_INVOKABLE void shufflePlay();
    Q_INVOKABLE void playTrackAt(int row);
    Q_INVOKABLE void toggleSaved();

signals:
    void infoChanged();
    void savedChanged();
    void errorReported(const QString& message);

private:
    mf::core::models::AlbumInfo      info_;
    mf::core::playback::QueueManager* queue_ = nullptr;
};

} // namespace mf::app::viewmodels

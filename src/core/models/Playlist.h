#pragma once

#include <QList>
#include <QString>
#include <QStringList>

#include <chrono>

namespace mf::core::models {

// Plain DTO representing one Subsonic / Navidrome / Funkwhale playlist.
// Used by SubsonicSession::getPlaylists / createPlaylist / updatePlaylist
// and consumed by the playlist view models.
class Playlist {
public:
    QString id() const { return id_; }
    void setId(QString v) { id_ = std::move(v); }

    QString name() const { return name_; }
    void setName(QString v) { name_ = std::move(v); }

    int songCount() const { return songCount_; }
    void setSongCount(int v) { songCount_ = v; }

    std::chrono::seconds duration() const { return duration_; }
    void setDuration(std::chrono::seconds v) { duration_ = v; }

    bool isPublic() const { return isPublic_; }
    void setIsPublic(bool v) { isPublic_ = v; }

    QString owner() const { return owner_; }
    void setOwner(QString v) { owner_ = std::move(v); }

    QString coverArt() const { return coverArt_; }
    void setCoverArt(QString v) { coverArt_ = std::move(v); }

    QStringList trackIds() const { return trackIds_; }
    void setTrackIds(QStringList v) { trackIds_ = std::move(v); }

private:
    QString              id_;
    QString              name_;
    int                  songCount_  = 0;
    std::chrono::seconds duration_{0};
    bool                 isPublic_   = false;
    QString              owner_;
    QString              coverArt_;
    QStringList          trackIds_;
};

} // namespace mf::core::models

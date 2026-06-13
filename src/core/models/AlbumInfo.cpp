// AlbumInfo.cpp
// Out-of-line definitions for ArtistInfo that depend on AlbumInfo's full type.

#include "AlbumInfo.h"
#include "ArtistInfo.h"

namespace mf::core::models {

QList<AlbumInfo> ArtistInfo::albums() const {
    return albums_;
}

void ArtistInfo::setAlbums(QList<AlbumInfo> v) {
    albums_ = std::move(v);
}

QString AlbumInfo::coverUrl() const {
    if (!coverUrl_.isEmpty()) return coverUrl_;
    return coverPath_;
}

} // namespace mf::core::models

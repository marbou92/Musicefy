#include "AutoPlaylistInfo.h"

namespace mf::core::models {

QList<MusicFile> AutoPlaylistInfo::refresh() const {
    if (!refreshFn_) {
        return currentTracks_;
    }
    auto fresh = refreshFn_(*this);
    if (!fresh.isEmpty()) {
        const_cast<AutoPlaylistInfo*>(this)->currentTracks_ = fresh;
    }
    return currentTracks_;
}

} // namespace mf::core::models

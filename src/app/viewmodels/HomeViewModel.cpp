// HomeViewModel.cpp
// See header. All data comes from LibraryRepository; we cache and
// re-publish, plus expose QML/Widgets-friendly commands.

#include "HomeViewModel.h"

#include "../core/database/LibraryRepository.h"
#include "../core/playback/QueueManager.h"
#include "../core/services/LibraryService.h"
#include "../core/services/NavigationService.h"
#include "../core/services/ToastService.h"

#include <QDateTime>
#include <algorithm>

namespace mf::app::viewmodels {

using mf::core::database::LibraryRepository;
using mf::core::playback::QueueManager;
using mf::core::services::LibraryService;
using mf::core::services::ToastService;
using mf::core::services::NavigationService;
using mf::core::models::MusicFile;
using mf::core::models::PlaylistInfo;

HomeViewModel::HomeViewModel(LibraryRepository* repo,
                             QueueManager*      queue,
                             LibraryService*    libSvc,
                             ToastService*      toasts,
                             NavigationService* nav,
                             QObject*           parent)
    : QObject(parent)
    , repo_(repo)
    , queue_(queue)
    , libSvc_(libSvc)
    , toasts_(toasts)
    , nav_(nav)
{
    loadAll();

    // Auto-refresh when the library changes underneath us — scan
    // finishes, track is starred, etc. The LibraryService coalesces
    // these into a single tracksChanged signal.
    if (libSvc_) {
        connect(libSvc_, &LibraryService::tracksChanged,
                this, &HomeViewModel::onLibraryServiceTracksChanged);
    }
}

bool HomeViewModel::libraryIsEmpty() const {
    if (libSvc_ && libSvc_->folders().isEmpty()) return true;
    return recentlyPlayed_.isEmpty()
        && favourites_.isEmpty()
        && playlists_.isEmpty()
        && quickAccess_.isEmpty();
}

QString HomeViewModel::greeting() const {
    const int h = QDateTime::currentDateTime().time().hour();
    if (h < 5)  return QStringLiteral("Good night");
    if (h < 12) return QStringLiteral("Good morning");
    if (h < 18) return QStringLiteral("Good afternoon");
    return QStringLiteral("Good evening");
}

void HomeViewModel::refresh() {
    loadAll();
}

void HomeViewModel::onLibraryServiceTracksChanged() {
    loadAll();
}

void HomeViewModel::loadAll() {
    if (repo_) {
        const auto recent = repo_->recentlyPlayedTracks(kRecentlyPlayedMax);
        recentlyPlayed_ = recent;
        quickAccess_    = recent.mid(0, kQuickAccessMax);
        favourites_     = repo_->favouriteTracks();
        // Trim favourites list to carousel length.
        if (favourites_.size() > kFavouritesMax) {
            favourites_ = favourites_.mid(0, kFavouritesMax);
        }
        playlists_      = repo_->allPlaylists();

        // Most played: sort all tracks by play count descending.
        auto allTracks = repo_->allTracks();
        mostPlayed_ = allTracks;
        std::sort(mostPlayed_.begin(), mostPlayed_.end(),
            [](const MusicFile& a, const MusicFile& b) {
                return a.playCount() > b.playCount();
            });
        // Filter to tracks that have actually been played at least once.
        mostPlayed_.erase(
            std::remove_if(mostPlayed_.begin(), mostPlayed_.end(),
                [](const MusicFile& t) { return t.playCount() <= 0; }),
            mostPlayed_.end());
        if (mostPlayed_.size() > kMostPlayedMax) {
            mostPlayed_ = mostPlayed_.mid(0, kMostPlayedMax);
        }

        // Recently added: sort by dateAdded descending.
        recentlyAdded_ = allTracks;
        std::sort(recentlyAdded_.begin(), recentlyAdded_.end(),
            [](const MusicFile& a, const MusicFile& b) {
                auto da = a.dateAdded().value_or(QDateTime());
                auto db = b.dateAdded().value_or(QDateTime());
                return da > db;
            });
        // Filter to tracks that have a dateAdded.
        recentlyAdded_.erase(
            std::remove_if(recentlyAdded_.begin(), recentlyAdded_.end(),
                [](const MusicFile& t) { return !t.dateAdded().has_value(); }),
            recentlyAdded_.end());
        if (recentlyAdded_.size() > kRecentlyAddedMax) {
            recentlyAdded_ = recentlyAdded_.mid(0, kRecentlyAddedMax);
        }
    } else {
        quickAccess_.clear();
        recentlyPlayed_.clear();
        favourites_.clear();
        playlists_.clear();
        mostPlayed_.clear();
        recentlyAdded_.clear();
    }
    emit contentChanged();
}

void HomeViewModel::enqueueAndPlay(int startIndex,
                                   const QList<MusicFile>& list) {
    if (!queue_ || list.isEmpty()) return;
    if (startIndex < 0 || startIndex >= list.size()) return;
    queue_->clear();
    queue_->enqueueMany(list);
    queue_->setCurrentIndex(startIndex);
}

int HomeViewModel::findTrackIndex(const QString& filePath) const {
    if (!repo_ || filePath.isEmpty()) return -1;
    const auto all = repo_->allTracks();
    for (int i = 0; i < all.size(); ++i) {
        if (all[i].filePath() == filePath) return i;
    }
    return -1;
}

void HomeViewModel::playTrack(const QString& filePath) {
    if (!queue_ || !repo_) return;
    const int idx = findTrackIndex(filePath);
    if (idx < 0) {
        if (toasts_) toasts_->showWarning(
            QStringLiteral("Track not in library"),
            QStringLiteral("This track isn't indexed yet — try a rescan."));
        return;
    }
    const auto all = repo_->allTracks();
    queue_->clear();
    queue_->enqueueMany(all);
    queue_->setCurrentIndex(idx);
}

void HomeViewModel::playAllFromQuickAccess() {
    enqueueAndPlay(0, quickAccess_);
}

void HomeViewModel::playAllFromRecentlyPlayed() {
    enqueueAndPlay(0, recentlyPlayed_);
}

void HomeViewModel::playAllFromFavourites() {
    enqueueAndPlay(0, favourites_);
}

void HomeViewModel::playAllFromMostPlayed() {
    enqueueAndPlay(0, mostPlayed_);
}

void HomeViewModel::playAllFromRecentlyAdded() {
    enqueueAndPlay(0, recentlyAdded_);
}

void HomeViewModel::toggleFavourite(const QString& filePath) {
    if (!repo_) return;
    repo_->toggleFavourite(filePath);
    // The repo doesn't emit a signal of its own; reload directly so
    // the favourites carousel + library counts stay in sync.
    loadAll();
}

void HomeViewModel::openPlaylistAt(int index) {
    if (!nav_) return;
    if (index < 0 || index >= playlists_.size()) return;
    nav_->requestPlaylist(playlists_[index]);
}

} // namespace mf::app::viewmodels

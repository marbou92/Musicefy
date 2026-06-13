// DiscoverViewModel.cpp
// See header. Refresh triggers three async lookups (charts, moods,
// new releases) and counts the in-flight callbacks so loading
// clears only after the last one resolves.

#include "DiscoverViewModel.h"

#include "../core/playback/QueueManager.h"
#include "../core/services/BrowseService.h"
#include "../core/services/ToastService.h"

namespace mf::app::viewmodels {

using mf::core::models::BrowseSection;
using mf::core::models::MusicFile;
using mf::core::playback::QueueManager;
using mf::core::services::BrowseService;
using mf::core::services::ToastService;

DiscoverViewModel::DiscoverViewModel(BrowseService* browse,
                                     QueueManager*  queue,
                                     ToastService*  toasts,
                                     QObject*       parent)
    : QObject(parent)
    , browse_(browse)
    , queue_(queue)
    , toasts_(toasts)
{
}

void DiscoverViewModel::setLoading(bool v) {
    if (isLoading_ == v) return;
    isLoading_ = v;
    emit loadingChanged();
}

bool DiscoverViewModel::takeIfChanged(Feed which,
                                      const QList<BrowseSection>& incoming) {
    QList<BrowseSection>& target =
        (which == Feed::Charts)      ? charts_ :
        (which == Feed::Moods)       ? moods_  :
                                       newReleases_;
    if (target == incoming) return false;
    target = incoming;
    emit contentChanged();
    return true;
}

void DiscoverViewModel::refresh() {
    if (!browse_) {
        emit errorReported(QStringLiteral("Browse service unavailable"));
        return;
    }
    pendingSource_.clear();
    setLoading(true);
    pendingFeeds_ = 3;

    browse_->loadCharts(QString(), [this](QList<BrowseSection> sections) {
        takeIfChanged(Feed::Charts, sections);
        if (--pendingFeeds_ == 0) {
            hasFetched_ = true;
            setLoading(false);
        }
    });
    browse_->loadMoodsAndGenres(QString(), [this](QList<BrowseSection> sections) {
        takeIfChanged(Feed::Moods, sections);
        if (--pendingFeeds_ == 0) {
            hasFetched_ = true;
            setLoading(false);
        }
    });
    browse_->loadNewReleases(QString(), [this](QList<BrowseSection> sections) {
        takeIfChanged(Feed::NewReleases, sections);
        if (--pendingFeeds_ == 0) {
            hasFetched_ = true;
            setLoading(false);
        }
    });
}

void DiscoverViewModel::refreshIfStale() {
    if (!hasFetched_) {
        refresh();
    }
}

void DiscoverViewModel::playAllIn(const QList<BrowseSection>& feed,
                                  int sectionIndex,
                                  bool shuffle) {
    if (!queue_) { emit errorReported(QStringLiteral("Queue unavailable")); return; }
    if (sectionIndex < 0 || sectionIndex >= feed.size()) return;
    const QList<MusicFile>& items = feed.at(sectionIndex).items();
    if (items.isEmpty()) return;
    if (shuffle && !queue_->isShuffle()) queue_->setShuffle(true);
    queue_->clear();
    queue_->enqueueMany(items);
    queue_->setCurrentIndex(0);
}

void DiscoverViewModel::playInSection(const QList<BrowseSection>& feed,
                                      int sectionIndex,
                                      int trackIndex) {
    if (!queue_) { emit errorReported(QStringLiteral("Queue unavailable")); return; }
    if (sectionIndex < 0 || sectionIndex >= feed.size()) return;
    const QList<MusicFile>& items = feed.at(sectionIndex).items();
    if (trackIndex < 0 || trackIndex >= items.size()) return;
    const MusicFile& t = items.at(trackIndex);
    if (t.filePath().isEmpty() && t.sourceUri().isEmpty()) return;
    queue_->clear();
    queue_->enqueue(t);
    queue_->setCurrentIndex(0);
}

void DiscoverViewModel::playAllFromCharts(int sectionIndex)      { playAllIn(charts_,      sectionIndex, false); }
void DiscoverViewModel::playAllFromMoods(int sectionIndex)       { playAllIn(moods_,       sectionIndex, false); }
void DiscoverViewModel::playAllFromNewReleases(int sectionIndex) { playAllIn(newReleases_, sectionIndex, false); }
void DiscoverViewModel::shuffleFromCharts(int sectionIndex)      { playAllIn(charts_,      sectionIndex, true);  }
void DiscoverViewModel::shuffleFromMoods(int sectionIndex)       { playAllIn(moods_,       sectionIndex, true);  }
void DiscoverViewModel::shuffleFromNewReleases(int sectionIndex) { playAllIn(newReleases_, sectionIndex, true);  }

void DiscoverViewModel::playTrackInCharts(int sectionIndex, int trackIndex) {
    playInSection(charts_, sectionIndex, trackIndex);
}
void DiscoverViewModel::playTrackInMoods(int sectionIndex, int trackIndex) {
    playInSection(moods_, sectionIndex, trackIndex);
}
void DiscoverViewModel::playTrackInNewReleases(int sectionIndex, int trackIndex) {
    playInSection(newReleases_, sectionIndex, trackIndex);
}

} // namespace mf::app::viewmodels

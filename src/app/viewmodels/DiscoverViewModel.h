// DiscoverViewModel.h
// Bindable view of the Discover page. Surfaces the three curated
// feeds (charts, moods, new releases) as Q_PROPERTYs and exposes
// per-feed play / shuffle commands. The model is populated lazily
// from BrowseService::loadCharts / loadMoodsAndGenres /
// loadNewReleases. While the fetch is in flight, isLoading is true.
//
// Each feed is a QList<BrowseSection>; the view renders one
// carousel per section.

#pragma once

#include "../../core/models/BrowseSection.h"

#include <QList>
#include <QObject>
#include <QString>

namespace mf::core::playback { class QueueManager; }
namespace mf::core::services { class BrowseService; class ToastService; }

namespace mf::app::viewmodels {

class DiscoverViewModel : public QObject {
    Q_OBJECT
    Q_PROPERTY(bool isLoading READ isLoading NOTIFY loadingChanged)
    Q_PROPERTY(bool hasContent READ hasContent NOTIFY contentChanged)
    Q_PROPERTY(QList<mf::core::models::BrowseSection> charts        READ charts        NOTIFY contentChanged)
    Q_PROPERTY(QList<mf::core::models::BrowseSection> moods        READ moods        NOTIFY contentChanged)
    Q_PROPERTY(QList<mf::core::models::BrowseSection> newReleases   READ newReleases   NOTIFY contentChanged)

public:
    explicit DiscoverViewModel(mf::core::services::BrowseService* browse,
                               mf::core::playback::QueueManager*  queue,
                               mf::core::services::ToastService*  toasts,
                               QObject* parent = nullptr);
    ~DiscoverViewModel() override = default;

    bool isLoading() const { return isLoading_; }
    bool hasContent() const {
        return !charts_.isEmpty() || !moods_.isEmpty() || !newReleases_.isEmpty();
    }

    QList<mf::core::models::BrowseSection> charts()      const { return charts_;      }
    QList<mf::core::models::BrowseSection> moods()      const { return moods_;      }
    QList<mf::core::models::BrowseSection> newReleases() const { return newReleases_; }

    Q_INVOKABLE void refresh();
    Q_INVOKABLE void refreshIfStale();

    Q_INVOKABLE void playAllFromCharts(int sectionIndex);
    Q_INVOKABLE void playAllFromMoods(int sectionIndex);
    Q_INVOKABLE void playAllFromNewReleases(int sectionIndex);
    Q_INVOKABLE void shuffleFromCharts(int sectionIndex);
    Q_INVOKABLE void shuffleFromMoods(int sectionIndex);
    Q_INVOKABLE void shuffleFromNewReleases(int sectionIndex);

    Q_INVOKABLE void playTrackInCharts(int sectionIndex, int trackIndex);
    Q_INVOKABLE void playTrackInMoods(int sectionIndex, int trackIndex);
    Q_INVOKABLE void playTrackInNewReleases(int sectionIndex, int trackIndex);

    Q_INVOKABLE int chartCount()        const { return charts_.size();      }
    Q_INVOKABLE int moodCount()         const { return moods_.size();      }
    Q_INVOKABLE int newReleaseCount()   const { return newReleases_.size(); }

signals:
    void contentChanged();
    void loadingChanged();
    void errorReported(const QString& message);

private:
    enum class Feed { Charts, Moods, NewReleases };

    void setLoading(bool v);
    bool takeIfChanged(Feed which, const QList<mf::core::models::BrowseSection>& incoming);
    void playAllIn(const QList<mf::core::models::BrowseSection>& feed,
                   int sectionIndex,
                   bool shuffle);
    void playInSection(const QList<mf::core::models::BrowseSection>& feed,
                       int sectionIndex,
                       int trackIndex);

    mf::core::services::BrowseService* browse_ = nullptr;
    mf::core::playback::QueueManager*  queue_  = nullptr;
    mf::core::services::ToastService*  toasts_ = nullptr;

    QList<mf::core::models::BrowseSection> charts_;
    QList<mf::core::models::BrowseSection> moods_;
    QList<mf::core::models::BrowseSection> newReleases_;

    bool    isLoading_ = false;
    int     pendingFeeds_ = 0;
    bool    hasFetched_   = false;
    QString pendingSource_;
};

} // namespace mf::app::viewmodels

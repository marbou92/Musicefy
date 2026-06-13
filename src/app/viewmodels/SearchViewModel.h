// SearchViewModel.h
// Search state machine with dual Local/Online mode, autocomplete
// suggestions, URL detection, and search history integration.
//
// States: Idle → Suggestions → Searching → Results
//   Idle:          empty query, shows recent search history
//   Suggestions:   debounced query, shows autocomplete (history + YouTube + library)
//   Searching:     actively searching (local + online sources)
//   Results:       search complete, shows categorized result groups

#pragma once

#include "../../core/models/MusicFile.h"
#include "../../core/models/SearchHistory.h"
#include "../../core/models/SearchResultGroup.h"
#include "../../core/models/SearchSourceMode.h"
#include "../../core/models/ArtistInfo.h"
#include "../../core/models/AlbumInfo.h"
#include "../../core/models/PlaylistInfo.h"

#include <QObject>
#include <QString>
#include <QStringList>
#include <QList>
#include <QTimer>
#include <memory>

namespace mf::core::interfaces   { class IStreamingSourceManager; class IMusicSourceSession; }
namespace mf::core::database     { class LibraryRepository; }
namespace mf::core::playback     { class QueueManager; }
namespace mf::core::services     { class SearchHistoryService; class NavigationService; }
namespace mf::app::viewmodels    { class LibraryViewModel; }

namespace mf::app::viewmodels {

enum class SearchState { Idle, Suggestions, Searching, Results };

enum class SearchResultFilter { All, Songs, Albums, Artists, Playlists };

class SearchViewModel : public QObject {
    Q_OBJECT
    using SearchSourceMode = mf::core::models::SearchSourceMode;
    Q_PROPERTY(int    state          READ state          NOTIFY stateChanged)
    Q_PROPERTY(QString query        READ query          WRITE setQuery NOTIFY queryChanged)
    Q_PROPERTY(int    sourceMode    READ sourceMode     NOTIFY sourceModeChanged)
    Q_PROPERTY(QStringList suggestions       READ suggestions       NOTIFY suggestionsChanged)
    Q_PROPERTY(QList<mf::core::models::SearchHistory> recentSearches READ recentSearches NOTIFY recentSearchesChanged)
    Q_PROPERTY(QList<mf::core::models::SearchResultGroup> resultGroups READ resultGroups NOTIFY resultsChanged)
    Q_PROPERTY(bool   isFromLink    READ isFromLink     NOTIFY fromLinkChanged)
    Q_PROPERTY(bool   hasResults    READ hasResults     NOTIFY stateChanged)
    Q_PROPERTY(bool   isSearching   READ isSearching    NOTIFY stateChanged)
    Q_PROPERTY(bool   hasError      READ hasError       NOTIFY errorChanged)
    Q_PROPERTY(QString errorMessage READ errorMessage  NOTIFY errorChanged)
    Q_PROPERTY(int    selectedFilter READ selectedFilter NOTIFY filterChanged)
    Q_PROPERTY(QString topResultTitle   READ topResultTitle   NOTIFY resultsChanged)
    Q_PROPERTY(QString topResultSubtitle READ topResultSubtitle NOTIFY resultsChanged)
    Q_PROPERTY(QString topResultType    READ topResultType    NOTIFY resultsChanged)
    Q_PROPERTY(int    totalResultCount  READ totalResultCount  NOTIFY resultsChanged)

public:
    SearchViewModel(LibraryViewModel*              libVm,
                    mf::core::interfaces::IStreamingSourceManager* sourceMgr,
                    mf::core::services::SearchHistoryService*      history,
                    mf::core::playback::QueueManager*             queue,
                    mf::core::services::NavigationService*        nav,
                    QObject* parent = nullptr);
    ~SearchViewModel() override;

    int    state()       const { return static_cast<int>(state_); }
    QString query()      const { return query_; }
    int    sourceMode()  const { return static_cast<int>(sourceMode_); }
    QStringList suggestions() const { return suggestions_; }
    QList<mf::core::models::SearchHistory> recentSearches() const { return recentSearches_; }
    QList<mf::core::models::SearchResultGroup> resultGroups() const { return resultGroups_; }
    bool   isFromLink()  const { return isFromLink_; }
    bool   hasResults()  const { return state_ == SearchState::Results && !resultGroups_.isEmpty(); }
    bool   isSearching() const { return state_ == SearchState::Searching; }
    bool   hasError()    const { return state_ == SearchState::Results && !errorMessage_.isEmpty(); }
    QString errorMessage() const { return errorMessage_; }
    int    selectedFilter() const { return static_cast<int>(selectedFilter_); }
    QString topResultTitle()   const { return topResultTitle_; }
    QString topResultSubtitle() const { return topResultSubtitle_; }
    QString topResultType()    const { return topResultType_; }
    int    totalResultCount()  const;

    /// The currently visible results after applying the active filter.
    QList<mf::core::models::MusicFile> flatResults() const;

    Q_INVOKABLE void setQuery(const QString& text);
    Q_INVOKABLE void search();
    Q_INVOKABLE void clearQuery();
    Q_INVOKABLE void setSourceMode(int mode);
    Q_INVOKABLE void selectFilter(int filter);
    Q_INVOKABLE void selectSuggestion(const QString& suggestion);
    Q_INVOKABLE void selectHistoryItem(int index);
    Q_INVOKABLE void clearHistory();
    Q_INVOKABLE void playTrack(const QString& filePath);
    Q_INVOKABLE void navigateToArtist(const QString& artistName);
    Q_INVOKABLE void navigateToAlbum(const QString& albumName, const QString& artistName);

signals:
    void stateChanged();
    void queryChanged();
    void sourceModeChanged();
    void suggestionsChanged();
    void recentSearchesChanged();
    void resultsChanged();
    void fromLinkChanged();
    void errorChanged();
    void filterChanged();

private slots:
    void onDebounceTimeout();
    void onLibraryChanged();

private:
    void setState(SearchState s);
    void fetchSuggestions();
    void searchLocal();
    void searchOnline();
    void handleUrl(const QString& url);
    void applyFilter();
    void updateTopResult();
    void loadRecentSearches();

    static bool matches(const QString& hay, const QString& needle);

    LibraryViewModel*                          libVm_     = nullptr;
    mf::core::interfaces::IStreamingSourceManager* sourceMgr_ = nullptr;
    mf::core::services::SearchHistoryService*  history_   = nullptr;
    mf::core::playback::QueueManager*          queue_     = nullptr;
    mf::core::services::NavigationService*     nav_       = nullptr;

    QTimer* debounce_ = nullptr;
    SearchState           state_ = SearchState::Idle;
    SearchSourceMode      sourceMode_ = static_cast<SearchSourceMode>(0);
    SearchResultFilter    selectedFilter_ = SearchResultFilter::All;
    QString               query_;
    bool                  isFromLink_ = false;
    QString               errorMessage_;
    QStringList           suggestions_;
    QList<mf::core::models::SearchHistory>    recentSearches_;
    QList<mf::core::models::SearchResultGroup> resultGroups_;
    QList<mf::core::models::MusicFile>         allLocalResults_;
    QList<mf::core::models::MusicFile>         allOnlineResults_;
    QString               topResultTitle_;
    QString               topResultSubtitle_;
    QString               topResultType_;

    int pendingOnlineSearches_ = 0;
};

} // namespace mf::app::viewmodels

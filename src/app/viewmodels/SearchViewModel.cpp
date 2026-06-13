// SearchViewModel.cpp

#include "SearchViewModel.h"

#include "../../core/interfaces/IStreamingSourceManager.h"
#include "../../core/interfaces/IMusicSourceSession.h"
#include "../../core/models/StreamingSource.h"
#include "../../core/database/LibraryRepository.h"
#include "../../core/playback/QueueManager.h"
#include "../../core/services/SearchHistoryService.h"
#include "../../core/services/NavigationService.h"
#include "../../core/sources/youtube/YouTubeUrlParser.h"
#include "LibraryViewModel.h"

#include <QSet>
#include <algorithm>

namespace mf::app::viewmodels {

using mf::core::models::SearchSourceMode;

// ──────────────────────────────────────────────────────────────────
SearchViewModel::SearchViewModel(LibraryViewModel*              libVm,
                                 mf::core::interfaces::IStreamingSourceManager* sourceMgr,
                                 mf::core::services::SearchHistoryService*      history,
                                 mf::core::playback::QueueManager*             queue,
                                 mf::core::services::NavigationService*        nav,
                                 QObject* parent)
    : QObject(parent)
    , libVm_(libVm)
    , sourceMgr_(sourceMgr)
    , history_(history)
    , queue_(queue)
    , nav_(nav)
{
    debounce_ = new QTimer(this);
    debounce_->setSingleShot(true);
    debounce_->setInterval(300);
    connect(debounce_, &QTimer::timeout, this, &SearchViewModel::onDebounceTimeout);

    connect(libVm_, &LibraryViewModel::libraryChanged,
            this, &SearchViewModel::onLibraryChanged);

    loadRecentSearches();
}

// ──────────────────────────────────────────────────────────────────
SearchViewModel::~SearchViewModel() = default;

// ──────────────────────────────────────────────────────────────────
int SearchViewModel::totalResultCount() const
{
    int total = 0;
    for (const auto& g : resultGroups_)
        total += g.results().size();
    return total;
}

// ──────────────────────────────────────────────────────────────────
QList<mf::core::models::MusicFile> SearchViewModel::flatResults() const
{
    QList<mf::core::models::MusicFile> out;
    if (selectedFilter_ == SearchResultFilter::All) {
        for (const auto& g : resultGroups_)
            out.append(g.results());
        return out;
    }

    for (const auto& g : resultGroups_) {
        bool include = false;
        switch (selectedFilter_) {
        case SearchResultFilter::Songs:
            include = (g.mode() == mf::core::models::SearchSourceMode::Local ||
                       g.mode() == SearchSourceMode::YouTube ||
                       g.mode() == SearchSourceMode::Subsonic);
            break;
        case SearchResultFilter::Albums:
            include = g.header().contains(QStringLiteral("Album"), Qt::CaseInsensitive);
            break;
        case SearchResultFilter::Artists:
            include = g.header().contains(QStringLiteral("Artist"), Qt::CaseInsensitive);
            break;
        case SearchResultFilter::Playlists:
            include = g.header().contains(QStringLiteral("Playlist"), Qt::CaseInsensitive);
            break;
        }
        if (include)
            out.append(g.results());
    }
    return out;
}

// ──────────────────────────────────────────────────────────────────
void SearchViewModel::setQuery(const QString& text)
{
    if (query_ == text) return;
    query_ = text;
    emit queryChanged();

    // Check for YouTube URL
    using namespace mf::core::sources::youtube;
    const auto parsed = YouTubeUrlParser::parse(query_);
    if (parsed.type != UrlType::Unknown) {
        debounce_->stop();
        if (!isFromLink_) {
            isFromLink_ = true;
            emit fromLinkChanged();
        }
        suggestions_.clear();
        emit suggestionsChanged();
        return;
    }

    if (isFromLink_) {
        isFromLink_ = false;
        emit fromLinkChanged();
    }

    // Empty query → idle
    if (query_.trimmed().isEmpty()) {
        debounce_->stop();
        setState(SearchState::Idle);
        suggestions_.clear();
        emit suggestionsChanged();
        resultGroups_.clear();
        allLocalResults_.clear();
        allOnlineResults_.clear();
        emit resultsChanged();
        return;
    }

    // Restart debounce timer
    debounce_->stop();
    debounce_->start();
}

// ──────────────────────────────────────────────────────────────────
void SearchViewModel::search()
{
    if (query_.trimmed().isEmpty()) return;
    if (state_ == SearchState::Searching) return;

    debounce_->stop();

    // URL handling
    using namespace mf::core::sources::youtube;
    const auto parsed = YouTubeUrlParser::parse(query_);
    if (parsed.type != UrlType::Unknown && parsed.type != UrlType::Unknown) {
        handleUrl(query_.trimmed());
        return;
    }

    // Record to history
    if (history_) {
        const QString modeStr = (sourceMode_ == mf::core::models::SearchSourceMode::All)
                                    ? QStringLiteral("online") : QStringLiteral("local");
        history_->recordSearch(query_, modeStr, 0);
    }

    setState(SearchState::Searching);
    allLocalResults_.clear();
    allOnlineResults_.clear();
    resultGroups_.clear();

    // Local search
    searchLocal();

    // Online search
    if (sourceMode_ == mf::core::models::SearchSourceMode::All) {
        searchOnline();
    } else {
        // Local only — finish immediately
        applyFilter();
        setState(SearchState::Results);
        loadRecentSearches();
    }
}

// ──────────────────────────────────────────────────────────────────
void SearchViewModel::clearQuery()
{
    debounce_->stop();
    query_.clear();
    emit queryChanged();
    if (isFromLink_) {
        isFromLink_ = false;
        emit fromLinkChanged();
    }
    setState(SearchState::Idle);
    suggestions_.clear();
    emit suggestionsChanged();
    resultGroups_.clear();
    allLocalResults_.clear();
    allOnlineResults_.clear();
    emit resultsChanged();
}

// ──────────────────────────────────────────────────────────────────
void SearchViewModel::setSourceMode(int mode)
{
    const auto m = static_cast<SearchSourceMode>(mode);
    if (sourceMode_ == m) return;
    sourceMode_ = m;
    emit sourceModeChanged();

    // Re-search if we have results
    if (state_ == SearchState::Results && !query_.trimmed().isEmpty()) {
        search();
    }
}

// ──────────────────────────────────────────────────────────────────
void SearchViewModel::selectFilter(int filter)
{
    const auto f = static_cast<SearchResultFilter>(filter);
    if (selectedFilter_ == f) return;
    selectedFilter_ = f;
    emit filterChanged();
    applyFilter();
}

// ──────────────────────────────────────────────────────────────────
void SearchViewModel::selectSuggestion(const QString& suggestion)
{
    query_ = suggestion;
    emit queryChanged();
    search();
}

// ──────────────────────────────────────────────────────────────────
void SearchViewModel::selectHistoryItem(int index)
{
    if (index < 0 || index >= recentSearches_.size()) return;
    const auto& item = recentSearches_[index];
    query_ = item.query();
    emit queryChanged();

    // Restore source mode
    if (item.sourceType() == QStringLiteral("online")) {
        setSourceMode(static_cast<int>(mf::core::models::SearchSourceMode::All));
    } else {
        setSourceMode(static_cast<int>(mf::core::models::SearchSourceMode::Local));
    }

    search();
}

// ──────────────────────────────────────────────────────────────────
void SearchViewModel::clearHistory()
{
    if (history_) {
        history_->clearAll();
        loadRecentSearches();
    }
}

// ──────────────────────────────────────────────────────────────────
void SearchViewModel::playTrack(const QString& filePath)
{
    queue_->clear();
    const auto tracks = libVm_->tracks();
    for (int i = 0; i < tracks.size(); ++i) {
        if (tracks[i].filePath() == filePath) {
            queue_->enqueueMany(tracks);
            queue_->setCurrentIndex(i);
            return;
        }
    }
}

// ──────────────────────────────────────────────────────────────────
void SearchViewModel::navigateToArtist(const QString& artistName)
{
    mf::core::models::ArtistInfo info;
    info.setName(artistName);
    nav_->requestArtist(info);
}

// ──────────────────────────────────────────────────────────────────
void SearchViewModel::navigateToAlbum(const QString& albumName, const QString& artistName)
{
    mf::core::models::AlbumInfo info;
    info.setName(albumName);
    info.setArtist(artistName);
    nav_->requestAlbum(info);
}

// ──────────────────────────────────────────────────────────────────
// Private
// ──────────────────────────────────────────────────────────────────

void SearchViewModel::onDebounceTimeout()
{
    if (query_.trimmed().isEmpty()) return;
    setState(SearchState::Suggestions);
    fetchSuggestions();
}

// ──────────────────────────────────────────────────────────────────
void SearchViewModel::onLibraryChanged()
{
    if (state_ == SearchState::Results && !query_.trimmed().isEmpty()) {
        searchLocal();
        applyFilter();
    }
}

// ──────────────────────────────────────────────────────────────────
void SearchViewModel::setState(SearchState s)
{
    if (state_ == s) return;
    state_ = s;
    emit stateChanged();
}

// ──────────────────────────────────────────────────────────────────
void SearchViewModel::fetchSuggestions()
{
    QStringList out;
    QSet<QString> seen;

    // 1) Search history prefix matches
    if (history_) {
        const auto historyItems = history_->suggestions(query_, 5);
        for (const auto& h : historyItems) {
            const QString q = h.query();
            if (!seen.contains(q.toLower())) {
                seen.insert(q.toLower());
                out.append(q);
            }
        }
    }

    // 2) Local library matches (top 3)
    const auto tracks = libVm_->tracks();
    int localCount = 0;
    for (const auto& t : tracks) {
        if (localCount >= 3) break;
        const QString label = QStringLiteral("%1 — %2").arg(t.title(), t.artist());
        if (matches(label, query_) && !seen.contains(label.toLower())) {
            seen.insert(label.toLower());
            out.append(label);
            ++localCount;
        }
    }

    // Cap at 10
    while (out.size() > 10)
        out.removeLast();

    suggestions_ = out;
    emit suggestionsChanged();
}

// ──────────────────────────────────────────────────────────────────
void SearchViewModel::searchLocal()
{
    allLocalResults_.clear();
    const auto tracks = libVm_->tracks();
    for (const auto& t : tracks) {
        if (matches(t.title(), query_) ||
            matches(t.artist(), query_) ||
            matches(t.album(), query_)) {
            allLocalResults_.append(t);
        }
    }
}

// ──────────────────────────────────────────────────────────────────
void SearchViewModel::searchOnline()
{
    if (!sourceMgr_) return;

    const auto sources = sourceMgr_->allSources();
    int pending = 0;

    for (const auto& source : sources) {
        if (!source.isConnected()) continue;
        if (source.type() != QStringLiteral("YouTube") &&
            source.type() != QStringLiteral("Subsonic")) {
            continue;
        }

        auto session = sourceMgr_->createSession(source.id());
        if (!session) continue;

        ++pending;
        // Move session ownership into a shared_ptr for lambda capture
        auto* sessionRaw = session.release();
        auto sessionPtr = std::shared_ptr<mf::core::interfaces::IMusicSourceSession>(sessionRaw);

        sessionPtr->searchTracks(
            query_, 20,
            [this, sessionPtr](const QList<mf::core::models::MusicFile>& results) {
                QMetaObject::invokeMethod(this, [this, results]() {
                    allOnlineResults_.append(results);
                    if (--pendingOnlineSearches_ <= 0) {
                        applyFilter();
                        setState(SearchState::Results);
                        loadRecentSearches();
                    }
                });
            },
            [this, sessionPtr](const QString& /*error*/) {
                QMetaObject::invokeMethod(this, [this]() {
                    if (--pendingOnlineSearches_ <= 0) {
                        applyFilter();
                        setState(SearchState::Results);
                        loadRecentSearches();
                    }
                });
            }
        );
    }

    pendingOnlineSearches_ = pending;
}

// ──────────────────────────────────────────────────────────────────
void SearchViewModel::handleUrl(const QString& url)
{
    using namespace mf::core::sources::youtube;
    const auto parsed = YouTubeUrlParser::parse(url);

    setState(SearchState::Searching);

    switch (parsed.type) {
    case UrlType::Video: {
        mf::core::models::MusicFile track;
        track.setTitle(parsed.videoId);
        track.setYouTubeVideoId(parsed.videoId);
        track.setSourceType(QStringLiteral("youtube"));
        queue_->clear();
        QList<mf::core::models::MusicFile> list;
        list.append(track);
        queue_->enqueueMany(list);
        queue_->setCurrentIndex(0);
        setState(SearchState::Results);
        break;
    }
    case UrlType::Playlist: {
        mf::core::models::PlaylistInfo pl;
        pl.setName(QStringLiteral("YouTube Playlist"));
        pl.setYouTubePlaylistId(parsed.playlistId);
        pl.setSourceType(QStringLiteral("youtube"));
        nav_->requestPlaylist(pl);
        setState(SearchState::Results);
        break;
    }
    case UrlType::Artist: {
        mf::core::models::ArtistInfo artist;
        artist.setName(QStringLiteral("YouTube Artist"));
        artist.setId(parsed.browseId);
        artist.setYouTubeChannelId(parsed.browseId);
        artist.setSourceType(QStringLiteral("youtube"));
        nav_->requestArtist(artist);
        setState(SearchState::Results);
        break;
    }
    case UrlType::Album: {
        mf::core::models::AlbumInfo album;
        album.setName(QStringLiteral("YouTube Album"));
        album.setId(parsed.browseId);
        album.setYouTubeAlbumId(parsed.browseId);
        album.setSourceType(QStringLiteral("youtube"));
        nav_->requestAlbum(album);
        setState(SearchState::Results);
        break;
    }
    case UrlType::Unknown:
        errorMessage_ = QStringLiteral("Unrecognized URL");
        emit errorChanged();
        setState(SearchState::Results);
        break;
    }
}

// ──────────────────────────────────────────────────────────────────
void SearchViewModel::applyFilter()
{
    resultGroups_.clear();

    // Local songs group
    if (!allLocalResults_.isEmpty()) {
        mf::core::models::SearchResultGroup group;
        group.setSourceType(QStringLiteral("local"));
        group.setMode(mf::core::models::SearchSourceMode::Local);
        group.setHeader(QStringLiteral("Songs"));
        group.setResults(allLocalResults_);
        resultGroups_.append(group);
    }

    // Online results by source type
    QHash<QString, QList<mf::core::models::MusicFile>> bySource;
    for (const auto& t : allOnlineResults_) {
        bySource[t.sourceType()].append(t);
    }

    for (auto it = bySource.begin(); it != bySource.end(); ++it) {
        mf::core::models::SearchResultGroup group;
        group.setSourceType(it.key());
        group.setMode(mf::core::models::SearchSourceMode::All);
        group.setHeader(it.key().toUpper() + QStringLiteral(" Songs"));
        group.setResults(it.value());
        resultGroups_.append(group);
    }

    selectedFilter_ = SearchResultFilter::All;
    emit filterChanged();
    emit resultsChanged();
    updateTopResult();
}

// ──────────────────────────────────────────────────────────────────
void SearchViewModel::updateTopResult()
{
    topResultTitle_.clear();
    topResultSubtitle_.clear();
    topResultType_.clear();

    if (resultGroups_.isEmpty()) return;

    // Use first result from first group as top result
    const auto& first = resultGroups_.first();
    if (!first.results().isEmpty()) {
        const auto& t = first.results().first();
        topResultTitle_ = t.title();
        topResultSubtitle_ = t.artist();
        topResultType_ = QStringLiteral("Song");
    }

    emit resultsChanged();
}

// ──────────────────────────────────────────────────────────────────
void SearchViewModel::loadRecentSearches()
{
    if (!history_) return;
    recentSearches_ = history_->recent(10);
    emit recentSearchesChanged();
}

// ──────────────────────────────────────────────────────────────────
bool SearchViewModel::matches(const QString& hay, const QString& needle)
{
    return hay.indexOf(needle, 0, Qt::CaseInsensitive) >= 0;
}

} // namespace mf::app::viewmodels

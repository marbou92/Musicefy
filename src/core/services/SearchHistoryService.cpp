// SearchHistoryService.cpp
// See header for design notes.

#include "SearchHistoryService.h"

#include "../database/LibraryRepository.h"
#include "../models/SearchHistory.h"

namespace mf::core::services {

using mf::core::database::LibraryRepository;
using mf::core::interfaces::ISearchHistoryService;
using mf::core::models::SearchHistory;

SearchHistoryService::SearchHistoryService(QObject* parent)
    : QObject(parent)
{
}

SearchHistoryService::SearchHistoryService(LibraryRepository* repo, QObject* parent)
    : QObject(parent)
    , repo_(repo)
{
}

SearchHistoryService::~SearchHistoryService() = default;

void SearchHistoryService::recordSearch(QString query, QString sourceType, int resultCount) {
    if (!repo_ || query.trimmed().isEmpty()) {
        return;
    }
    repo_->recordSearch(query.trimmed(), sourceType, resultCount);
    notifyChanged();
}

void SearchHistoryService::recordClick(QString query, QString sourceType) {
    if (!repo_ || query.trimmed().isEmpty()) {
        return;
    }
    repo_->bumpSearchClickCount(query.trimmed(), sourceType);
    notifyChanged();
}

void SearchHistoryService::clearAll() {
    if (!repo_) return;
    repo_->clearAllSearchHistory();
    notifyChanged();
}

void SearchHistoryService::clearForSource(QString sourceType) {
    if (!repo_) return;
    repo_->clearSearchHistoryForSource(sourceType);
    notifyChanged();
}

QList<SearchHistory> SearchHistoryService::recent(int limit) const {
    if (!repo_) return {};
    return repo_->recentSearchHistory(limit);
}

QList<SearchHistory> SearchHistoryService::suggestions(QString prefix, int limit) const {
    if (!repo_ || prefix.isEmpty()) return {};
    return repo_->searchHistorySuggestions(prefix, limit);
}

void SearchHistoryService::notifyChanged() {
    if (onHistoryChanged_) {
        onHistoryChanged_(recent(50));
    }
    emit historyChangedQ();
}

} // namespace mf::core::services

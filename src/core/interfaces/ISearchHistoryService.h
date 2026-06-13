#pragma once

#include "../models/SearchHistory.h"

#include <QString>

#include <functional>

namespace mf::core::interfaces {

class ISearchHistoryService {
public:
    virtual ~ISearchHistoryService() = default;

    using HistoryCallback = std::function<void(QList<mf::core::models::SearchHistory>)>;

    virtual void recordSearch(QString query, QString sourceType, int resultCount) = 0;
    virtual void recordClick(QString query, QString sourceType) = 0;
    virtual void clearAll() = 0;
    virtual void clearForSource(QString sourceType) = 0;
    virtual QList<mf::core::models::SearchHistory> recent(int limit) const = 0;
    virtual QList<mf::core::models::SearchHistory> suggestions(QString prefix, int limit) const = 0;

    virtual void setOnHistoryChanged(HistoryCallback cb) = 0;
};

} // namespace mf::core::interfaces

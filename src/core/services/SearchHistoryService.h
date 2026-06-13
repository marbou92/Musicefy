// SearchHistoryService.h
// Persists search history (with click counts) to the database. Suggestions
// are computed at query time by prefix-matching against recent queries.

#pragma once

#include "../database/LibraryRepository.h"
#include "../interfaces/ISearchHistoryService.h"

#include <QObject>
#include <QString>

#include <memory>

namespace mf::core::services {

class SearchHistoryService : public QObject,
                             public mf::core::interfaces::ISearchHistoryService {
    Q_OBJECT
public:
    explicit SearchHistoryService(QObject* parent = nullptr);
    explicit SearchHistoryService(mf::core::database::LibraryRepository* repo,
                                  QObject* parent = nullptr);
    ~SearchHistoryService() override;

    void recordSearch(QString query, QString sourceType, int resultCount) override;
    void recordClick(QString query, QString sourceType) override;
    void clearAll() override;
    void clearForSource(QString sourceType) override;
    QList<mf::core::models::SearchHistory> recent(int limit) const override;
    QList<mf::core::models::SearchHistory> suggestions(QString prefix, int limit) const override;

    void setOnHistoryChanged(HistoryCallback cb) override { onHistoryChanged_ = std::move(cb); }

signals:
    void historyChangedQ();

private:
    void notifyChanged();

    mf::core::database::LibraryRepository* repo_ = nullptr;
    HistoryCallback                        onHistoryChanged_;
};

} // namespace mf::core::services

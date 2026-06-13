#pragma once

#include <QDateTime>
#include <QString>

namespace mf::core::models {

class SearchHistory {
public:
    SearchHistory() = default;

    QString id() const { return id_; }
    void setId(QString v) { id_ = std::move(v); }

    QString query() const { return query_; }
    void setQuery(QString v) { query_ = std::move(v); }

    QString sourceType() const { return sourceType_; }
    void setSourceType(QString v) { sourceType_ = std::move(v); }

    QDateTime lastSearchedAt() const { return lastSearchedAt_; }
    void setLastSearchedAt(QDateTime v) { lastSearchedAt_ = v; }

    int resultCount() const { return resultCount_; }
    void setResultCount(int v) { resultCount_ = v; }

    int clickCount() const { return clickCount_; }
    void setClickCount(int v) { clickCount_ = v; }

    bool isSuggestion() const { return isSuggestion_; }
    void setIsSuggestion(bool v) { isSuggestion_ = v; }

    void markClicked();

private:
    QString id_;
    QString query_;
    QString sourceType_;
    QDateTime lastSearchedAt_;
    int resultCount_ = 0;
    int clickCount_ = 0;
    bool isSuggestion_ = false;
};

} // namespace mf::core::models

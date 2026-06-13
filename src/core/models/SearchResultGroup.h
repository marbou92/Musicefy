#pragma once

#include "MusicFile.h"
#include "SearchSourceMode.h"

#include <QList>
#include <QString>

namespace mf::core::models {

class SearchResultGroup {
public:
    SearchResultGroup() = default;

    QString sourceType() const { return sourceType_; }
    void setSourceType(QString v) { sourceType_ = std::move(v); }

    SearchSourceMode mode() const { return mode_; }
    void setMode(SearchSourceMode v) { mode_ = v; }

    QString header() const { return header_; }
    void setHeader(QString v) { header_ = std::move(v); }

    QList<MusicFile> results() const { return results_; }
    void setResults(QList<MusicFile> v) { results_ = std::move(v); }

    int totalCount() const { return totalCount_; }
    void setTotalCount(int v) { totalCount_ = v; }

    bool hasMore() const { return hasMore_; }
    void setHasMore(bool v) { hasMore_ = v; }

private:
    QString sourceType_;
    SearchSourceMode mode_ = SearchSourceMode::All;
    QString header_;
    QList<MusicFile> results_;
    int totalCount_ = 0;
    bool hasMore_ = false;
};

} // namespace mf::core::models

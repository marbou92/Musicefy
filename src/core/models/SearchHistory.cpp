#include "SearchHistory.h"

namespace mf::core::models {

void SearchHistory::markClicked() {
    ++clickCount_;
    lastSearchedAt_ = QDateTime::currentDateTime();
}

} // namespace mf::core::models

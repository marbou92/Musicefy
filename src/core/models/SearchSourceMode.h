#pragma once

#include <QString>

namespace mf::core::models {

enum class SearchSourceMode {
    Local,
    YouTube,
    Subsonic,
    Extension,
    All,
};

QString toString(SearchSourceMode mode);

} // namespace mf::core::models

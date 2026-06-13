#include "SearchSourceMode.h"

namespace mf::core::models {

QString toString(SearchSourceMode mode) {
    switch (mode) {
    case SearchSourceMode::Local:     return QStringLiteral("Local");
    case SearchSourceMode::YouTube:   return QStringLiteral("YouTube");
    case SearchSourceMode::Subsonic:  return QStringLiteral("Subsonic");
    case SearchSourceMode::Extension: return QStringLiteral("Extension");
    case SearchSourceMode::All:       return QStringLiteral("All");
    }
    return QStringLiteral("All");
}

} // namespace mf::core::models

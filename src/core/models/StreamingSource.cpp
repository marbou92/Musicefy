// StreamingSource.cpp

#include "StreamingSource.h"

namespace mf::core::models {

void StreamingSource::ensureConfiguration() {
    if (!url_.isEmpty() && !configuration_.contains(QStringLiteral("url"))) {
        configuration_.insert(QStringLiteral("url"), url_);
    }
    if (!username_.isEmpty() && !configuration_.contains(QStringLiteral("username"))) {
        configuration_.insert(QStringLiteral("username"), username_);
    }
    // Note: password is intentionally NOT copied into configuration_.

    if (type_ == QStringLiteral("Local")
        && !url_.isEmpty()
        && !configuration_.contains(QStringLiteral("folderPath"))) {
        configuration_.insert(QStringLiteral("folderPath"), url_);
    }
}

QString StreamingSource::toDisplayString() const {
    return name_ + QStringLiteral(" (") + type_ + QStringLiteral(")");
}

} // namespace mf::core::models

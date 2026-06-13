// SourceTypes.h
// Constants identifying streaming source kinds. Port of Musicefy.Core.SourceTypes.

#pragma once

#include <QString>
#include <QStringLiteral>

namespace mf::core::models {

class SourceTypes {
public:
    static const QString Local;
    static const QString YouTube;
    static const QString Subsonic;
    static const QString Extension;
};

} // namespace mf::core::models

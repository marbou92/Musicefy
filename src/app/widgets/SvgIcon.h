// SvgIcon.h
// Lightweight wrapper around QSvgRenderer that produces a QIcon
// from a Lucide icon name + color + size. The set of available
// names is defined in Icons.h. Unknown names return an empty
// QIcon (so QPushButton::setIcon silently does nothing rather
// than crashing).

#pragma once

#include <QColor>
#include <QIcon>
#include <QString>

namespace mf::app::widgets {

class SvgIcon {
public:
    // Render the named Lucide icon (e.g. "play", "shuffle") at
    // `size` x `size` pixels using `color` as the stroke color.
    // Returns an empty QIcon if `name` is unknown or `size` <= 0.
    static QIcon get(const QString& name, const QColor& color, int size);
};

} // namespace mf::app::widgets

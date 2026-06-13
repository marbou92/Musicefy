// SvgIcon.cpp
// See header. Lucide icons are 24x24 stroke-based SVGs; we wrap
// the path data with a viewBox + stroke attributes and let
// QSvgRenderer rasterize to a transparent QPixmap, which we
// hand back to QIcon.

#include "SvgIcon.h"

#include "Icons.h"

#include <QHash>
#include <QPainter>
#include <QPixmap>
#include <QSvgRenderer>

namespace mf::app::widgets {

QIcon SvgIcon::get(const QString& name, const QColor& color, int size) {
    if (size <= 0 || !color.isValid()) {
        return {};
    }
    const QHash<QString, QString>& table = icons::all();
    auto it = table.find(name);
    if (it == table.end()) {
        return {};
    }
    const QString svg = QStringLiteral(
        "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 24 24' "
        "fill='none' stroke='%1' stroke-width='2' "
        "stroke-linecap='round' stroke-linejoin='round'>"
        "%2"
        "</svg>")
        .arg(color.name(), *it);

    QPixmap pm(size, size);
    pm.fill(Qt::transparent);
    QPainter p(&pm);
    QSvgRenderer renderer(svg.toUtf8());
    if (!renderer.isValid()) {
        return {};
    }
    renderer.render(&p);
    p.end();
    return QIcon(pm);
}

} // namespace mf::app::widgets

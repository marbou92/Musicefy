// ColorExtractor.cpp
// k-means dominant-color extraction in sRGB space. We downscale first
// (default 96 px longest edge) so the cost is bounded to a few thousand
// pixels regardless of source resolution. The seed color picks the
// most-saturated cluster center, then re-runs it through HctFacade at
// chroma 40 + tone 50 so dynamic accents always read well in the UI.

#include "ColorExtractor.h"

#include "../theme/HctFacade.h"

#include <QHash>
#include <QtGlobal>
#include <algorithm>
#include <cmath>
#include <limits>

namespace mf::core::services {

namespace {
struct Centroid {
    double r = 0, g = 0, b = 0;
    int    count = 0;
};

QImage downscale(const QImage& src, int maxSide) {
    if (src.isNull()) return {};
    if (src.width() <= maxSide && src.height() <= maxSide) {
        return src.convertToFormat(QImage::Format_ARGB32);
    }
    return src.scaled(maxSide, maxSide,
                      Qt::KeepAspectRatio,
                      Qt::SmoothTransformation)
              .convertToFormat(QImage::Format_ARGB32);
}

double sqDist(const Centroid& a, const QColor& c) {
    const double dr = a.r - c.redF();
    const double dg = a.g - c.greenF();
    const double db = a.b - c.blueF();
    return dr*dr + dg*dg + db*db;
}

Centroid meanOf(const QList<QColor>& pts) {
    Centroid m;
    if (pts.isEmpty()) return m;
    double sr = 0, sg = 0, sb = 0;
    for (const QColor& c : pts) {
        sr += c.redF(); sg += c.greenF(); sb += c.blueF();
    }
    m.r = sr / pts.size();
    m.g = sg / pts.size();
    m.b = sb / pts.size();
    m.count = pts.size();
    return m;
}
} // namespace

QList<ColorExtractor::Sample> ColorExtractor::dominantColors(
        const QImage& src, int count, int maxSide) {
    QList<Sample> result;
    if (count <= 0) return result;

    const QImage img = downscale(src, maxSide);
    if (img.isNull()) return result;

    // Collect non-transparent pixels.
    QList<QColor> points;
    points.reserve(img.width() * img.height() / 4);
    for (int y = 0; y < img.height(); ++y) {
        const QRgb* line = reinterpret_cast<const QRgb*>(img.constScanLine(y));
        for (int x = 0; x < img.width(); ++x) {
            const QRgb p = line[x];
            if (qAlpha(p) < 16) continue;
            QColor c(p);
            c.setAlpha(255);
            points.append(c);
        }
    }
    if (points.isEmpty()) return result;
    if (points.size() < count) {
        // Not enough points to cluster meaningfully — return the input
        // colors (or unique ones) sorted by luma.
        QHash<QRgb, int> freq;
        for (const QColor& c : points) ++freq[c.rgba()];
        QList<QHash<QRgb, int>::iterator> sorted;
        sorted.reserve(freq.size());
        for (auto it = freq.begin(); it != freq.end(); ++it) sorted.append(it);
        std::sort(sorted.begin(), sorted.end(),
                  [](auto a, auto b){ return a.value() > b.value(); });
        for (int i = 0; i < sorted.size() && result.size() < count; ++i) {
            Sample s;
            s.color = QColor(sorted[i].key());
            s.count = sorted[i].value();
            result.append(s);
        }
        return result;
    }

    // k-means with k-means++ init. Iterations capped at 12 — more is
    // wasted work for visual use.
    const int k = std::min(count, points.size());
    QList<Centroid> cents;
    cents.reserve(k);
    cents.append(meanOf(QList<QColor>{ points.first() }));
    while (cents.size() < k) {
        // k-means++: pick the point farthest from any existing centroid.
        double bestDist = -1;
        QColor bestPt = points.first();
        for (const QColor& p : points) {
            double minD = std::numeric_limits<double>::max();
            for (const Centroid& c : cents) {
                minD = std::min(minD, sqDist(c, p));
            }
            if (minD > bestDist) {
                bestDist = minD;
                bestPt = p;
            }
        }
        cents.append(meanOf(QList<QColor>{ bestPt }));
    }

    for (int iter = 0; iter < 12; ++iter) {
        QVector<QList<QColor>> groups(k);
        for (const QColor& p : points) {
            int best = 0;
            double bestD = sqDist(cents[0], p);
            for (int i = 1; i < k; ++i) {
                const double d = sqDist(cents[i], p);
                if (d < bestD) { bestD = d; best = i; }
            }
            groups[best].append(p);
        }
        bool moved = false;
        for (int i = 0; i < k; ++i) {
            if (groups[i].isEmpty()) continue;
            Centroid m = meanOf(groups[i]);
            if (std::abs(m.r - cents[i].r) > 1e-3 ||
                std::abs(m.g - cents[i].g) > 1e-3 ||
                std::abs(m.b - cents[i].b) > 1e-3) {
                moved = true;
            }
            cents[i] = m;
        }
        if (!moved) break;
    }

    // Sort by cluster size desc.
    std::sort(cents.begin(), cents.end(),
              [](const Centroid& a, const Centroid& b) {
                  return a.count > b.count;
              });
    for (const Centroid& c : cents) {
        if (c.count == 0) continue;
        Sample s;
        s.color = QColor::fromRgbF(std::clamp(c.r, 0.0, 1.0),
                                   std::clamp(c.g, 0.0, 1.0),
                                   std::clamp(c.b, 0.0, 1.0));
        s.count = c.count;
        result.append(s);
    }
    return result;
}

QColor ColorExtractor::seedColor(const QImage& image) {
    if (image.isNull()) return {};
    const QList<Sample> dom = dominantColors(image, 5, 96);
    if (dom.isEmpty()) return {};

    // Pick the cluster with the highest chroma in HCT space. HctFacade
    // expects ARGB ints.
    int    bestArgb = dom.first().color.rgba();
    double bestChroma = -1.0;
    for (const Sample& s : dom) {
        const int argb = s.color.rgba();
        const double chroma = mf::core::theme::HctFacade::chromaFromArgb(argb);
        if (chroma > bestChroma) {
            bestChroma = chroma;
            bestArgb   = argb;
        }
    }
    if (bestChroma <= 0.0) {
        // All clusters are greyscale — fall back to the largest cluster.
        bestArgb = dom.first().color.rgba();
    }

    const double hue = mf::core::theme::HctFacade::hueFromArgb(bestArgb);
    // Re-render at chroma=40 tone=50 — the Material 3 sweet spot for a
    // theme accent. If the source had no chroma this stays grey.
    const int outArgb = mf::core::theme::HctFacade::argbFromHct(hue, 40.0, 50.0);
    return QColor(outArgb);
}

} // namespace mf::core::services

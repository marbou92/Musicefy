// ColorExtractor.h
// Pure Qt dominant-color extraction for cover art. Two entry points:
//   - dominantColors(image, n) : top-N dominant colors via k-means on a
//                                downscaled image.
//   - seedColor(image)         : single Material-3 seed color for the
//                                dynamic-accent feature. Picked from the
//                                most-saturated cluster, then converted
//                                to HCT via HctFacade and re-emitted at
//                                chroma=40 so it always reads well as
//                                a theme accent.

#pragma once

#include <QColor>
#include <QImage>
#include <QList>

namespace mf::core::services {

class ColorExtractor {
public:
    struct Sample {
        QColor color;
        int    count = 0;
    };

    /// Returns up to `count` dominant colors, sorted by cluster size desc.
    /// `image` is internally scaled to `maxSide` on the longest edge.
    static QList<Sample> dominantColors(const QImage& image,
                                        int count = 5,
                                        int maxSide = 96);

    /// Returns the recommended Material-3 seed color for `image`.
    /// Returns an invalid QColor if the image is null.
    static QColor seedColor(const QImage& image);
};

} // namespace mf::core::services

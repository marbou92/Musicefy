#pragma once

#include <QColor>
#include <QMetaType>
#include <QString>

namespace mf::core::theme {

struct MusicefyColorScheme {
    Q_GADGET
public:

    QString name;

    QColor primary;
    QColor onPrimary;
    QColor primaryContainer;
    QColor onPrimaryContainer;

    QColor secondary;
    QColor onSecondary;
    QColor secondaryContainer;
    QColor onSecondaryContainer;

    QColor tertiary;
    QColor onTertiary;
    QColor tertiaryContainer;
    QColor onTertiaryContainer;

    QColor error;
    QColor onError;
    QColor errorContainer;
    QColor onErrorContainer;

    QColor background;
    QColor onBackground;
    QColor surface;
    QColor onSurface;
    QColor surfaceVariant;
    QColor onSurfaceVariant;
    QColor outline;
    QColor outlineVariant;

    QColor surfaceContainer;
    QColor surfaceContainerHigh;
    QColor surfaceContainerHighest;
    QColor surfaceContainerLow;
    QColor surfaceContainerLowest;

    QColor inverseSurface;
    QColor inverseOnSurface;
    QColor inversePrimary;
};

/// Default light color scheme.
MusicefyColorScheme lightDefault();

} // namespace mf::core::theme

Q_DECLARE_METATYPE(mf::core::theme::MusicefyColorScheme)

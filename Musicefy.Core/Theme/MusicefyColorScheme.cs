using System.Windows.Media;

namespace Musicefy.Core.Theme
{
    /// <summary>
    /// A flat POCO that mirrors the Material 3 color roles Musicefy uses,
    /// expressed as plain <see cref="Color"/> structs instead of being
    /// computed on the fly from HCT/TonalPalette. Each named <see cref="AppTheme"/>
    /// provides exactly two instances of this class: one for light mode and one
    /// for dark mode.
    /// </summary>
    public sealed class MusicefyColorScheme
    {
        // ── Primary ──────────────────────────────────────────────────────────
        public Color Primary              { get; init; }
        public Color OnPrimary            { get; init; }
        public Color PrimaryContainer     { get; init; }
        public Color OnPrimaryContainer   { get; init; }

        // ── Secondary ────────────────────────────────────────────────────────
        public Color Secondary            { get; init; }
        public Color OnSecondary          { get; init; }
        public Color SecondaryContainer   { get; init; }
        public Color OnSecondaryContainer { get; init; }

        // ── Tertiary ─────────────────────────────────────────────────────────
        public Color Tertiary             { get; init; }
        public Color OnTertiary           { get; init; }
        public Color TertiaryContainer    { get; init; }
        public Color OnTertiaryContainer  { get; init; }

        // ── Error ────────────────────────────────────────────────────────────
        public Color Error                { get; init; }
        public Color OnError              { get; init; }
        public Color ErrorContainer       { get; init; }
        public Color OnErrorContainer     { get; init; }

        // ── Surface ──────────────────────────────────────────────────────────
        public Color Surface              { get; init; }
        public Color OnSurface            { get; init; }
        public Color SurfaceVariant       { get; init; }
        public Color OnSurfaceVariant     { get; init; }
        public Color SurfaceContainerLowest  { get; init; }
        public Color SurfaceContainerLow     { get; init; }
        public Color SurfaceContainer        { get; init; }
        public Color SurfaceContainerHigh    { get; init; }
        public Color SurfaceContainerHighest { get; init; }

        // ── Outline ──────────────────────────────────────────────────────────
        public Color Outline              { get; init; }
        public Color OutlineVariant       { get; init; }

        // ── Inverse ──────────────────────────────────────────────────────────
        public Color InverseSurface       { get; init; }
        public Color InverseOnSurface     { get; init; }
        public Color InversePrimary       { get; init; }

        /// <summary>True if this scheme is intended for dark mode.</summary>
        public bool  IsDark               { get; init; }
    }
}

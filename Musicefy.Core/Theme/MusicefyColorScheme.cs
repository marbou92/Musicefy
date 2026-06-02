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
        public Color Primary              { get; set; }
        public Color OnPrimary            { get; set; }
        public Color PrimaryContainer     { get; set; }
        public Color OnPrimaryContainer   { get; set; }

        // ── Secondary ────────────────────────────────────────────────────────
        public Color Secondary            { get; set; }
        public Color OnSecondary          { get; set; }
        public Color SecondaryContainer   { get; set; }
        public Color OnSecondaryContainer { get; set; }

        // ── Tertiary ─────────────────────────────────────────────────────────
        public Color Tertiary             { get; set; }
        public Color OnTertiary           { get; set; }
        public Color TertiaryContainer    { get; set; }
        public Color OnTertiaryContainer  { get; set; }

        // ── Error ────────────────────────────────────────────────────────────
        public Color Error                { get; set; }
        public Color OnError              { get; set; }
        public Color ErrorContainer       { get; set; }
        public Color OnErrorContainer     { get; set; }

        // ── Surface ──────────────────────────────────────────────────────────
        public Color Surface              { get; set; }
        public Color OnSurface            { get; set; }
        public Color SurfaceVariant       { get; set; }
        public Color OnSurfaceVariant     { get; set; }
        public Color SurfaceContainerLowest  { get; set; }
        public Color SurfaceContainerLow     { get; set; }
        public Color SurfaceContainer        { get; set; }
        public Color SurfaceContainerHigh    { get; set; }
        public Color SurfaceContainerHighest { get; set; }

        // ── Outline ──────────────────────────────────────────────────────────
        public Color Outline              { get; set; }
        public Color OutlineVariant       { get; set; }

        // ── Inverse ──────────────────────────────────────────────────────────
        public Color InverseSurface       { get; set; }
        public Color InverseOnSurface     { get; set; }
        public Color InversePrimary       { get; set; }

        /// <summary>True if this scheme is intended for dark mode.</summary>
        public bool  IsDark               { get; set; }
    }
}

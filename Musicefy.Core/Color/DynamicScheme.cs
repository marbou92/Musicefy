using System;

namespace Musicefy.Core.Hct
{
    // ── Palette Styles (ArchiveTune-inspired) ──────────────────────────────
    // These match Material 3 Expressive palette styles, adapted for WPF.
    // Each style applies different chroma/hue offsets to the seed.
    public enum PaletteStyle
    {
        TonalSpot,    // Default: medium chroma, close secondaries  (classic M3)
        Vibrant,      // High chroma, high-contrast energy
        Expressive,   // Shifted hues, very colorful, playful
        Fidelity,     // Stays closest to original seed hue
        Monochrome,   // Near-zero chroma — neutral gray tones
        Neutral,      // Low chroma — muted, understated tones
        Rainbow,      // Wide hue spread across primaries
        FruitSalad,   // Opposite hue for secondary — high contrast
    }

    public class DynamicScheme
    {
        public TonalPalette PrimaryPalette { get; }
        public TonalPalette SecondaryPalette { get; }
        public TonalPalette TertiaryPalette { get; }
        public TonalPalette NeutralPalette { get; }
        public TonalPalette NeutralVariantPalette { get; }
        public TonalPalette ErrorPalette { get; }

        public bool IsDark { get; }
        public bool IsDarkPure { get; }
        public bool IsExactPalette { get; }
        public PaletteStyle Style { get; }
        public SeedPalette Source { get; }

        /// <summary>
        /// Whether this scheme was generated from dynamic album-art colors.
        /// When true, surface colors are anchored to the base seed palette
        /// rather than tinted by the album art hue.
        /// </summary>
        public bool IsDynamicAccent { get; }

        // Cached primary ARGB at the correct tone for this mode (used for surface tinting)
        private readonly double _primaryHue;
        private readonly double _primaryChroma;

        private DynamicScheme(
            SeedPalette source,
            TonalPalette primary,
            TonalPalette secondary,
            TonalPalette tertiary,
            TonalPalette neutral,
            TonalPalette neutralVariant,
            TonalPalette error,
            bool isDark,
            bool isDarkPure,
            bool isExactPalette,
            PaletteStyle style,
            double primaryHue,
            double primaryChroma,
            bool isDynamicAccent = false)
        {
            Source = source;
            PrimaryPalette = primary;
            SecondaryPalette = secondary;
            TertiaryPalette = tertiary;
            NeutralPalette = neutral;
            NeutralVariantPalette = neutralVariant;
            ErrorPalette = error;
            IsDark = isDark;
            IsDarkPure = isDarkPure;
            IsExactPalette = isExactPalette;
            Style = style;
            IsDynamicAccent = isDynamicAccent;
            _primaryHue = primaryHue;
            _primaryChroma = primaryChroma;
        }

        /// <summary>
        /// Creates a hybrid scheme that uses the provided accent palettes
        /// (primary/secondary/tertiary) but preserves the neutral palettes
        /// from the base (seed) scheme. This is the ArchiveTune approach:
        /// album art colors update accents only, while surface/neutral colors
        /// remain anchored to the user's chosen palette.
        /// </summary>
        public static DynamicScheme CreateDynamicAccentScheme(
            DynamicScheme baseScheme,
            TonalPalette dynamicPrimary,
            TonalPalette dynamicSecondary,
            TonalPalette dynamicTertiary)
        {
            return new DynamicScheme(
                source: baseScheme.Source,
                primary: dynamicPrimary,
                secondary: dynamicSecondary,
                tertiary: dynamicTertiary,
                neutral: baseScheme.NeutralPalette,
                neutralVariant: baseScheme.NeutralVariantPalette,
                error: baseScheme.ErrorPalette,
                isDark: baseScheme.IsDark,
                isDarkPure: baseScheme.IsDarkPure,
                isExactPalette: baseScheme.IsExactPalette,
                style: baseScheme.Style,
                primaryHue: baseScheme._primaryHue,
                primaryChroma: baseScheme._primaryChroma,
                isDynamicAccent: true);
        }

        // ── Factory: from SeedPalette (ArchiveTune merged scheme approach) ────
        //
        // ArchiveTune generates 4 independent Material3 schemes from 4 seed
        // colors (primary, secondary, tertiary, neutral), then merges tokens
        // from each: primary tokens from primary scheme, secondary tokens from
        // secondary scheme, surfaces from neutral scheme.
        //
        // We replicate this by creating separate TonalPalettes from each seed
        // channel's own hue+chroma, instead of deriving everything from the
        // primary hue via style offsets.
        //
        public static DynamicScheme FromSeed(
            SeedPalette seed,
            bool isDark,
            bool isDarkPure,
            bool isExactPalette = false,
            PaletteStyle style = PaletteStyle.TonalSpot)
        {
            // ── Step 1: Compute base hues/chromas from the SeedPalette's own parameters ──
            // The SeedPalette defines its own secondary/tertiary offsets and chroma ratios.
            // We use these as the BASE values, then apply style adjustments on top.
            double primaryHue    = seed.PrimaryHue;
            double primaryChroma = seed.PrimaryChroma;

            // Secondary: seed's own hue offset and chroma ratio
            double secondaryHue    = MathUtils.SanitizeDegrees(seed.PrimaryHue + seed.SecondaryHueOffset);
            double secondaryChroma = seed.PrimaryChroma * seed.SecondaryChromaRatio;

            // Tertiary: seed's own hue offset and chroma ratio
            double tertiaryHue    = MathUtils.SanitizeDegrees(seed.PrimaryHue + seed.TertiaryHueOffset);
            double tertiaryChroma = seed.PrimaryChroma * seed.TertiaryChromaRatio;

            // Neutral: use primary hue with the seed's own NeutralChroma
            double neutralHue    = seed.PrimaryHue;
            double neutralChroma = seed.NeutralChroma;

            // ── Step 2: Apply style-based adjustments on top of seed parameters ──
            // Style modifies chroma levels and can shift secondary/tertiary hues,
            // but it should NOT completely override the seed's natural character.
            if (isExactPalette)
            {
                // Exact: preserve seed chroma for primary, minimal secondary offset
                // Neutral stays at the seed's natural level but is capped
                neutralChroma = Math.Min(neutralChroma, 6);
            }
            else
            {
                switch (style)
                {
                    case PaletteStyle.TonalSpot:
                        // Classic M3: boost primary chroma minimum, keep seed's secondary/tertiary
                        primaryChroma = Math.Max(36.0, primaryChroma);
                        // Neutral capped at 4 (ArchiveTune standard)
                        neutralChroma = Math.Min(neutralChroma, 4);
                        break;

                    case PaletteStyle.Vibrant:
                        // High chroma across the board, secondaries are brighter
                        primaryChroma    = Math.Max(48.0, primaryChroma * 1.2);
                        secondaryHue     = MathUtils.SanitizeDegrees(seed.PrimaryHue + 15);
                        secondaryChroma  = Math.Max(secondaryChroma, 32);
                        tertiaryHue      = MathUtils.SanitizeDegrees(seed.PrimaryHue - 40);
                        tertiaryChroma   = Math.Max(tertiaryChroma, 36);
                        neutralChroma    = 6;
                        break;

                    case PaletteStyle.Expressive:
                        // Playful: primary hue stays, secondary/tertiary jump dramatically
                        primaryChroma    = Math.Max(40.0, primaryChroma);
                        secondaryHue     = MathUtils.SanitizeDegrees(seed.PrimaryHue + 95);
                        secondaryChroma  = Math.Max(secondaryChroma, 24);
                        tertiaryHue      = MathUtils.SanitizeDegrees(seed.PrimaryHue + 180);
                        tertiaryChroma   = Math.Max(tertiaryChroma, 32);
                        neutralChroma    = Math.Min(neutralChroma, 8);
                        break;

                    case PaletteStyle.Fidelity:
                        // Stay near seed hue; secondary/tertiary very close
                        // Use seed's own chroma for primary (no minimum boost)
                        secondaryHue     = MathUtils.SanitizeDegrees(seed.PrimaryHue + 5);
                        secondaryChroma  = Math.Max(primaryChroma * 0.7, 16);
                        tertiaryHue      = MathUtils.SanitizeDegrees(seed.PrimaryHue + 20);
                        tertiaryChroma   = Math.Max(primaryChroma * 0.5, 12);
                        neutralChroma    = Math.Min(Math.Max(primaryChroma * 0.12, 2), 8);
                        break;

                    case PaletteStyle.Neutral:
                        // Low chroma: muted, understated — ArchiveTune uses this for chroma < 12
                        primaryChroma    = Math.Min(primaryChroma, 12);
                        secondaryChroma  = Math.Min(secondaryChroma, 8);
                        tertiaryChroma   = Math.Min(tertiaryChroma, 6);
                        neutralChroma    = Math.Min(neutralChroma, 4);
                        break;

                    case PaletteStyle.Monochrome:
                        primaryChroma    = 4;
                        secondaryHue     = seed.PrimaryHue;
                        secondaryChroma  = 4;
                        tertiaryHue      = seed.PrimaryHue;
                        tertiaryChroma   = 4;
                        neutralChroma    = 2;
                        break;

                    case PaletteStyle.Rainbow:
                        // Wide hue spread across primaries
                        primaryChroma    = Math.Max(36.0, primaryChroma);
                        secondaryHue     = MathUtils.SanitizeDegrees(seed.PrimaryHue + 120);
                        secondaryChroma  = Math.Max(secondaryChroma, 28);
                        tertiaryHue      = MathUtils.SanitizeDegrees(seed.PrimaryHue + 240);
                        tertiaryChroma   = Math.Max(tertiaryChroma, 28);
                        neutralChroma    = Math.Min(neutralChroma, 6);
                        break;

                    case PaletteStyle.FruitSalad:
                        // Opposite hue for secondary — high contrast
                        primaryChroma    = Math.Max(36.0, primaryChroma);
                        secondaryHue     = MathUtils.SanitizeDegrees(seed.PrimaryHue - 50);
                        secondaryChroma  = Math.Max(secondaryChroma, 36);
                        tertiaryHue      = MathUtils.SanitizeDegrees(seed.PrimaryHue + 50);
                        tertiaryChroma   = Math.Max(tertiaryChroma, 36);
                        neutralChroma    = Math.Min(neutralChroma, 6);
                        break;
                }
            }

            // ── Step 3: Build TonalPalettes from the computed parameters ──
            // Each palette gets its own hue+chroma, following the ArchiveTune
            // merged-scheme approach where each channel is independent.
            double chromaFactor = isDarkPure ? 0.65 : 1.0;

            var primaryPalette = TonalPalette.FromHueAndChroma(
                primaryHue, Math.Max(primaryChroma, 4) * chromaFactor);

            // ArchiveTune merged scheme approach: secondary palette uses the
            // secondary seed's hue but generates a "primary" tonal palette from it.
            // This gives secondary containers the richness of a full palette,
            // not just a low-chroma tint.
            var secondaryPalette = TonalPalette.FromHueAndChroma(
                secondaryHue, Math.Max(secondaryChroma, 4) * chromaFactor);

            var tertiaryPalette = TonalPalette.FromHueAndChroma(
                tertiaryHue, Math.Max(tertiaryChroma, 4) * chromaFactor);

            // Neutral palettes: capped chroma (ArchiveTune approach)
            // neutral = min(chroma, 4.0), neutralVariant = min(chroma, 8.0)
            // This prevents light-mode "solid color" where surfaces become
            // a flat tinted sheet instead of having subtle tonal variation.
            var neutralPalette = TonalPalette.FromHueAndChroma(
                neutralHue, Math.Min(neutralChroma, 4.0) * chromaFactor);

            var neutralVariantPalette = TonalPalette.FromHueAndChroma(
                neutralHue, Math.Min(Math.Max(neutralChroma * 2, neutralChroma), 8.0) * chromaFactor);

            var errorPalette = TonalPalette.FromHueAndChroma(25.0, 84.0);

            return new DynamicScheme(
                seed, primaryPalette, secondaryPalette, tertiaryPalette,
                neutralPalette, neutralVariantPalette, errorPalette,
                isDark, isDarkPure, isExactPalette, style,
                primaryHue, primaryChroma * chromaFactor);
        }

        // ── Factory: from ARGB colors ──────────────────────────────────────
        public static DynamicScheme FromColors(
            int primaryArgb,
            int secondaryArgb,
            int tertiaryArgb,
            int neutralArgb,
            bool isDark,
            bool isDarkPure,
            bool isExactPalette = false,
            PaletteStyle style = PaletteStyle.TonalSpot)
        {
            // Extract hue+chroma from each provided color for richer multi-channel schemes
            var pHct = Hct.FromInt(primaryArgb);
            var sHct = Hct.FromInt(secondaryArgb);
            var tHct = Hct.FromInt(tertiaryArgb);
            var nHct = Hct.FromInt(neutralArgb);

            double chromaFactor = isDarkPure ? 0.65 : 1.0;
            double primaryChroma = isExactPalette ? pHct.Chroma : Math.Max(36.0, pHct.Chroma);

            // Build each palette from its own seed color's hue+chroma
            // (ArchiveTune merged scheme approach)
            var primaryPalette = TonalPalette.FromHueAndChroma(
                pHct.Hue, primaryChroma * chromaFactor);

            var secondaryPalette = TonalPalette.FromHueAndChroma(
                sHct.Hue, Math.Max(sHct.Chroma, 4) * chromaFactor);

            var tertiaryPalette = TonalPalette.FromHueAndChroma(
                tHct.Hue, Math.Max(tHct.Chroma, 4) * chromaFactor);

            // Neutral palettes: capped chroma (ArchiveTune approach)
            var neutralPalette = TonalPalette.FromHueAndChroma(
                nHct.Hue, Math.Min(Math.Max(nHct.Chroma, 2), 4.0) * chromaFactor);

            var neutralVariantPalette = TonalPalette.FromHueAndChroma(
                nHct.Hue, Math.Min(Math.Max(nHct.Chroma * 1.5, 4), 8.0) * chromaFactor);

            var errorPalette = TonalPalette.FromHueAndChroma(25.0, 84.0);

            return new DynamicScheme(
                null, primaryPalette, secondaryPalette, tertiaryPalette,
                neutralPalette, neutralVariantPalette, errorPalette,
                isDark, isDarkPure, isExactPalette, style,
                pHct.Hue, primaryChroma * chromaFactor);
        }

        // ── Tone role resolution ───────────────────────────────────────────
        public double GetTone(ToneRole role)
        {
            bool dark = IsDark;

            return role switch
            {
                // Primary roles
                ToneRole.Primary             => dark ? 80 : 40,
                ToneRole.OnPrimary           => dark ? 20 : 100,
                ToneRole.PrimaryContainer    => dark ? 30 : 90,
                ToneRole.OnPrimaryContainer  => dark ? 90 : 10,

                // Secondary roles
                ToneRole.Secondary            => dark ? 80 : 40,
                ToneRole.OnSecondary          => dark ? 20 : 100,
                ToneRole.SecondaryContainer   => dark ? 30 : 90,
                ToneRole.OnSecondaryContainer => dark ? 90 : 10,

                // Tertiary roles
                ToneRole.Tertiary            => dark ? 80 : 40,
                ToneRole.OnTertiary          => dark ? 20 : 100,
                ToneRole.TertiaryContainer   => dark ? 30 : 90,
                ToneRole.OnTertiaryContainer => dark ? 90 : 10,

                // Error roles
                ToneRole.Error               => dark ? 80 : 40,
                ToneRole.OnError             => dark ? 20 : 100,
                ToneRole.ErrorContainer      => dark ? 30 : 90,
                ToneRole.OnErrorContainer    => dark ? 90 : 10,

                // ── Tonal surface roles ───────────────────────────────────
                ToneRole.Surface                  => dark ? 6   : 98,
                ToneRole.OnSurface                => dark ? 90  : 10,
                ToneRole.SurfaceVariant           => dark ? 30  : 90,
                ToneRole.OnSurfaceVariant         => dark ? 80  : 30,
                ToneRole.Outline                  => dark ? 60  : 50,
                ToneRole.OutlineVariant           => dark ? 30  : 80,
                ToneRole.SurfaceContainerLowest   => dark ? 4   : 100,
                ToneRole.SurfaceContainerLow      => dark ? 10  : 96,
                ToneRole.SurfaceContainer         => dark ? 12  : 94,
                ToneRole.SurfaceContainerHigh     => dark ? 17  : 92,
                ToneRole.SurfaceContainerHighest  => dark ? 22  : 90,
                ToneRole.Hover                    => dark ? 30  : 90,
                ToneRole.SkeletonBase             => dark ? 15  : 92,
                ToneRole.SkeletonHigh             => dark ? 25  : 96,
                ToneRole.InverseSurface           => dark ? 90  : 20,
                ToneRole.InverseOnSurface         => dark ? 20  : 95,
                ToneRole.InversePrimary           => dark ? 40  : 80,

                _ => 50
            };
        }

        // ── ARGB resolution ────────────────────────────────────────────────
        public int GetArgb(ToneRole role)
        {
            TonalPalette palette = role switch
            {
                ToneRole.Primary or ToneRole.OnPrimary
                    or ToneRole.PrimaryContainer or ToneRole.OnPrimaryContainer
                    => PrimaryPalette,

                ToneRole.Secondary or ToneRole.OnSecondary
                    or ToneRole.SecondaryContainer or ToneRole.OnSecondaryContainer
                    => SecondaryPalette,

                ToneRole.Tertiary or ToneRole.OnTertiary
                    or ToneRole.TertiaryContainer or ToneRole.OnTertiaryContainer
                    => TertiaryPalette,

                ToneRole.Error or ToneRole.OnError
                    or ToneRole.ErrorContainer or ToneRole.OnErrorContainer
                    => ErrorPalette,

                // All surface/neutral roles come from tonal neutral palettes
                ToneRole.Surface or ToneRole.OnSurface
                    or ToneRole.SurfaceContainerLowest or ToneRole.SurfaceContainerLow
                    or ToneRole.SurfaceContainer or ToneRole.SurfaceContainerHigh
                    or ToneRole.SurfaceContainerHighest
                    or ToneRole.Hover or ToneRole.SkeletonBase or ToneRole.SkeletonHigh
                    or ToneRole.InverseSurface or ToneRole.InverseOnSurface
                    => NeutralPalette,

                ToneRole.SurfaceVariant or ToneRole.OnSurfaceVariant
                    or ToneRole.Outline or ToneRole.OutlineVariant
                    or ToneRole.InversePrimary
                    => NeutralVariantPalette,

                _ => PrimaryPalette
            };

            // Pure black mode: override surface-family tones to near-black
            if (IsDarkPure && IsSurfaceRole(role))
            {
                double tone = role switch
                {
                    ToneRole.Surface                 => 0,
                    ToneRole.SurfaceContainerLowest  => 0,
                    ToneRole.SurfaceContainerLow     => 2,
                    ToneRole.SurfaceContainer        => 4,
                    ToneRole.SurfaceContainerHigh    => 6,
                    ToneRole.SurfaceContainerHighest => 8,
                    ToneRole.Hover                   => 10,
                    ToneRole.SkeletonBase            => 5,
                    ToneRole.SkeletonHigh            => 10,
                    _ => GetTone(role)
                };
                return palette.GetTone(tone);
            }

            return palette.GetTone(GetTone(role));
        }

        private static bool IsSurfaceRole(ToneRole role) => role is
            ToneRole.Surface or ToneRole.SurfaceContainerLowest or ToneRole.SurfaceContainerLow
            or ToneRole.SurfaceContainer or ToneRole.SurfaceContainerHigh
            or ToneRole.SurfaceContainerHighest or ToneRole.Hover
            or ToneRole.SkeletonBase or ToneRole.SkeletonHigh;

        // ── Accent variants ────────────────────────────────────────────────
        public int GetAccentArgb(AccentVariant variant)
        {
            return variant switch
            {
                AccentVariant.Default  => PrimaryPalette.GetTone(IsDark ? 80 : 40),
                AccentVariant.Hover    => PrimaryPalette.GetTone(IsDark ? 30 : 90),
                AccentVariant.Pressed  => PrimaryPalette.GetTone(IsDark ? 50 : 30),
                AccentVariant.Glow     => PrimaryPalette.GetTone(IsDark ? 80 : 40),
                _ => PrimaryPalette.GetTone(IsDark ? 80 : 40)
            };
        }

        // ── Preview color (mode-aware) ─────────────────────────────────────
        public int GetPreviewPrimaryArgb()  => PrimaryPalette.GetTone(IsDark ? 80 : 40);
        public int GetPreviewSecondaryArgb() => SecondaryPalette.GetTone(IsDark ? 80 : 40);
        public int GetPreviewTertiaryArgb() => TertiaryPalette.GetTone(IsDark ? 80 : 40);
        public int GetPreviewSurfaceArgb()  => NeutralPalette.GetTone(IsDark ? 12 : 94);

        // ── Raw seed preview (ArchiveTune approach) ─────────────────────────
        // For palette picker cards: shows the raw seed color at a neutral tone (60)
        // regardless of light/dark mode.
        public int GetRawSeedPrimaryArgb()   => PrimaryPalette.GetTone(60);
        public int GetRawSeedSecondaryArgb() => SecondaryPalette.GetTone(60);
        public int GetRawSeedTertiaryArgb()  => TertiaryPalette.GetTone(60);
        public int GetRawSeedNeutralArgb()   => NeutralPalette.GetTone(60);

        // ── Auto-select palette style based on seed chroma (ArchiveTune) ───
        public static PaletteStyle AutoSelectStyle(double chroma)
        {
            if (chroma < 4.0)  return PaletteStyle.Monochrome;
            if (chroma < 12.0) return PaletteStyle.Neutral;
            return PaletteStyle.TonalSpot;
        }

        public static PaletteStyle AutoSelectStyle(SeedPalette seed)
            => AutoSelectStyle(seed.PrimaryChroma);

        public static PaletteStyle AutoSelectStyle(int argb)
            => AutoSelectStyle(Hct.FromInt(argb).Chroma);
    }

    // ── Extended ToneRole enum ─────────────────────────────────────────────
    public enum ToneRole
    {
        Primary, OnPrimary, PrimaryContainer, OnPrimaryContainer,
        Secondary, OnSecondary, SecondaryContainer, OnSecondaryContainer,
        Tertiary, OnTertiary, TertiaryContainer, OnTertiaryContainer,
        Error, OnError, ErrorContainer, OnErrorContainer,
        Surface, OnSurface,
        SurfaceVariant, OnSurfaceVariant,
        Outline, OutlineVariant,
        SurfaceContainerLowest, SurfaceContainerLow,
        SurfaceContainer, SurfaceContainerHigh, SurfaceContainerHighest,
        Hover, SkeletonBase, SkeletonHigh,
        InverseSurface, InverseOnSurface, InversePrimary,
    }

    public enum AccentVariant { Default, Hover, Pressed, Glow }
}

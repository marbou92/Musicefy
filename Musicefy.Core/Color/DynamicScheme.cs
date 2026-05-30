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
            double primaryChroma)
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
            _primaryHue = primaryHue;
            _primaryChroma = primaryChroma;
        }

        // ── Factory: from SeedPalette ──────────────────────────────────────
        public static DynamicScheme FromSeed(
            SeedPalette seed,
            bool isDark,
            bool isDarkPure,
            bool isExactPalette = false,
            PaletteStyle style = PaletteStyle.TonalSpot)
        {
            return FromHue(
                seed.PrimaryHue,
                seed.PrimaryChroma,
                isDark,
                isDarkPure,
                isExactPalette,
                style,
                seed);
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

            // Build the scheme from the primary hue, then override individual palettes
            // with the actual extracted hue+chroma from each channel.
            StyleParams p = ComputeStyleParams(pHct.Hue, pHct.Chroma, style, isExactPalette);

            double chromaFactor = isDarkPure ? 0.65 : 1.0;
            double primaryChroma = isExactPalette ? pHct.Chroma : p.PrimaryChroma;

            var primaryPalette        = TonalPalette.FromHueAndChroma(p.PrimaryHue, primaryChroma * chromaFactor);
            var secondaryPalette      = TonalPalette.FromHueAndChroma(sHct.Hue, Math.Max(sHct.Chroma, 4) * chromaFactor);
            var tertiaryPalette       = TonalPalette.FromHueAndChroma(tHct.Hue, Math.Max(tHct.Chroma, 4) * chromaFactor);
            var neutralPalette        = TonalPalette.FromHueAndChroma(nHct.Hue, Math.Max(nHct.Chroma, 2) * chromaFactor);
            var neutralVariantPalette = TonalPalette.FromHueAndChroma(nHct.Hue, Math.Max(nHct.Chroma * 1.5, 4) * chromaFactor);
            var errorPalette          = TonalPalette.FromHueAndChroma(25.0, 84.0);

            return new DynamicScheme(
                null, primaryPalette, secondaryPalette, tertiaryPalette,
                neutralPalette, neutralVariantPalette, errorPalette,
                isDark, isDarkPure, isExactPalette, style,
                p.PrimaryHue, primaryChroma * chromaFactor);
        }

        // ── Core builder ──────────────────────────────────────────────────
        private static DynamicScheme FromHue(
            double hue,
            double chroma,
            bool isDark,
            bool isDarkPure,
            bool isExactPalette,
            PaletteStyle style,
            SeedPalette seed = null)
        {
            // Exact palette: keep chroma as-is; normal: apply style modifiers
            StyleParams p = ComputeStyleParams(hue, chroma, style, isExactPalette);

            double chromaFactor = isDarkPure ? 0.65 : 1.0;

            // In Exact mode, primary uses the original chroma without the M3 tone-system clamp
            double primaryChroma = isExactPalette ? chroma : p.PrimaryChroma;

            var primary        = TonalPalette.FromHueAndChroma(p.PrimaryHue, primaryChroma * chromaFactor);
            var secondary      = TonalPalette.FromHueAndChroma(p.SecondaryHue, p.SecondaryChroma * chromaFactor);
            var tertiary       = TonalPalette.FromHueAndChroma(p.TertiaryHue, p.TertiaryChroma * chromaFactor);

            // Tonal neutral: slight hue bias from primary (ArchiveTune approach — no pure gray)
            // NeutralChroma of ~4–6 gives a subtle warmth/cool tint instead of lifeless gray
            var neutral        = TonalPalette.FromHueAndChroma(p.PrimaryHue, p.NeutralChroma * chromaFactor);
            var neutralVariant = TonalPalette.FromHueAndChroma(p.PrimaryHue, p.NeutralVariantChroma * chromaFactor);
            var error          = TonalPalette.FromHueAndChroma(25.0, 84.0);

            return new DynamicScheme(
                seed, primary, secondary, tertiary, neutral, neutralVariant, error,
                isDark, isDarkPure, isExactPalette, style,
                p.PrimaryHue, primaryChroma * chromaFactor);
        }

        // ── Style parameter computation ────────────────────────────────────
        private static StyleParams ComputeStyleParams(
            double hue, double chroma, PaletteStyle style, bool exact)
        {
            // TonalSpot defaults (closest to material baseline)
            double pH = hue, pC = Math.Max(36.0, chroma);
            double sH = hue + 15, sC = 16;
            double tH = hue + 60, tC = 24;
            double nC = 4, nvC = 8;

            if (exact)
            {
                // Exact: preserve seed chroma for primary, small secondary offset
                pC  = chroma;
                sH  = hue + 30; sC = chroma * 0.4;
                tH  = hue + 60; tC = chroma * 0.3;
                nC  = Math.Min(chroma * 0.1, 6); nvC = Math.Min(chroma * 0.15, 10);
            }
            else
            {
                switch (style)
                {
                    case PaletteStyle.TonalSpot:
                        // defaults above
                        break;

                    case PaletteStyle.Vibrant:
                        // High chroma across the board, secondaries are bright
                        pC  = Math.Max(48.0, chroma * 1.2);
                        sH  = hue + 15;   sC = 32;
                        tH  = hue - 40;   tC = 36;
                        nC  = 6;          nvC = 10;
                        break;

                    case PaletteStyle.Expressive:
                        // Playful: primary hue stays, secondary/tertiary jump dramatically
                        pC  = Math.Max(40.0, chroma);
                        sH  = hue + 95;   sC = 24;
                        tH  = hue + 180;  tC = 32;  // complementary tertiary
                        nC  = 8;          nvC = 12;
                        break;

                    case PaletteStyle.Fidelity:
                        // Stay near seed hue; secondary/tertiary very close
                        pC  = chroma;
                        sH  = hue +  5;   sC = chroma * 0.7;
                        tH  = hue + 20;   tC = chroma * 0.5;
                        nC  = Math.Min(chroma * 0.12, 8); nvC = Math.Min(chroma * 0.18, 12);
                        break;

                    case PaletteStyle.Monochrome:
                        pC  = 4;
                        sH  = hue; sC = 4;
                        tH  = hue; tC = 4;
                        nC  = 2;   nvC = 4;
                        break;

                    case PaletteStyle.Rainbow:
                        pC  = Math.Max(36.0, chroma);
                        sH  = hue + 120;  sC = 28;
                        tH  = hue + 240;  tC = 28;
                        nC  = 6;          nvC = 10;
                        break;

                    case PaletteStyle.FruitSalad:
                        pC  = Math.Max(36.0, chroma);
                        sH  = hue - 50;   sC = 36;
                        tH  = hue + 50;   tC = 36;
                        nC  = 6;          nvC = 10;
                        break;
                }
            }

            // Sanitize hues
            sH  = MathUtils.SanitizeDegrees(sH);
            tH  = MathUtils.SanitizeDegrees(tH);

            return new StyleParams
            {
                PrimaryHue          = pH,
                PrimaryChroma       = pC,
                SecondaryHue        = sH,
                SecondaryChroma     = sC,
                TertiaryHue         = tH,
                TertiaryChroma      = tC,
                NeutralChroma       = nC,
                NeutralVariantChroma = nvC,
            };
        }

        private struct StyleParams
        {
            public double PrimaryHue;
            public double PrimaryChroma;
            public double SecondaryHue;
            public double SecondaryChroma;
            public double TertiaryHue;
            public double TertiaryChroma;
            public double NeutralChroma;
            public double NeutralVariantChroma;
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
                // These are derived from NeutralPalette, not hardcoded gray.
                // ArchiveTune uses 6/98 as surface, giving a faint hue bias.
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

                // All surface/neutral roles come from tonal neutral palettes (not hardcoded gray)
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
        // Used by palette picker to show how a seed looks in the current mode.
        public int GetPreviewPrimaryArgb()  => PrimaryPalette.GetTone(IsDark ? 80 : 40);
        public int GetPreviewSecondaryArgb() => SecondaryPalette.GetTone(IsDark ? 80 : 40);
        public int GetPreviewTertiaryArgb() => TertiaryPalette.GetTone(IsDark ? 80 : 40);
        public int GetPreviewSurfaceArgb()  => NeutralPalette.GetTone(IsDark ? 12 : 94);
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

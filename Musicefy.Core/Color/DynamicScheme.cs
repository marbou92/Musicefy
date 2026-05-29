namespace Musicefy.Core.Hct
{
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
        public SeedPalette Source { get; }

        private DynamicScheme(
            SeedPalette source,
            TonalPalette primary,
            TonalPalette secondary,
            TonalPalette tertiary,
            TonalPalette neutral,
            TonalPalette neutralVariant,
            TonalPalette error,
            bool isDark,
            bool isDarkPure)
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
        }

        public static DynamicScheme FromSeed(SeedPalette seed, bool isDark, bool isDarkPure)
        {
            double chromaFactor = isDarkPure ? 0.6 : 1.0;

            var primary = TonalPalette.FromHueAndChroma(seed.PrimaryHue, seed.PrimaryChroma * chromaFactor);
            var secondary = TonalPalette.FromHueAndChroma(
                seed.PrimaryHue + seed.SecondaryHueOffset,
                seed.PrimaryChroma * seed.SecondaryChromaRatio * chromaFactor);
            var tertiary = TonalPalette.FromHueAndChroma(
                seed.PrimaryHue + seed.TertiaryHueOffset,
                seed.PrimaryChroma * seed.TertiaryChromaRatio * chromaFactor);
            var neutral = TonalPalette.FromHueAndChroma(seed.PrimaryHue, seed.NeutralChroma * chromaFactor);
            var neutralVariant = TonalPalette.FromHueAndChroma(seed.PrimaryHue, seed.NeutralChroma * 2.0 * chromaFactor);
            var error = TonalPalette.FromHueAndChroma(25.0, 84.0);

            return new DynamicScheme(seed, primary, secondary, tertiary, neutral, neutralVariant, error, isDark, isDarkPure);
        }

        public double GetTone(ToneRole role)
        {
            bool dark = IsDark;

            return role switch
            {
                ToneRole.Primary => dark ? 80 : 40,
                ToneRole.OnPrimary => dark ? 20 : 100,
                ToneRole.PrimaryContainer => dark ? 30 : 90,
                ToneRole.OnPrimaryContainer => dark ? 90 : 10,

                ToneRole.Secondary => dark ? 80 : 40,
                ToneRole.OnSecondary => dark ? 20 : 100,
                ToneRole.SecondaryContainer => dark ? 30 : 90,
                ToneRole.OnSecondaryContainer => dark ? 90 : 10,

                ToneRole.Tertiary => dark ? 80 : 40,
                ToneRole.OnTertiary => dark ? 20 : 100,
                ToneRole.TertiaryContainer => dark ? 30 : 90,
                ToneRole.OnTertiaryContainer => dark ? 90 : 10,

                ToneRole.Error => dark ? 80 : 40,
                ToneRole.OnError => dark ? 20 : 100,
                ToneRole.ErrorContainer => dark ? 30 : 90,
                ToneRole.OnErrorContainer => dark ? 90 : 10,

                _ => 50
            };
        }

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

                _ => PrimaryPalette
            };

            return palette.GetTone(GetTone(role));
        }

        public int GetAccentArgb(AccentVariant variant)
        {
            return variant switch
            {
                AccentVariant.Default => PrimaryPalette.GetTone(IsDark ? 80 : 40),
                AccentVariant.Hover => PrimaryPalette.GetTone(IsDark ? 30 : 90),
                AccentVariant.Pressed => PrimaryPalette.GetTone(IsDark ? 50 : 30),
                AccentVariant.Glow => PrimaryPalette.GetTone(IsDark ? 80 : 40),
                _ => PrimaryPalette.GetTone(IsDark ? 80 : 40)
            };
        }
    }

    public enum ToneRole
    {
        Primary,
        OnPrimary,
        PrimaryContainer,
        OnPrimaryContainer,
        Secondary,
        OnSecondary,
        SecondaryContainer,
        OnSecondaryContainer,
        Tertiary,
        OnTertiary,
        TertiaryContainer,
        OnTertiaryContainer,
        Error,
        OnError,
        ErrorContainer,
        OnErrorContainer
    }

    public enum AccentVariant
    {
        Default,
        Hover,
        Pressed,
        Glow
    }
}

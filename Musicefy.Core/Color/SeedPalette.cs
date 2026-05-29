namespace Musicefy.Core.Color
{
    public class SeedPalette
    {
        public string Name { get; }
        public ColorFamily Family { get; }
        public double PrimaryHue { get; }
        public double PrimaryChroma { get; }
        public double SecondaryHueOffset { get; }
        public double SecondaryChromaRatio { get; }
        public double TertiaryHueOffset { get; }
        public double TertiaryChromaRatio { get; }
        public double NeutralChroma { get; }

        public SeedPalette(
            string name,
            ColorFamily family,
            double primaryHue,
            double primaryChroma,
            double secondaryHueOffset = 30.0,
            double secondaryChromaRatio = 1.1,
            double tertiaryHueOffset = 60.0,
            double tertiaryChromaRatio = 0.8,
            double neutralChroma = 4.0)
        {
            Name = name;
            Family = family;
            PrimaryHue = primaryHue;
            PrimaryChroma = primaryChroma;
            SecondaryHueOffset = secondaryHueOffset;
            SecondaryChromaRatio = secondaryChromaRatio;
            TertiaryHueOffset = tertiaryHueOffset;
            TertiaryChromaRatio = tertiaryChromaRatio;
            NeutralChroma = neutralChroma;
        }
    }

    public enum ColorFamily
    {
        Reds,
        Oranges,
        Yellows,
        Greens,
        Teals,
        Blues,
        Indigos,
        Purples,
        Pinks,
        Earth,
        Seasonal,
        Vibrant,
        Neutral
    }
}

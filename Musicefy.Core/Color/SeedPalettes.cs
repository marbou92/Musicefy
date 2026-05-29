namespace Musicefy.Core.Hct
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

        // Pre-computed ARGB seed colors (ArchiveTune-style: 4 independent colors)
        public int PrimaryArgb { get; }
        public int SecondaryArgb { get; }
        public int TertiaryArgb { get; }
        public int NeutralArgb { get; }

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

            // Pre-compute 4 seed ARGB colors at tone 60
            PrimaryArgb = ComputeArgb(primaryHue, primaryChroma);
            SecondaryArgb = ComputeArgb(primaryHue + secondaryHueOffset, primaryChroma * secondaryChromaRatio);
            TertiaryArgb = ComputeArgb(primaryHue + tertiaryHueOffset, primaryChroma * tertiaryChromaRatio);
            NeutralArgb = ComputeArgb(primaryHue, neutralChroma);
        }

        private static int ComputeArgb(double hue, double chroma)
        {
            return Hct.From(hue, System.Math.Max(chroma, 0.5), 60.0).ToInt();
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

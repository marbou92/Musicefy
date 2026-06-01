using System.Collections.Generic;

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

        /// <summary>
        /// Offset from the primary hue used for the neutral palette.
        /// ArchiveTune approach: the neutral palette should be nearly
        /// achromatic to prevent tinted surfaces (e.g. cyan/lavender
        /// backgrounds in light mode). A large offset shifts the
        /// neutral hue away from the primary toward a warm-neutral.
        /// Default is 0 (uses primary hue) for backward compat.
        /// </summary>
        public double NeutralHueOffset { get; }

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
            double neutralChroma = 4.0,
            double neutralHueOffset = 0.0)
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
            NeutralHueOffset = neutralHueOffset;

            PrimaryArgb = ComputeArgb(primaryHue, primaryChroma);
            SecondaryArgb = ComputeArgb(primaryHue + secondaryHueOffset, primaryChroma * secondaryChromaRatio);
            TertiaryArgb = ComputeArgb(primaryHue + tertiaryHueOffset, primaryChroma * tertiaryChromaRatio);
            // Neutral uses the offset hue — if offset is large, this shifts
            // the neutral seed toward a warm/cool neutral instead of primary-tinted
            NeutralArgb = ComputeArgb(primaryHue + neutralHueOffset, neutralChroma);
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

    public static class SeedPalettes
    {
        public static readonly List<SeedPalette> All = new List<SeedPalette>
        {
            // Default palette — ArchiveTune's warm coral-red (#ED5564 approx)
            // This provides a distinctive, warm identity instead of generic purple.
            // NeutralHueOffset shifts neutral palette to warm-neutral (hue ~50)
            // so light mode surfaces stay nearly white instead of lavender-tinted.
            new("Default", ColorFamily.Reds, 350, 44, 40, 1.2, 60, 0.7, 3.0, 50.0),

            // Reds — warm family, neutralHueOffset: 30.0
            new("Crimson", ColorFamily.Reds, 348, 44, 40, 1.2, 60, 0.7, neutralHueOffset: 30.0),
            new("Ruby", ColorFamily.Reds, 340, 38, 35, 1.1, 70, 0.6, neutralHueOffset: 30.0),
            new("Rose", ColorFamily.Reds, 350, 30, 30, 1.0, 65, 0.7, neutralHueOffset: 30.0),
            new("Cherry", ColorFamily.Reds, 355, 50, 45, 1.2, 55, 0.6, neutralHueOffset: 30.0),
            new("Burgundy", ColorFamily.Reds, 345, 28, 40, 0.9, 75, 0.5, 3.0, neutralHueOffset: 30.0),
            new("Scarlet", ColorFamily.Reds, 358, 48, 38, 1.2, 58, 0.6, neutralHueOffset: 30.0),
            new("Raspberry", ColorFamily.Reds, 338, 42, 35, 1.1, 65, 0.7, neutralHueOffset: 30.0),

            // Oranges — warm family, neutralHueOffset: 30.0
            new("Tangerine", ColorFamily.Oranges, 18, 46, 35, 1.2, 50, 0.6, neutralHueOffset: 30.0),
            new("Pumpkin", ColorFamily.Oranges, 22, 52, 40, 1.2, 55, 0.5, neutralHueOffset: 30.0),
            new("Apricot", ColorFamily.Oranges, 28, 30, 30, 1.0, 60, 0.7, neutralHueOffset: 30.0),
            new("Amber", ColorFamily.Oranges, 38, 44, 35, 1.1, 50, 0.6, neutralHueOffset: 30.0),
            new("Coral", ColorFamily.Oranges, 10, 40, 30, 1.1, 55, 0.7, neutralHueOffset: 30.0),
            new("Peach", ColorFamily.Oranges, 14, 28, 30, 1.0, 58, 0.8, neutralHueOffset: 30.0),
            new("Carrot", ColorFamily.Oranges, 25, 48, 35, 1.2, 55, 0.5, neutralHueOffset: 30.0),

            // Yellows — warm family, neutralHueOffset: 30.0
            new("Lemon", ColorFamily.Yellows, 55, 50, 35, 1.0, 50, 0.5, neutralHueOffset: 30.0),
            new("Gold", ColorFamily.Yellows, 48, 42, 40, 1.1, 55, 0.6, neutralHueOffset: 30.0),
            new("Sunflower", ColorFamily.Yellows, 50, 55, 35, 1.1, 50, 0.5, neutralHueOffset: 30.0),
            new("Honey", ColorFamily.Yellows, 42, 38, 35, 1.0, 55, 0.6, neutralHueOffset: 30.0),
            new("Banana", ColorFamily.Yellows, 58, 36, 30, 1.0, 55, 0.7, neutralHueOffset: 30.0),
            new("Mustard", ColorFamily.Yellows, 45, 32, 40, 0.9, 60, 0.6, 6.0, neutralHueOffset: 30.0),

            // Greens — colorful family, neutralHueOffset: 50.0
            new("Emerald", ColorFamily.Greens, 140, 40, 35, 1.1, 55, 0.7, neutralHueOffset: 50.0),
            new("Lime", ColorFamily.Greens, 110, 44, 30, 1.2, 50, 0.6, neutralHueOffset: 50.0),
            new("Forest", ColorFamily.Greens, 130, 32, 40, 0.9, 60, 0.6, 3.0, neutralHueOffset: 50.0),
            new("Olive", ColorFamily.Greens, 100, 28, 35, 0.9, 55, 0.7, 5.0, neutralHueOffset: 50.0),
            new("Mint", ColorFamily.Greens, 155, 34, 30, 1.0, 58, 0.7, neutralHueOffset: 50.0),
            new("Sage", ColorFamily.Greens, 120, 22, 35, 0.9, 60, 0.8, 5.0, neutralHueOffset: 50.0),
            new("Pine", ColorFamily.Greens, 135, 30, 40, 0.9, 65, 0.5, 3.0, neutralHueOffset: 50.0),
            new("Clover", ColorFamily.Greens, 145, 38, 35, 1.1, 55, 0.6, neutralHueOffset: 50.0),

            // Teals — colorful family, neutralHueOffset: 50.0
            new("Teal", ColorFamily.Teals, 175, 36, 35, 1.1, 55, 0.7, 3.0, neutralHueOffset: 50.0),
            new("Cyan", ColorFamily.Teals, 185, 42, 30, 1.1, 50, 0.7, neutralHueOffset: 50.0),
            new("Aqua", ColorFamily.Teals, 190, 30, 30, 1.0, 55, 0.8, neutralHueOffset: 50.0),
            new("Turquoise", ColorFamily.Teals, 170, 44, 35, 1.1, 50, 0.6, neutralHueOffset: 50.0),
            new("Seafoam", ColorFamily.Teals, 165, 26, 30, 1.0, 58, 0.8, 5.0, neutralHueOffset: 50.0),

            // Blues — colorful family, neutralHueOffset: 50.0
            new("Azure", ColorFamily.Blues, 210, 40, 35, 1.1, 55, 0.7, neutralHueOffset: 50.0),
            new("Sky", ColorFamily.Blues, 205, 32, 30, 1.0, 55, 0.8, neutralHueOffset: 50.0),
            new("Ocean", ColorFamily.Blues, 220, 44, 40, 1.1, 60, 0.6, neutralHueOffset: 50.0),
            new("Steel", ColorFamily.Blues, 215, 28, 35, 0.9, 60, 0.7, 5.0, neutralHueOffset: 50.0),
            new("Royal", ColorFamily.Blues, 230, 46, 35, 1.2, 55, 0.6, neutralHueOffset: 50.0),
            new("Cerulean", ColorFamily.Blues, 200, 36, 30, 1.1, 55, 0.7, neutralHueOffset: 50.0),
            new("Sapphire", ColorFamily.Blues, 225, 42, 40, 1.1, 58, 0.6, neutralHueOffset: 50.0),
            new("Navy", ColorFamily.Blues, 240, 30, 40, 0.9, 65, 0.5, 3.0, neutralHueOffset: 50.0),

            // Indigos — colorful family (blue-purple), neutralHueOffset: 50.0
            new("Indigo", ColorFamily.Indigos, 255, 38, 30, 1.1, 55, 0.6, neutralHueOffset: 50.0),
            new("Denim", ColorFamily.Indigos, 248, 32, 35, 1.0, 60, 0.7, 4.0, neutralHueOffset: 50.0),
            new("Periwinkle", ColorFamily.Indigos, 260, 28, 30, 1.0, 55, 0.8, neutralHueOffset: 50.0),
            new("Slate", ColorFamily.Indigos, 250, 20, 35, 0.8, 65, 0.7, 3.0, neutralHueOffset: 50.0),

            // Purples — colorful family, neutralHueOffset: 50.0
            new("Violet", ColorFamily.Purples, 278, 40, 30, 1.1, 55, 0.7, neutralHueOffset: 50.0),
            new("Purple", ColorFamily.Purples, 285, 44, 35, 1.1, 55, 0.6, neutralHueOffset: 50.0),
            new("Amethyst", ColorFamily.Purples, 290, 36, 30, 1.0, 58, 0.7, neutralHueOffset: 50.0),
            new("Orchid", ColorFamily.Purples, 300, 32, 30, 1.0, 55, 0.8, neutralHueOffset: 50.0),
            new("Plum", ColorFamily.Purples, 295, 28, 35, 0.9, 60, 0.6, 3.0, neutralHueOffset: 50.0),
            new("Lavender", ColorFamily.Purples, 275, 26, 28, 1.0, 55, 0.8, 5.0, neutralHueOffset: 50.0),
            new("Wisteria", ColorFamily.Purples, 270, 30, 30, 1.0, 55, 0.7, 4.0, neutralHueOffset: 50.0),

            // Pinks — colorful family, neutralHueOffset: 50.0
            new("Pink", ColorFamily.Pinks, 330, 40, 30, 1.1, 55, 0.7, neutralHueOffset: 50.0),
            new("Magenta", ColorFamily.Pinks, 320, 46, 35, 1.2, 50, 0.6, neutralHueOffset: 50.0),
            new("Fuchsia", ColorFamily.Pinks, 310, 50, 35, 1.2, 50, 0.6, neutralHueOffset: 50.0),
            new("Blush", ColorFamily.Pinks, 340, 28, 30, 1.0, 58, 0.8, 5.0, neutralHueOffset: 50.0),
            new("Rosebud", ColorFamily.Pinks, 345, 34, 30, 1.1, 55, 0.7, neutralHueOffset: 50.0),
            new("Bubblegum", ColorFamily.Pinks, 335, 36, 30, 1.0, 55, 0.7, neutralHueOffset: 50.0),

            // Earth — warm family, neutralHueOffset: 30.0
            new("Brown", ColorFamily.Earth, 20, 16, 35, 0.9, 60, 0.7, 3.0, neutralHueOffset: 30.0),
            new("Sand", ColorFamily.Earth, 35, 14, 30, 0.9, 58, 0.8, 5.0, neutralHueOffset: 30.0),
            new("Clay", ColorFamily.Earth, 15, 20, 35, 1.0, 55, 0.7, 4.0, neutralHueOffset: 30.0),
            new("Taupe", ColorFamily.Earth, 30, 10, 35, 0.8, 65, 0.8, 3.0, neutralHueOffset: 30.0),
            new("Coffee", ColorFamily.Earth, 25, 18, 40, 0.9, 60, 0.6, 3.0, neutralHueOffset: 30.0),
            new("Terracotta", ColorFamily.Earth, 12, 30, 35, 1.1, 55, 0.6, neutralHueOffset: 30.0),
            new("Walnut", ColorFamily.Earth, 28, 14, 40, 0.8, 65, 0.7, 3.0, neutralHueOffset: 30.0),

            // Seasonal — warm family, neutralHueOffset: 30.0
            new("Winter Frost", ColorFamily.Seasonal, 210, 18, 35, 0.8, 65, 0.8, 3.0, neutralHueOffset: 30.0),
            new("Spring", ColorFamily.Seasonal, 120, 35, 30, 1.1, 55, 0.7, neutralHueOffset: 30.0),
            new("Summer", ColorFamily.Seasonal, 195, 38, 30, 1.1, 55, 0.7, neutralHueOffset: 30.0),
            new("Autumn", ColorFamily.Seasonal, 22, 40, 40, 1.2, 55, 0.5, neutralHueOffset: 30.0),
            new("Sunset", ColorFamily.Seasonal, 350, 44, 50, 1.1, 70, 0.5, neutralHueOffset: 30.0),

            // Vibrant — colorful family, neutralHueOffset: 50.0
            new("Electric Blue", ColorFamily.Vibrant, 220, 55, 35, 1.2, 55, 0.5, neutralHueOffset: 50.0),
            new("Neon Green", ColorFamily.Vibrant, 115, 58, 30, 1.2, 50, 0.4, neutralHueOffset: 50.0),
            new("Hot Pink", ColorFamily.Vibrant, 335, 54, 30, 1.2, 55, 0.5, neutralHueOffset: 50.0),
            new("Cyber Yellow", ColorFamily.Vibrant, 52, 60, 35, 1.1, 50, 0.4, neutralHueOffset: 50.0),
            new("Volt", ColorFamily.Vibrant, 95, 56, 30, 1.2, 50, 0.5, neutralHueOffset: 50.0),

            // Neutral — already near-achromatic, neutralHueOffset: 0.0
            new("Gray", ColorFamily.Neutral, 0, 2, 30, 1.0, 60, 1.0, 2.0, neutralHueOffset: 0.0),
            new("Slate", ColorFamily.Neutral, 250, 4, 35, 0.9, 65, 0.9, 2.0, neutralHueOffset: 0.0),
            new("Charcoal", ColorFamily.Neutral, 0, 1, 30, 0.8, 60, 1.0, 1.5, neutralHueOffset: 0.0),
            new("Warm Stone", ColorFamily.Neutral, 35, 6, 30, 0.9, 60, 0.9, 3.0, neutralHueOffset: 0.0),
            new("Cool Stone", ColorFamily.Neutral, 200, 5, 35, 0.9, 65, 0.9, 2.5, neutralHueOffset: 0.0),
            new("Pearl", ColorFamily.Neutral, 40, 4, 30, 1.0, 60, 1.0, 3.0, neutralHueOffset: 0.0),
        };
    }
}

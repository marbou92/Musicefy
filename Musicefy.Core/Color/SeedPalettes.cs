using System.Collections.Generic;

namespace Musicefy.Core.Hct
{
    public static class SeedPalettes
    {
        public static readonly List<SeedPalette> All = new List<SeedPalette>
        {
            // ── Reds ──
            new("Crimson", ColorFamily.Reds, 348, 44, 40, 1.2, 60, 0.7),
            new("Ruby", ColorFamily.Reds, 340, 38, 35, 1.1, 70, 0.6),
            new("Rose", ColorFamily.Reds, 350, 30, 30, 1.0, 65, 0.7),
            new("Cherry", ColorFamily.Reds, 355, 50, 45, 1.2, 55, 0.6),
            new("Burgundy", ColorFamily.Reds, 345, 28, 40, 0.9, 75, 0.5, 3.0),
            new("Scarlet", ColorFamily.Reds, 358, 48, 38, 1.2, 58, 0.6),
            new("Raspberry", ColorFamily.Reds, 338, 42, 35, 1.1, 65, 0.7),

            // ── Oranges ──
            new("Tangerine", ColorFamily.Oranges, 18, 46, 35, 1.2, 50, 0.6),
            new("Pumpkin", ColorFamily.Oranges, 22, 52, 40, 1.2, 55, 0.5),
            new("Apricot", ColorFamily.Oranges, 28, 30, 30, 1.0, 60, 0.7),
            new("Amber", ColorFamily.Oranges, 38, 44, 35, 1.1, 50, 0.6),
            new("Coral", ColorFamily.Oranges, 10, 40, 30, 1.1, 55, 0.7),
            new("Peach", ColorFamily.Oranges, 14, 28, 30, 1.0, 58, 0.8),
            new("Carrot", ColorFamily.Oranges, 25, 48, 35, 1.2, 55, 0.5),

            // ── Yellows ──
            new("Lemon", ColorFamily.Yellows, 55, 50, 35, 1.0, 50, 0.5),
            new("Gold", ColorFamily.Yellows, 48, 42, 40, 1.1, 55, 0.6),
            new("Sunflower", ColorFamily.Yellows, 50, 55, 35, 1.1, 50, 0.5),
            new("Honey", ColorFamily.Yellows, 42, 38, 35, 1.0, 55, 0.6),
            new("Banana", ColorFamily.Yellows, 58, 36, 30, 1.0, 55, 0.7),
            new("Mustard", ColorFamily.Yellows, 45, 32, 40, 0.9, 60, 0.6, 6.0),

            // ── Greens ──
            new("Emerald", ColorFamily.Greens, 140, 40, 35, 1.1, 55, 0.7),
            new("Lime", ColorFamily.Greens, 110, 44, 30, 1.2, 50, 0.6),
            new("Forest", ColorFamily.Greens, 130, 32, 40, 0.9, 60, 0.6, 3.0),
            new("Olive", ColorFamily.Greens, 100, 28, 35, 0.9, 55, 0.7, 5.0),
            new("Mint", ColorFamily.Greens, 155, 34, 30, 1.0, 58, 0.7),
            new("Sage", ColorFamily.Greens, 120, 22, 35, 0.9, 60, 0.8, 5.0),
            new("Pine", ColorFamily.Greens, 135, 30, 40, 0.9, 65, 0.5, 3.0),
            new("Clover", ColorFamily.Greens, 145, 38, 35, 1.1, 55, 0.6),

            // ── Teals ──
            new("Teal", ColorFamily.Teals, 175, 36, 35, 1.1, 55, 0.7, 3.0),
            new("Cyan", ColorFamily.Teals, 185, 42, 30, 1.1, 50, 0.7),
            new("Aqua", ColorFamily.Teals, 190, 30, 30, 1.0, 55, 0.8),
            new("Turquoise", ColorFamily.Teals, 170, 44, 35, 1.1, 50, 0.6),
            new("Seafoam", ColorFamily.Teals, 165, 26, 30, 1.0, 58, 0.8, 5.0),

            // ── Blues ──
            new("Azure", ColorFamily.Blues, 210, 40, 35, 1.1, 55, 0.7),
            new("Sky", ColorFamily.Blues, 205, 32, 30, 1.0, 55, 0.8),
            new("Ocean", ColorFamily.Blues, 220, 44, 40, 1.1, 60, 0.6),
            new("Steel", ColorFamily.Blues, 215, 28, 35, 0.9, 60, 0.7, 5.0),
            new("Royal", ColorFamily.Blues, 230, 46, 35, 1.2, 55, 0.6),
            new("Cerulean", ColorFamily.Blues, 200, 36, 30, 1.1, 55, 0.7),
            new("Sapphire", ColorFamily.Blues, 225, 42, 40, 1.1, 58, 0.6),
            new("Navy", ColorFamily.Blues, 240, 30, 40, 0.9, 65, 0.5, 3.0),

            // ── Indigos ──
            new("Indigo", ColorFamily.Indigos, 255, 38, 30, 1.1, 55, 0.6),
            new("Denim", ColorFamily.Indigos, 248, 32, 35, 1.0, 60, 0.7, 4.0),
            new("Periwinkle", ColorFamily.Indigos, 260, 28, 30, 1.0, 55, 0.8),
            new("Slate", ColorFamily.Indigos, 250, 20, 35, 0.8, 65, 0.7, 3.0),

            // ── Purples ──
            new("Violet", ColorFamily.Purples, 278, 40, 30, 1.1, 55, 0.7),
            new("Purple", ColorFamily.Purples, 285, 44, 35, 1.1, 55, 0.6),
            new("Amethyst", ColorFamily.Purples, 290, 36, 30, 1.0, 58, 0.7),
            new("Orchid", ColorFamily.Purples, 300, 32, 30, 1.0, 55, 0.8),
            new("Plum", ColorFamily.Purples, 295, 28, 35, 0.9, 60, 0.6, 3.0),
            new("Lavender", ColorFamily.Purples, 275, 26, 28, 1.0, 55, 0.8, 5.0),
            new("Wisteria", ColorFamily.Purples, 270, 30, 30, 1.0, 55, 0.7, 4.0),

            // ── Pinks ──
            new("Pink", ColorFamily.Pinks, 330, 40, 30, 1.1, 55, 0.7),
            new("Magenta", ColorFamily.Pinks, 320, 46, 35, 1.2, 50, 0.6),
            new("Fuchsia", ColorFamily.Pinks, 310, 50, 35, 1.2, 50, 0.6),
            new("Blush", ColorFamily.Pinks, 340, 28, 30, 1.0, 58, 0.8, 5.0),
            new("Rosebud", ColorFamily.Pinks, 345, 34, 30, 1.1, 55, 0.7),
            new("Bubblegum", ColorFamily.Pinks, 335, 36, 30, 1.0, 55, 0.7),

            // ── Earth ──
            new("Brown", ColorFamily.Earth, 20, 16, 35, 0.9, 60, 0.7, 3.0),
            new("Sand", ColorFamily.Earth, 35, 14, 30, 0.9, 58, 0.8, 5.0),
            new("Clay", ColorFamily.Earth, 15, 20, 35, 1.0, 55, 0.7, 4.0),
            new("Taupe", ColorFamily.Earth, 30, 10, 35, 0.8, 65, 0.8, 3.0),
            new("Coffee", ColorFamily.Earth, 25, 18, 40, 0.9, 60, 0.6, 3.0),
            new("Terracotta", ColorFamily.Earth, 12, 30, 35, 1.1, 55, 0.6),
            new("Walnut", ColorFamily.Earth, 28, 14, 40, 0.8, 65, 0.7, 3.0),

            // ── Seasonal ──
            new("Winter Frost", ColorFamily.Seasonal, 210, 18, 35, 0.8, 65, 0.8, 3.0),
            new("Spring", ColorFamily.Seasonal, 120, 35, 30, 1.1, 55, 0.7),
            new("Summer", ColorFamily.Seasonal, 195, 38, 30, 1.1, 55, 0.7),
            new("Autumn", ColorFamily.Seasonal, 22, 40, 40, 1.2, 55, 0.5),
            new("Sunset", ColorFamily.Seasonal, 350, 44, 50, 1.1, 70, 0.5),

            // ── Vibrant ──
            new("Electric Blue", ColorFamily.Vibrant, 220, 55, 35, 1.2, 55, 0.5),
            new("Neon Green", ColorFamily.Vibrant, 115, 58, 30, 1.2, 50, 0.4),
            new("Hot Pink", ColorFamily.Vibrant, 335, 54, 30, 1.2, 55, 0.5),
            new("Cyber Yellow", ColorFamily.Vibrant, 52, 60, 35, 1.1, 50, 0.4),
            new("Volt", ColorFamily.Vibrant, 95, 56, 30, 1.2, 50, 0.5),

            // ── Neutral ──
            new("Gray", ColorFamily.Neutral, 0, 2, 30, 1.0, 60, 1.0, 2.0),
            new("Slate", ColorFamily.Neutral, 250, 4, 35, 0.9, 65, 0.9, 2.0),
            new("Charcoal", ColorFamily.Neutral, 0, 1, 30, 0.8, 60, 1.0, 1.5),
            new("Warm Stone", ColorFamily.Neutral, 35, 6, 30, 0.9, 60, 0.9, 3.0),
            new("Cool Stone", ColorFamily.Neutral, 200, 5, 35, 0.9, 65, 0.9, 2.5),
            new("Pearl", ColorFamily.Neutral, 40, 4, 30, 1.0, 60, 1.0, 3.0),
        };
    }
}

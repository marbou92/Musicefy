using System;
using System.Collections.Generic;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Color = System.Windows.Media.Color;

namespace Musicefy.Core.Services
{
    public class ExtractedColors
    {
        public Color Primary { get; set; }
        public Color Vibrant { get; set; }
        public Color Muted { get; set; }
        public Color OnPrimary { get; set; }
        public Color Surface { get; set; }
    }

    public static class ColorExtractor
    {
        public static ExtractedColors Extract(BitmapSource source)
        {
            if (source == null)
                return GetDefaults();

            try
            {
                var resized = new FormatConvertedBitmap(source, PixelFormats.Bgr32, null, 0);
                int w = Math.Min(resized.PixelWidth, 64);
                int h = Math.Min(resized.PixelHeight, 64);

                var scaled = new FormatConvertedBitmap(source, PixelFormats.Bgr32, null, 0);
                if (source.PixelWidth != w || source.PixelHeight != h)
                {
                    scaled = new FormatConvertedBitmap(source, PixelFormats.Bgr32, null, 0);
                    var target = new TransformedBitmap(scaled,
                        new System.Windows.Media.ScaleTransform(
                            (double)w / source.PixelWidth,
                            (double)h / source.PixelHeight));
                    scaled = new FormatConvertedBitmap(target, PixelFormats.Bgr32, null, 0);
                }

                int stride = w * 3;
                byte[] pixels = new byte[stride * h];
                scaled.CopyPixels(pixels, stride, 0);

                var colorBuckets = new Dictionary<int, int>();
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        int idx = y * stride + x * 3;
                        byte b = pixels[idx];
                        byte g = pixels[idx + 1];
                        byte r = pixels[idx + 2];

                        if (r < 20 && g < 20 && b < 20) continue;
                        if (r > 240 && g > 240 && b > 240) continue;

                        int key = ((r >> 3) << 10) | ((g >> 3) << 5) | (b >> 3);
                        colorBuckets.TryGetValue(key, out int count);
                        colorBuckets[key] = count + 1;
                    }
                }

                var sorted = new List<KeyValuePair<int, int>>(colorBuckets);
                sorted.Sort((a, b) => b.Value.CompareTo(a.Value));

                var dominant = new List<ColorBucket>();
                foreach (var kv in sorted)
                {
                    if (dominant.Count >= 8) break;
                    int r = ((kv.Key >> 10) & 0x1F) << 3;
                    int g = ((kv.Key >> 5) & 0x1F) << 3;
                    int b = (kv.Key & 0x1F) << 3;
                    dominant.Add(new ColorBucket
                    {
                        Color = Color.FromRgb((byte)r, (byte)g, (byte)b),
                        Count = kv.Value
                    });
                }

                if (dominant.Count == 0)
                    return GetDefaults();

                var primary = dominant[0].Color;
                var vibrant = FindVibrant(dominant);
                var muted = FindMuted(dominant, primary);
                var onPrimary = GetContrastColor(primary);
                var surface = Darken(primary, 0.3);

                return new ExtractedColors
                {
                    Primary = primary,
                    Vibrant = vibrant,
                    Muted = muted,
                    OnPrimary = onPrimary,
                    Surface = surface
                };
            }
            catch
            {
                return GetDefaults();
            }
        }

        private static Color FindVibrant(List<ColorBucket> buckets)
        {
            Color best = buckets[0].Color;
            double bestScore = 0;
            foreach (var b in buckets)
            {
                double saturation = GetSaturation(b.Color);
                double brightness = GetBrightness(b.Color);
                double score = saturation * 0.6 + brightness * 0.4;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = b.Color;
                }
            }
            return best;
        }

        private static Color FindMuted(List<ColorBucket> buckets, Color primary)
        {
            double pSat = GetSaturation(primary);
            Color best = buckets[0].Color;
            double bestDist = double.MaxValue;
            foreach (var b in buckets)
            {
                double dist = Math.Abs(GetSaturation(b.Color) - pSat * 0.4);
                double hueDist = Math.Abs(GetHue(b.Color) - GetHue(primary));
                double total = dist + hueDist * 0.3;
                if (total < bestDist)
                {
                    bestDist = total;
                    best = b.Color;
                }
            }
            return best;
        }

        private static double GetSaturation(Color c)
        {
            double max = Math.Max(c.R, Math.Max(c.G, c.B));
            double min = Math.Min(c.R, Math.Min(c.G, c.B));
            if (max == 0) return 0;
            return (max - min) / (double)max;
        }

        private static double GetBrightness(Color c)
        {
            return (c.R * 0.299 + c.G * 0.587 + c.B * 0.114) / 255.0;
        }

        private static double GetHue(Color c)
        {
            double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
            double max = Math.Max(r, Math.Max(g, b));
            double min = Math.Min(r, Math.Min(g, b));
            double d = max - min;
            if (d == 0) return 0;
            double h = 0;
            if (max == r) h = ((g - b) / d) % 6;
            else if (max == g) h = (b - r) / d + 2;
            else h = (r - g) / d + 4;
            h *= 60;
            if (h < 0) h += 360;
            return h;
        }

        private static Color GetContrastColor(Color c)
        {
            double brightness = GetBrightness(c);
            return brightness > 0.5 ? Color.FromRgb(30, 30, 30) : Color.FromRgb(240, 240, 240);
        }

        private static Color Darken(Color c, double factor)
        {
            return Color.FromRgb(
                (byte)(c.R * (1 - factor)),
                (byte)(c.G * (1 - factor)),
                (byte)(c.B * (1 - factor)));
        }

        private static ExtractedColors GetDefaults()
        {
            return new ExtractedColors
            {
                Primary = Color.FromRgb(60, 140, 231),
                Vibrant = Color.FromRgb(60, 140, 231),
                Muted = Color.FromRgb(80, 100, 140),
                OnPrimary = Colors.White,
                Surface = Color.FromRgb(20, 25, 40)
            };
        }

        private class ColorBucket
        {
            public Color Color { get; set; }
            public int Count { get; set; }
        }
    }
}

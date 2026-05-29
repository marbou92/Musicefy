using System;

namespace Musicefy.Core.Hct
{
    public struct Hct
    {
        public double Hue { get; }
        public double Chroma { get; }
        public double Tone { get; }

        private Hct(double hue, double chroma, double tone)
        {
            Hue = hue;
            Chroma = chroma;
            Tone = tone;
        }

        public static Hct From(double hue, double chroma, double tone)
        {
            int argb = SolveToInt(MathUtils.SanitizeDegrees(hue), chroma, tone);
            return FromInt(argb);
        }

        public static Hct FromInt(int argb)
        {
            double[] xyz = ColorUtils.XyzFromArgb(argb);
            double tone = ColorUtils.LstarFromY(xyz[1]);
            var cam = Cam16.FromInt(argb);
            return new Hct(cam.Hue, cam.Chroma, tone);
        }

        public int ToInt()
        {
            return SolveToInt(Hue, Chroma, Tone);
        }

        private static int SolveToInt(double hue, double chroma, double tone)
        {
            if (tone <= 0.0) return -16777216;
            if (tone >= 100.0) return -1;

            chroma = Math.Max(chroma, 0.0);

            double maxChroma = FindMaxChroma(hue, tone);
            if (chroma > maxChroma) chroma = maxChroma;

            double l = tone;
            double hueRad = hue * Math.PI / 180.0;

            double a = chroma * Math.Cos(hueRad);
            double b = chroma * Math.Sin(hueRad);

            int argb = ColorUtils.ArgbFromLab(l, a, b);

            var cam = Cam16.FromInt(argb);
                double hueDiff = MathUtils.DifferenceDegrees(cam.Hue, hue);

                if ((hueDiff > 2.0 || Math.Abs(cam.Chroma - chroma) / Math.Max(chroma, 1.0) > 0.05)
                    && chroma > 10.0 && tone > 10.0 && tone < 90.0)
                {
                    argb = SolveIterative(l, a, b, hue, chroma);
                }

            return argb;
        }

        private static int SolveIterative(double l, double a, double b, double targetHue, double targetChroma)
        {
            double targetHueRad = targetHue * Math.PI / 180.0;

            for (int i = 0; i < 10; i++)
            {
                int argb = ColorUtils.ArgbFromLab(l, a, b);
                var cam = Cam16.FromInt(argb);
                double hueDiff = MathUtils.DifferenceDegrees(cam.Hue, targetHue);

                if (hueDiff < 0.5 && Math.Abs(cam.Chroma - targetChroma) / Math.Max(targetChroma, 1.0) < 0.02)
                    break;

                double currentHueRad = cam.Hue * Math.PI / 180.0;
                double angleDiff = targetHueRad - currentHueRad;
                double cosA = Math.Cos(angleDiff * 0.6);
                double sinA = Math.Sin(angleDiff * 0.6);
                double newA = a * cosA - b * sinA;
                double newB = a * sinA + b * cosA;

                double chromaScale = targetChroma / Math.Max(cam.Chroma, 0.01);
                chromaScale = MathUtils.Clamp(0.1, 10.0, chromaScale);
                a = newA * chromaScale;
                b = newB * chromaScale;
            }

            return ColorUtils.ArgbFromLab(l, a, b);
        }

        private static double FindMaxChroma(double hue, double tone)
        {
            double hueRad = hue * Math.PI / 180.0;
            double low = 0.0, high = 200.0;

            for (int i = 0; i < 15; i++)
            {
                double mid = (low + high) / 2.0;
                double a = mid * Math.Cos(hueRad);
                double b = mid * Math.Sin(hueRad);
                int argb = ColorUtils.ArgbFromLab(tone, a, b);
                double[] xyz = ColorUtils.XyzFromArgb(argb);
                if (ColorUtils.IsInSrgbGamut(xyz))
                    low = mid;
                else
                    high = mid;
            }
            return low;
        }
    }
}

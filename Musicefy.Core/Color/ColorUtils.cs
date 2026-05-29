using System;

namespace Musicefy.Core.Hct
{
    internal static class ColorUtils
    {
        public const double SRgbToXyzScale = 100.0;

        public static int ArgbFromRgb(int red, int green, int blue)
        {
            return (255 << 24) | ((red & 0xFF) << 16) | ((green & 0xFF) << 8) | (blue & 0xFF);
        }

        public static int AlphaFromArgb(int argb) => (argb >> 24) & 0xFF;
        public static int RedFromArgb(int argb) => (argb >> 16) & 0xFF;
        public static int GreenFromArgb(int argb) => (argb >> 8) & 0xFF;
        public static int BlueFromArgb(int argb) => argb & 0xFF;

        public static bool IsOpaque(int argb) => AlphaFromArgb(argb) >= 255;

        public static int ArgbFromLinrgb(double[] linrgb)
        {
            int r = Delinearized(linrgb[0]);
            int g = Delinearized(linrgb[1]);
            int b = Delinearized(linrgb[2]);
            return ArgbFromRgb(r, g, b);
        }

        public static double[] LinrgbFromArgb(int argb)
        {
            int r = RedFromArgb(argb);
            int g = GreenFromArgb(argb);
            int b = BlueFromArgb(argb);
            return new[] { Linearized(r), Linearized(g), Linearized(b) };
        }

        public static double Linearized(int rgbComponent)
        {
            double normalized = rgbComponent / 255.0;
            if (normalized <= 0.040449936)
                return normalized / 12.92;
            return Math.Pow((normalized + 0.055) / 1.055, 2.4);
        }

        public static int Delinearized(double rgbComponent)
        {
            double normalized;
            if (rgbComponent <= 0.0031308)
                normalized = rgbComponent * 12.92;
            else
                normalized = 1.055 * Math.Pow(rgbComponent, 1.0 / 2.4) - 0.055;
            return (int)Math.Round(normalized * 255.0);
        }

        public static double[] XyzFromArgb(int argb)
        {
            double[] linrgb = LinrgbFromArgb(argb);
            double m00 = 0.41233895, m01 = 0.35762064, m02 = 0.18051042;
            double m10 = 0.2126, m11 = 0.7152, m12 = 0.0722;
            double m20 = 0.01932141, m21 = 0.11916382, m22 = 0.95034478;
            double x = (m00 * linrgb[0] + m01 * linrgb[1] + m02 * linrgb[2]) * SRgbToXyzScale;
            double y = (m10 * linrgb[0] + m11 * linrgb[1] + m12 * linrgb[2]) * SRgbToXyzScale;
            double z = (m20 * linrgb[0] + m21 * linrgb[1] + m22 * linrgb[2]) * SRgbToXyzScale;
            return new[] { x, y, z };
        }

        public static int ArgbFromXyz(double x, double y, double z)
        {
            double m00 = 3.2413774792388685, m01 = -1.5376652402851851, m02 = -0.49885366846268053;
            double m10 = -0.9691452513005321, m11 = 1.8758853451067872, m12 = 0.04156585616912061;
            double m20 = 0.05562093689691305, m21 = -0.20395524564742123, m22 = 1.0571799111220335;
            double lr = m00 * x + m01 * y + m02 * z;
            double lg = m10 * x + m11 * y + m12 * z;
            double lb = m20 * x + m21 * y + m22 * z;
            return ArgbFromLinrgb(new[] { lr / SRgbToXyzScale, lg / SRgbToXyzScale, lb / SRgbToXyzScale });
        }

        public static double[] LabFromArgb(int argb)
        {
            double[] xyz = XyzFromArgb(argb);
            double x = xyz[0] / 95.047, y = xyz[1] / 100.0, z = xyz[2] / 108.883;
            double fx = LabF(x), fy = LabF(y), fz = LabF(z);
            double l = 116.0 * fy - 16.0;
            double a = 500.0 * (fx - fy);
            double b = 200.0 * (fy - fz);
            return new[] { l, a, b };
        }

        public static int ArgbFromLab(double l, double a, double b)
        {
            double fy = (l + 16.0) / 116.0;
            double fx = a / 500.0 + fy;
            double fz = fy - b / 200.0;
            double x = 95.047 * LabInvF(fx);
            double y = 100.0 * LabInvF(fy);
            double z = 108.883 * LabInvF(fz);
            return ArgbFromXyz(x, y, z);
        }

        public static double YFromLstar(double lstar)
        {
            double ke = 8.0;
            if (lstar > ke)
            {
                double v = (lstar + 16.0) / 116.0;
                return v * v * v * 100.0;
            }
            return lstar / (24389.0 / 27.0) * 100.0;
        }

        public static double LstarFromY(double y)
        {
            double yNorm = y / 100.0;
            double ke = 216.0 / 24389.0;
            if (yNorm > ke)
            {
                double v = Math.Pow(yNorm, 1.0 / 3.0);
                return 116.0 * v - 16.0;
            }
            return (24389.0 / 27.0) * yNorm;
        }

        public static double LstarFromArgb(int argb)
        {
            double y = XyzFromArgb(argb)[1];
            return LstarFromY(y);
        }

        public static bool IsInSrgbGamut(double[] xyz)
        {
            double m00 = 3.2413774792388685, m01 = -1.5376652402851851, m02 = -0.49885366846268053;
            double m10 = -0.9691452513005321, m11 = 1.8758853451067872, m12 = 0.04156585616912061;
            double m20 = 0.05562093689691305, m21 = -0.20395524564742123, m22 = 1.0571799111220335;
            double lr = m00 * xyz[0] + m01 * xyz[1] + m02 * xyz[2];
            double lg = m10 * xyz[0] + m11 * xyz[1] + m12 * xyz[2];
            double lb = m20 * xyz[0] + m21 * xyz[1] + m22 * xyz[2];
            return lr >= 0 && lr <= SRgbToXyzScale && lg >= 0 && lg <= SRgbToXyzScale && lb >= 0 && lb <= SRgbToXyzScale;
        }

        public static int ArgbFromXyzInSrgbGamut(double x, double y, double z)
        {
            int rgb = ArgbFromXyz(x, y, z);
            if (!IsOpaque(rgb)) return -16777216;
            int r = RedFromArgb(rgb), g = GreenFromArgb(rgb), b = BlueFromArgb(rgb);
            return ArgbFromRgb(
                MathUtils.ClampInt(0, 255, r),
                MathUtils.ClampInt(0, 255, g),
                MathUtils.ClampInt(0, 255, b));
        }

        public static double LabF(double t)
        {
            double delta = 6.0 / 29.0;
            if (t > delta * delta * delta) return Math.Pow(t, 1.0 / 3.0);
            return t / (3.0 * delta * delta) + 4.0 / 29.0;
        }

        private static double LabInvF(double ft)
        {
            double delta = 6.0 / 29.0;
            if (ft > delta) return ft * ft * ft;
            return 3.0 * delta * delta * (ft - 4.0 / 29.0);
        }
    }
}

using System;

namespace Musicefy.Core.Color
{
    internal static class MathUtils
    {
        public static double Clamp(double min, double max, double value)
        {
            return Math.Min(Math.Max(value, min), max);
        }

        public static int ClampInt(int min, int max, int value)
        {
            return Math.Min(Math.Max(value, min), max);
        }

        public static double SanitizeDegrees(double degrees)
        {
            degrees %= 360.0;
            if (degrees < 0) degrees += 360.0;
            return degrees;
        }

        public static double DifferenceDegrees(double a, double b)
        {
            return 180.0 - Math.Abs(Math.Abs(a - b) - 180.0);
        }

        public static double RotationDirection(double from, double to)
        {
            double diff = to - from;
            double sign = Math.Sign(diff);
            if (diff == 0) return 0;
            double diffDegrees = SanitizeDegrees(diff);
            return diffDegrees <= 180.0 ? sign : -sign;
        }

        public static double Signum(double value)
        {
            if (value < 0) return -1;
            if (value > 0) return 1;
            return 0;
        }

        public static double Lerp(double from, double to, double amount)
        {
            return from + (to - from) * amount;
        }
    }
}

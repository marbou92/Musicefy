using System;

namespace Musicefy.Core.Hct
{
    internal class Cam16
    {
        public double Hue { get; }
        public double Chroma { get; }
        public double J { get; }
        public double Q { get; }
        public double M { get; }
        public double S { get; }

        private Cam16(double hue, double chroma, double j, double q, double m, double s)
        {
            Hue = hue;
            Chroma = chroma;
            J = j;
            Q = q;
            M = m;
            S = s;
        }

        public static Cam16 FromInt(int argb)
        {
            return FromXyzInViewingConditions(
                ColorUtils.XyzFromArgb(argb),
                ViewingConditions.Standard);
        }

        public static Cam16 FromXyzInViewingConditions(double[] xyz, ViewingConditions vc)
        {
            double x = xyz[0], y = xyz[1], z = xyz[2];

            double rC = 0.401288 * x + 0.650173 * y - 0.051461 * z;
            double gC = -0.250268 * x + 1.204414 * y + 0.045854 * z;
            double bC = -0.002079 * x + 0.048952 * y + 0.953127 * z;

            double rD = vc.RgbD[0] * rC;
            double gD = vc.RgbD[1] * gC;
            double bD = vc.RgbD[2] * bC;

            double rA = vc.Fl * Math.Pow(Math.Abs(rD), 0.42) * Math.Sign(rD);
            double gA = vc.Fl * Math.Pow(Math.Abs(gD), 0.42) * Math.Sign(gD);
            double bA = vc.Fl * Math.Pow(Math.Abs(bD), 0.42) * Math.Sign(bD);

            double a = (-12.0 * rA + 27.0 * gA - 3.0 * bA) / 11.0;
            double b = (20.0 * rA + 20.0 * gA - 40.0 * bA) / 11.0;
            double u = (rA + gA + bA) * 20.0 / 11.0;

            double hue = Math.Atan2(b, a) * 180.0 / Math.PI;
            if (hue < 0) hue += 360;

            double p1 = 12500.0 / 13.0 * vc.Nc * vc.Ncb *
                (a * a + b * b) / (u * u + 0.005);
            double t = p1 * Math.Sqrt(1.0 + 0.005 * u * u / (a * a + b * b));

            double j = 100.0 * Math.Pow(u / vc.Aw, vc.C * vc.Z);
            double q = 4.0 / vc.C * Math.Sqrt(j / 100.0) * (vc.Aw + 4.0) * Math.Pow(vc.Fl, 0.25);
            double c = Math.Sqrt(t) * Math.Pow(j / 100.0, 0.5);
            double m = c * Math.Pow(vc.Fl, 0.25);
            double s = 100.0 * Math.Sqrt(m / vc.Aw + 0.0001);

            return new Cam16(hue, c, j, q, m, s);
        }

        public int ToInt()
        {
            return ToXyzInViewingConditions(ViewingConditions.Standard).ToInt();
        }

        public XyzColor ToXyzInViewingConditions(ViewingConditions vc)
        {
            double j = J;
            if (j < 0) j = 0;
            if (j > 100) j = 100;

            double alpha = Chroma <= 0 ? 0 : Chroma / Math.Sqrt(j / 100.0);
            double t = alpha * alpha;
            double u = (20.0 / 11.0) * vc.Aw * Math.Pow(j / 100.0, 1.0 / (vc.C * vc.Z));

            double hueRad = Hue * Math.PI / 180.0;
            double a = Math.Cos(hueRad);
            double b = Math.Sin(hueRad);

            double p1 = 12500.0 / 13.0 * vc.Nc * vc.Ncb;
            double p2 = 0.005 * u * u / (a * a + b * b);
            double p3 = p1 * (a * a + b * b) / (t + p2);
            double p4 = 2.0 * p3 / (1.0 + Math.Sqrt(1.0 + 4.0 * p3 / (u * u)));

            double aPrime = p4 * a;
            double bPrime = p4 * b;

            double rAPrime = (20.0 * u + 12.0 * aPrime + 327.0 * bPrime) / 61.0;
            double gAPrime = (20.0 * u - 27.0 * aPrime + 12.0 * bPrime) / 61.0;
            double bAPrime = (20.0 * u - 235.0 * aPrime - 12.0 * bPrime) / 61.0;

            double rDPrime = Math.Pow(Math.Abs(rAPrime) / vc.Fl, 1.0 / 0.42) * Math.Sign(rAPrime);
            double gDPrime = Math.Pow(Math.Abs(gAPrime) / vc.Fl, 1.0 / 0.42) * Math.Sign(gAPrime);
            double bDPrime = Math.Pow(Math.Abs(bAPrime) / vc.Fl, 1.0 / 0.42) * Math.Sign(bAPrime);

            double[] invRgbD = vc.InvRgbD;
            double rC = rDPrime * invRgbD[0];
            double gC = gDPrime * invRgbD[1];
            double bC = bDPrime * invRgbD[2];

            double x = 1.86206786 * rC - 1.01125463 * gC + 0.14918677 * bC;
            double y = 0.38752654 * rC + 0.62144744 * gC - 0.00897398 * bC;
            double z = -0.01584150 * rC - 0.03412294 * gC + 1.04996444 * bC;

            return new XyzColor(x, y, z);
        }

        public struct XyzColor
        {
            public readonly double X, Y, Z;

            public XyzColor(double x, double y, double z)
            {
                X = x; Y = y; Z = z;
            }

            public int ToInt()
            {
                return ColorUtils.ArgbFromXyz(X, Y, Z);
            }
        }

        internal class ViewingConditions
        {
            public static readonly ViewingConditions Standard = MakeStandard();

            public double N { get; }
            public double Aw { get; }
            public double Nbb { get; }
            public double Ncb { get; }
            public double C { get; }
            public double Nc { get; }
            public double Fl { get; }
            public double FlRoot { get; }
            public double Z { get; }
            public double[] RgbD { get; }
            public double[] InvRgbD { get; }

            private ViewingConditions(
                double n, double aw, double nbb, double ncb,
                double c, double nc, double fl, double flRoot,
                double z, double[] rgbD, double[] invRgbD)
            {
                N = n; Aw = aw; Nbb = nbb; Ncb = ncb;
                C = c; Nc = nc; Fl = fl; FlRoot = flRoot;
                Z = z; RgbD = rgbD; InvRgbD = invRgbD;
            }

            private static ViewingConditions MakeStandard()
            {
                double[] whitePoint = { 95.047, 100.0, 108.883 };
                double adaptingLuminance = 200.0 / Math.PI * whitePoint[1] / 100.0;
                double backgroundLstar = 50.0;
                double surroundFactor = 0.69;
                double n = backgroundLstar / whitePoint[1];
                double nbb = 0.725 * Math.Pow(1.0 / n, 0.2);
                double ncb = nbb;
                double c = 0.69;
                double nc = 1.0;
                double fl = 0.3884 * adaptingLuminance * adaptingLuminance /
                    (adaptingLuminance * adaptingLuminance + 0.111111) +
                    0.04674 * adaptingLuminance / (adaptingLuminance * adaptingLuminance + 0.111111) +
                    0.0;
                if (fl < 0.1) fl = 0.1;
                if (fl > 10.0) fl = 10.0;
                double flRoot = Math.Pow(fl, 0.25);
                double z = 1.48 + Math.Sqrt(n);

                double x = whitePoint[0], yW = whitePoint[1], zW = whitePoint[2];
                double rW = 0.401288 * x + 0.650173 * yW - 0.051461 * zW;
                double gW = -0.250268 * x + 1.204414 * yW + 0.045854 * zW;
                double bW = -0.002079 * x + 0.048952 * yW + 0.953127 * zW;

                double d = surroundFactor *
                    (1.0 - 1.0 / 3.6 * Math.Exp((-adaptingLuminance - 42.0) / 92.0));
                double dFactor = 1.0 - d;

                double[] rgbD = {
                    dFactor * (yW / rW),
                    dFactor * (yW / gW),
                    dFactor * (yW / bW)
                };
                rgbD[0] = Math.Min(rgbD[0], 1.0);
                rgbD[1] = Math.Min(rgbD[1], 1.0);
                rgbD[2] = Math.Min(rgbD[2], 1.0);

                double[] invRgbD = {
                    1.0 / rgbD[0],
                    1.0 / rgbD[1],
                    1.0 / rgbD[2]
                };

                double rAD = fl * Math.Pow(Math.Abs(rW * rgbD[0]), 0.42) * Math.Sign(rW);
                double gAD = fl * Math.Pow(Math.Abs(gW * rgbD[1]), 0.42) * Math.Sign(gW);
                double bAD = fl * Math.Pow(Math.Abs(bW * rgbD[2]), 0.42) * Math.Sign(bW);
                double aw = (2.0 * rAD + gAD + 0.05 * bAD) * nbb;

                return new ViewingConditions(
                    n, aw, nbb, ncb, c, nc, fl, flRoot, z, rgbD, invRgbD);
            }
        }
    }
}

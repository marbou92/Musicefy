using System.Collections.Generic;

namespace Musicefy.Core.Color
{
    public class TonalPalette
    {
        private readonly double _hue;
        private readonly double _chroma;
        private readonly Dictionary<int, int> _cache;

        private TonalPalette(double hue, double chroma)
        {
            _hue = hue;
            _chroma = chroma;
            _cache = new Dictionary<int, int>();
        }

        public static TonalPalette FromHueAndChroma(double hue, double chroma)
        {
            return new TonalPalette(hue, chroma);
        }

        public static TonalPalette FromColor(int argb)
        {
            var hct = Hct.FromInt(argb);
            return new TonalPalette(hct.Hue, hct.Chroma);
        }

        public double Hue => _hue;
        public double Chroma => _chroma;

        public int GetTone(double tone)
        {
            int key = (int)System.Math.Round(tone);
            key = System.Math.Max(0, System.Math.Min(100, key));

            if (!_cache.TryGetValue(key, out int argb))
            {
                argb = Hct.From(_hue, _chroma, tone).ToInt();
                _cache[key] = argb;
            }
            return argb;
        }

        public Hct GetHct(double tone)
        {
            return Hct.FromInt(GetTone(tone));
        }
    }
}

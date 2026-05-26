using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using Microsoft.Extensions.DependencyInjection;
using Musicefy.Core.Interfaces;

namespace Musicefy.Converters
{
    public class IconGlyphConverter : IValueConverter
    {
        private static readonly Dictionary<string, string> BuiltInPaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Subsonic", "M17 7h-4v2h4c1.65 0 3 1.35 3 3s-1.35 3-3 3h-4v2h4c2.76 0 5-2.24 5-5s-2.24-5-5-5zm-6 8H7c-1.65 0-3-1.35-3-3s1.35-3 3-3h4V7H7c-2.76 0-5 2.24-5 5s2.24 5 5 5h4v-2zm-3-4h8v2H8v-2z" },
            { "YouTube", "M8 5v14l11-7z" },
            { "Local", "M20 6h-8l-2-2H4c-1.1 0-1.99.9-1.99 2L2 18c0 1.1.9 2 2 2h16c1.1 0 2-.9 2-2V8c0-1.1-.9-2-2-2zm0 12H4V8h16v10z" },
            { "Extension", "M20.5 11H19V7c0-1.1-.9-2-2-2h-4V3.5C13 2.12 11.88 1 10.5 1S8 2.12 8 3.5V5H4c-1.1 0-2 .9-2 2v3.8h1.5c1.49 0 2.7 1.21 2.7 2.7s-1.21 2.7-2.7 2.7H2V20c0 1.1.9 2 2 2h3.8v-1.5c0-1.49 1.21-2.7 2.7-2.7s2.7 1.21 2.7 2.7V22H17c1.1 0 2-.9 2-2v-4h1.5c1.38 0 2.5-1.12 2.5-2.5S21.88 11 20.5 11z" }
        };

        private static Dictionary<string, IMusicSourceProvider> _providerCache;

        private static Dictionary<string, IMusicSourceProvider> GetProviders()
        {
            if (_providerCache != null) return _providerCache;
            try
            {
                var providers = App.Services.GetServices<IMusicSourceProvider>();
                _providerCache = new Dictionary<string, IMusicSourceProvider>(StringComparer.OrdinalIgnoreCase);
                foreach (var p in providers)
                {
                    if (!_providerCache.ContainsKey(p.SourceType))
                        _providerCache[p.SourceType] = p;
                }
            }
            catch
            {
                _providerCache ??= new Dictionary<string, IMusicSourceProvider>();
            }
            return _providerCache;
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string key)
            {
                if (BuiltInPaths.TryGetValue(key, out var pathData))
                    return Geometry.Parse(pathData);

                // Check extension providers for a registered icon
                var providers = GetProviders();
                if (providers.ContainsKey(key))
                    return Geometry.Parse(BuiltInPaths["Extension"]);
            }

            return Geometry.Parse(BuiltInPaths["Extension"]);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

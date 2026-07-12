using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using Microsoft.Extensions.DependencyInjection;
using Musicefy.Core.Interfaces;
using static Musicefy.Core.SourceTypes;

namespace Musicefy.Converters
{
    /// <summary>
    /// Converts a source type string (e.g. "Local", "YouTube", "Subsonic")
    /// to a Geometry path for rendering as a vector icon.
    /// </summary>
    public class IconGlyphConverter : IValueConverter
    {
        private static readonly Dictionary<string, string> BuiltInPaths =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                // Local — folder icon
                { Local, "M20 6h-8l-2-2H4c-1.1 0-1.99.9-1.99 2L2 18c0 1.1.9 2 2 2h16c1.1 0 2-.9 2-2V8c0-1.1-.9-2-2-2zm0 12H4V8h16v10z" },

                // YouTube — proper YouTube logo (rounded rectangle + play triangle)
                { YouTube, "M23.498 6.186a3.016 3.016 0 0 0-2.122-2.136C19.505 3.545 12 3.545 12 3.545s-7.505 0-9.377.505A3.017 3.017 0 0 0 .502 6.186C0 8.07 0 12 0 12s0 3.93.502 5.814a3.016 3.016 0 0 0 2.122 2.136c1.871.505 9.376.505 9.376.505s7.505 0 9.377-.505a3.015 3.015 0 0 0 2.122-2.136C24 15.93 24 12 24 12s0-3.93-.502-5.814zM9.545 15.568V8.432L15.818 12l-6.273 3.568z" },

                // Subsonic — kept for orphaned sources (will render this icon
                // if a user has an old Subsonic source from a previous install).
                { Subsonic, "M17 7h-4v2h4c1.65 0 3 1.35 3 3s-1.35 3-3 3h-4v2h4c2.76 0 5-2.24 5-5s-2.24-5-5-5zm-6 8H7c-1.65 0-3-1.35-3-3s1.35-3 3-3h4V7H7c-2.76 0-5 2.24-5 5s2.24 5 5 5h4v-2zm-3-4h8v2H8v-2z" },

                // Extension — kept for orphaned sources from the old extension system.
                { Extension, "M20.5 11H19V7c0-1.1-.9-2-2-2h-4V3.5C13 2.12 11.88 1 10.5 1S8 2.12 8 3.5V5H4c-1.1 0-2 .9-2 2v3.8h1.5c1.49 0 2.7 1.21 2.7 2.7s-1.21 2.7-2.7 2.7H2V20c0 1.1.9 2 2 2h3.8v-1.5c0-1.49 1.21-2.7 2.7-2.7s2.7 1.21 2.7 2.7V22H17c1.1 0 2-.9 2-2v-4h1.5c1.38 0 2.5-1.12 2.5-2.5S21.88 11 20.5 11z" }
            };

        /// <summary>
        /// Fallback icon (generic music note) for unknown source types.
        /// </summary>
        private const string FallbackPath =
            "M12 3v10.55c-.59-.34-1.27-.55-2-.55-2.21 0-4 1.79-4 4s1.79 4 4 4 4-1.79 4-4V7h4V3h-6z";

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string key)
            {
                if (BuiltInPaths.TryGetValue(key, out var pathData))
                    return Geometry.Parse(pathData);
            }

            return Geometry.Parse(FallbackPath);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

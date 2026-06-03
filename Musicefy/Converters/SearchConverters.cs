using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Musicefy.Converters
{
    /// <summary>
    /// Converts an integer count to Visibility:
    ///   Non-zero → Visible, Zero → Collapsed.
    /// Used to show/hide UI elements based on collection count.
    /// </summary>
    public class NonZeroToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int count)
                return count > 0 ? Visibility.Visible : Visibility.Collapsed;
            if (value is System.Collections.ICollection col)
                return col.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converts an integer count to Visibility:
    ///   Zero → Visible, Non-zero → Collapsed.
    /// Inverse of NonZeroToVisibilityConverter.
    /// </summary>
    public class ZeroToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int count)
                return count == 0 ? Visibility.Visible : Visibility.Collapsed;
            if (value is System.Collections.ICollection col)
                return col.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converts a SearchSourceMode enum value to a Geometry object
    /// for the source mode toggle icon in the search bar.
    ///   Local → Cloud-off icon, Online → Cloud icon
    ///
    /// FIX: Previously returned a raw string, but Path.Data binding requires
    /// a Geometry object. WPF's binding engine does NOT auto-convert strings
    /// to Geometry through bindings — only the XAML parser does that for
    /// attribute values. This caused the cloud icon in the search bar to
    /// appear blank/broken (the blue-highlighted area in the user's screenshot).
    /// Now returns Geometry.Parse() objects, matching the pattern used by
    /// IconGlyphConverter throughout the rest of the app.
    /// </summary>
    public class SearchModeIconConverter : IValueConverter
    {
        // Cloud icon (online mode) - Material Design "cloud" path
        private const string CloudPath = "M19.35,10.04C18.67,6.59 15.64,4 12,4C9.11,4 6.6,5.64 5.35,8.04C2.34,8.36 0,10.91 0,14A6,6 0 0,0 6,20H19A5,5 0 0,0 24,15C24,12.36 21.95,10.22 19.35,10.04Z";

        // Cloud-off icon (local mode) - Material Design "cloud_off" path
        private const string CloudOffPath = "M19.35,10.04C18.67,6.59 15.64,4 12,4C10.74,4 9.57,4.36 8.55,4.96L10,6.42C10.64,6.15 11.3,6 12,6C14.21,6 16.09,7.44 16.73,9.44L16.73,9.44C16.98,9.39 17.23,9.36 17.5,9.36C19.71,9.36 21.5,11.15 21.5,13.36C21.5,14.34 21.13,15.23 20.52,15.93L21.93,17.34C22.91,16.15 23.5,14.68 23.5,13.36C23.5,10.41 21.72,7.88 19.35,6.84L19.35,10.04ZM3,5.27L5.77,8.04C3.67,8.46 2,10.3 2,12.5C2,15.04 4.04,17.1 6.57,17.1H17.73L19.73,19.1H6.57C2.96,19.1 0,16.14 0,12.5C0,9.65 1.72,7.21 4.16,6.27L3,5.27Z";

        // Cache parsed geometries as static fields to avoid re-parsing on every conversion
        private static readonly Geometry CloudGeometry = Geometry.Parse(CloudPath);
        private static readonly Geometry CloudOffGeometry = Geometry.Parse(CloudOffPath);

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Musicefy.Core.Models.SearchSourceMode mode)
            {
                return mode == Musicefy.Core.Models.SearchSourceMode.Online
                    ? CloudGeometry
                    : CloudOffGeometry;
            }
            return CloudGeometry;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

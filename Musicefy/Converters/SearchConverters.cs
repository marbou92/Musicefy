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
    /// for the source mode toggle icon.
    ///   Local → Cloud-off icon, Online → Cloud icon
    /// Returns Geometry (not string) because Path.Data bindings
    /// require a Geometry object — WPF does not apply its string-to-Geometry
    /// type converter for values arriving through bindings.
    /// </summary>
    public class SearchModeIconConverter : IValueConverter
    {
        // Cloud icon (online mode)
        private const string CloudPath = "M19.35,10.04C18.67,6.59 15.64,4 12,4C9.11,4 6.6,5.64 5.35,8.04C2.34,8.36 0,10.91 0,14A6,6 0 0,0 6,20H19A5,5 0 0,0 24,15C24,12.36 21.95,10.22 19.35,10.04Z";

        // Cloud-off icon (local mode)
        private const string CloudOffPath = "M19.8,22.6L17.2,20H6A6,6 0 0,1 0,14C0,10.91 2.34,8.36 5.35,8.04C5.13,8.65 5,9.31 5,10H7A5,5 0 0,1 12,5C14.36,5 16.4,6.38 17.29,8.44C17.75,8.16 18.29,8 18.86,8C20.6,8 22,9.4 22,11.14C22,11.85 21.73,12.5 21.28,13L23.5,15.18L19.8,22.6Z";

        // Pre-parsed Geometry objects for performance
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

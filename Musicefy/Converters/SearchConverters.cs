using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Musicefy.Converters
{
    /// <summary>
    /// Non-zero → Visible, Zero → Collapsed.
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
    /// Zero → Visible, Non-zero → Collapsed.
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
    /// Converts a SearchSourceMode enum to a Geometry for the source mode icon.
    /// IMPORTANT: Must return Geometry (not string) because Path.Data expects Geometry.
    /// </summary>
    public class SearchModeIconConverter : IValueConverter
    {
        private const string CloudPath = "M19.35,10.04C18.67,6.59 15.64,4 12,4C9.11,4 6.6,5.64 5.35,8.04C2.34,8.36 0,10.91 0,14A6,6 0 0,0 6,20H19A5,5 0 0,0 24,15C24,12.36 21.95,10.22 19.35,10.04Z";
        private const string CloudOffPath = "M19.8,22.6L17.2,20H6A6,6 0 0,1 0,14C0,10.91 2.34,8.36 5.35,8.04C5.13,8.65 5,9.31 5,10H7A5,5 0 0,1 12,5C14.36,5 16.4,6.38 17.29,8.44C17.75,8.16 18.29,8 18.86,8C20.6,8 22,9.4 22,11.14C22,11.85 21.73,12.5 21.28,13L23.5,15.18L19.8,22.6Z";

        private static Geometry _cloudGeometry;
        private static Geometry _cloudOffGeometry;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                if (value is Musicefy.Core.Models.SearchSourceMode mode)
                {
                    if (mode == Musicefy.Core.Models.SearchSourceMode.Online)
                        return _cloudGeometry ?? (_cloudGeometry = Geometry.Parse(CloudPath));
                    else
                        return _cloudOffGeometry ?? (_cloudOffGeometry = Geometry.Parse(CloudOffPath));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SearchModeIconConverter] Error: {ex.Message}");
            }

            return _cloudGeometry ?? (_cloudGeometry = Geometry.Parse(CloudPath));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

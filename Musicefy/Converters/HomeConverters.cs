using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Musicefy.Converters
{
    /// <summary>
    /// Converts true to Collapsed, false to Visible (inverse of BooleanToVisibilityConverter).
    /// Used to hide content when IsLoading is true.
    /// </summary>
    public class InverseBoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b)
                return b ? Visibility.Collapsed : Visibility.Visible;
            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility v)
                return v != Visibility.Visible;
            return false;
        }
    }

    /// <summary>
    /// Converts "Local" source type to Collapsed, non-local to Visible.
    /// Used to show/hide the source type badge on section headers.
    /// </summary>
    public class NonLocalVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string sourceType)
                return sourceType == "Local" ? Visibility.Collapsed : Visibility.Visible;
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converts null or empty string to Collapsed, non-empty to Visible.
    /// </summary>
    public class NullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return Visibility.Collapsed;
            if (value is string s && string.IsNullOrEmpty(s)) return Visibility.Collapsed;
            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converts an integer count to Visible when count >= threshold (parameter),
    /// Collapsed otherwise. Default threshold is 5.
    /// Used for the "See All" button visibility: show when a section has 5+ items.
    /// </summary>
    public class CountGreaterThanOrEqualConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            int count = 0;
            if (value is int i)
                count = i;
            else if (value is System.Collections.ICollection col)
                count = col.Count;

            int threshold = 5;
            if (parameter != null && int.TryParse(parameter.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int t))
                threshold = t;

            return count >= threshold ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converts an album Year integer to a display suffix string.
    /// Returns " · YYYY" when Year > 0, or empty string when Year is 0 or default.
    /// Used in the AlbumCardTemplate subtitle: "Artist · 2024"
    /// </summary>
    public class AlbumYearSuffixConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int year && year > 0)
                return $" \u00B7 {year}";
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

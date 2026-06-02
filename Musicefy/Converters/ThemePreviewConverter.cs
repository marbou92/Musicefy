using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using Musicefy.Core.Theme;

namespace Musicefy.Converters
{
    /// <summary>
    /// Converts a ThemeMode or AppTheme value to a preview brush for display.
    /// Updated for the Aniyomi-style separate AppTheme + ThemeMode model.
    /// </summary>
    public class ThemePreviewConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ThemeMode mode)
            {
                return mode switch
                {
                    ThemeMode.Light  => new SolidColorBrush(Color.FromRgb(245, 245, 245)),
                    ThemeMode.Dark   => new SolidColorBrush(Color.FromRgb(30, 30, 30)),
                    ThemeMode.Amoled => new SolidColorBrush(Colors.Black),
                    _                => new SolidColorBrush(Color.FromRgb(30, 30, 30)), // System defaults to dark preview
                };
            }

            if (value is AppTheme theme)
            {
                var (primary, surface, secondary, tertiary) = AppThemeColorSchemes.GetPreviewColors(theme);
                var brush = new LinearGradientBrush(primary, surface, new Point(0, 0), new Point(1, 1));
                brush.Freeze();
                return brush;
            }

            // Fallback: try to parse old-style string
            if (value is string themeSetting)
            {
                string modeName = themeSetting.Split('|')[0].Trim();
                Brush brush = modeName switch
                {
                    "Light" => new SolidColorBrush(Color.FromRgb(245, 245, 245)),
                    _       => new SolidColorBrush(Color.FromRgb(30, 30, 30)),
                };
                brush.Freeze();
                return brush;
            }

            return Brushes.Transparent;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

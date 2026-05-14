using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Musicefy.Converters
{
    public class ThemePreviewConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string themeName)
            {
                switch (themeName)
                {
                    case "Dark":
                        return new SolidColorBrush(Color.FromRgb(30, 30, 30));
                    case "Light":
                        return new SolidColorBrush(Color.FromRgb(245, 245, 245));
                    case "DarkLavender":
                        return new LinearGradientBrush(
                            Color.FromRgb(181, 126, 220),
                            Color.FromRgb(154, 111, 208),
                            new Point(0, 0),
                            new Point(1, 1));
                    case "WhiteLavender":
                        return new LinearGradientBrush(
                            Color.FromRgb(181, 126, 220),
                            Color.FromRgb(200, 157, 242),
                            new Point(0, 0),
                            new Point(1, 1));
                    default:
                        // Fallback for custom or unknown themes
                        return new SolidColorBrush(Colors.DimGray);
                }
            }
            return new SolidColorBrush(Colors.Transparent);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

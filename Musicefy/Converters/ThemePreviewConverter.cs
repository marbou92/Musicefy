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
            if (value is string themeSetting)
            {
                // Extracts "Dark" or "DarkLavender" from composite settings like "Dark|Default"
                string themeName = themeSetting.Split('|')[0].Trim();

                Brush brush;
                switch (themeName)
                {
                    case "Dark":
                        brush = new SolidColorBrush(Color.FromRgb(30, 30, 30));
                        break;
                    case "Light":
                        brush = new SolidColorBrush(Color.FromRgb(245, 245, 245));
                        break;
                    case "DarkLavender":
                        brush = new LinearGradientBrush(
                            Color.FromRgb(181, 126, 220),
                            Color.FromRgb(154, 111, 208),
                            new Point(0, 0),
                            new Point(1, 1));
                        break;
                    case "WhiteLavender":
                        brush = new LinearGradientBrush(
                            Color.FromRgb(181, 126, 220),
                            Color.FromRgb(200, 157, 242),
                            new Point(0, 0),
                            new Point(1, 1));
                        break;
                    default:
                        brush = new SolidColorBrush(Colors.DimGray);
                        break;
                }

                brush.Freeze(); // Performance optimization for WPF brushes
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

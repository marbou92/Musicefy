using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace Musicefy.Converters
{
    public class PathToImageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string path = value as string;
            if (string.IsNullOrEmpty(path))
            {
                return new BitmapImage(new Uri("pack://application:,,,/Assets/default_cover.png"));
            }

            try
            {
                return new BitmapImage(new Uri(path, UriKind.RelativeOrAbsolute));
            }
            catch
            {
                return new BitmapImage(new Uri("pack://application:,,,/Assets/default_cover.png"));
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

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
            return CreateBitmap(string.IsNullOrEmpty(path) ? "pack://application:,,,/Assets/default_cover.png" : path);
        }

        private BitmapImage CreateBitmap(string uriString)
        {
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(uriString, UriKind.RelativeOrAbsolute);
                // PERFORMANCE FIX: CacheOnLoad prevents file locking and drops memory allocation spikes on win7
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze(); // Freeze to allow cross-thread UI access safely
                return bitmap;
            }
            catch
            {
                if (uriString != "pack://application:,,,/Assets/default_cover.png")
                {
                    return CreateBitmap("pack://application:,,,/Assets/default_cover.png");
                }
                return null;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

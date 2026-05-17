using System;
using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace Musicefy.Converters
{
    public class PathToImageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string path = value as string;

            // FIXED: Safely verify if a valid local disk path string was passed down
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                return CreateBitmap(path, false);
            }

            // Fallback default image resource path mapping rule
            return CreateBitmap("pack://application:,,,/Assets/default_cover.png", true);
        }

        private BitmapImage CreateBitmap(string uriString, bool isResource)
        {
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                
                if (isResource)
                {
                    bitmap.UriSource = new Uri(uriString, UriKind.RelativeOrAbsolute);
                }
                else
                {
                    // FIXED: Converts raw hard drive cache paths straight to local file URI schemas cleanly
                    bitmap.UriSource = new Uri(Path.GetFullPath(uriString), UriKind.Absolute);
                }

                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze(); // Safely allows cross-thread UI visibility bounds access
                return bitmap;
            }
            catch
            {
                if (uriString != "pack://application:,,,/Assets/default_cover.png")
                {
                    return CreateBitmap("pack://application:,,,/Assets/default_cover.png", true);
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

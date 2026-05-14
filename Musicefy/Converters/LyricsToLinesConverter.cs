using System;
using System.Globalization;
using System.Windows.Data;

namespace Musicefy.Converters
{
    public class LyricsToLinesConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string lyrics = value as string;
            if (string.IsNullOrEmpty(lyrics))
                return null;

            return lyrics.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

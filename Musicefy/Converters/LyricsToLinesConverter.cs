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
                return new string[] { "", "🎵 Instrumental 🎵", "" };

            // Splits on both carriage returns and raw newlines safely
            return lyrics.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

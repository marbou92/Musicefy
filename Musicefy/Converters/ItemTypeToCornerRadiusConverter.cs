using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Musicefy.Core.Models;

namespace Musicefy.Converters
{
    /// <summary>
    /// Returns a CornerRadius based on the runtime type of the data item.
    /// Artists get a circular corner radius (20 for 40x40 size),
    /// songs and albums get a small rounded corner (4).
    /// This matches Echo Music's convention where artist avatars are round
    /// and album/track thumbnails are rounded squares.
    /// </summary>
    public class ItemTypeToCornerRadiusConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ArtistInfo)
                return new CornerRadius(20); // Circle for 40×40 artist avatars

            // MusicFile, AlbumInfo, PlaylistInfo, and unknown types → rounded square
            return new CornerRadius(4);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

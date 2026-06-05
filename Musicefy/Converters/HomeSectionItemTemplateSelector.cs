using System.Windows;
using System.Windows.Controls;
using Musicefy.Core.Models;

namespace Musicefy.Converters
{
    /// <summary>
    /// Selects a DataTemplate based on the runtime type of the data item
    /// in a HomeSection. This enables each content type to get its own
    /// visual treatment, matching Echo Music's convention:
    ///   - MusicFile  → square card with cover, title, artist, play overlay
    ///   - AlbumInfo  → square card with cover, album name, artist + year
    ///   - ArtistInfo → circle avatar with name below
    ///
    /// Usage in XAML:
    ///   &lt;ItemsControl.ItemTemplateSelector&gt;
    ///     &lt;local:HomeSectionItemTemplateSelector
    ///       TrackTemplate="{StaticResource TrackCardTemplate}"
    ///       AlbumTemplate="{StaticResource AlbumCardTemplate}"
    ///       ArtistTemplate="{StaticResource ArtistCardTemplate}"/&gt;
    ///   &lt;/ItemsControl.ItemTemplateSelector&gt;
    /// </summary>
    public class HomeSectionItemTemplateSelector : DataTemplateSelector
    {
        /// <summary>Template for MusicFile items (track cards).</summary>
        public DataTemplate TrackTemplate { get; set; }

        /// <summary>Template for AlbumInfo items (album cards).</summary>
        public DataTemplate AlbumTemplate { get; set; }

        /// <summary>Template for ArtistInfo items (artist circle cards).</summary>
        public DataTemplate ArtistTemplate { get; set; }

        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            if (item is MusicFile)
                return TrackTemplate;
            if (item is AlbumInfo)
                return AlbumTemplate;
            if (item is ArtistInfo)
                return ArtistTemplate;

            // Fallback: treat unknown types as tracks
            return TrackTemplate;
        }
    }
}

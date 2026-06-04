using System.Collections.Generic;

namespace Musicefy.Core.Models
{
    public class ArtistInfo
    {
        /// <summary>Stable unique identifier (for local artists: a generated ID; for YouTube: the channel browse ID).</summary>
        public string Id { get; set; }

        public string Name { get; set; }
        public string CoverPath { get; set; }
        public string SourceType { get; set; }

        /// <summary>YouTube channel browse ID (UC...) — enables browsing the artist page on YouTube Music.</summary>
        public string YouTubeChannelId { get; set; }

        public List<MusicFile> Tracks { get; set; } = new List<MusicFile>();
        public List<AlbumInfo> Albums { get; set; } = new List<AlbumInfo>();
    }
}

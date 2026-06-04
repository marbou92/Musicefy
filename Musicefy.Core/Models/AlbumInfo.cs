using System.Collections.Generic;

namespace Musicefy.Core.Models
{
    public class AlbumInfo
    {
        /// <summary>Stable unique identifier (for local albums: a generated ID; for YouTube: the album browse ID).</summary>
        public string Id { get; set; }

        public string Name { get; set; }
        public string Artist { get; set; }
        public int Year { get; set; }
        public string CoverPath { get; set; }
        public string SourceType { get; set; }

        /// <summary>YouTube album browse ID (MPRE...) — enables browsing the album page on YouTube Music.</summary>
        public string YouTubeAlbumId { get; set; }

        public List<MusicFile> Tracks { get; set; } = new List<MusicFile>();
    }
}

using System.Collections.Generic;

namespace Musicefy.Core.Models
{
    public class ArtistInfo
    {
        public string Name { get; set; }
        public string CoverPath { get; set; }
        public string SourceType { get; set; }
        public List<MusicFile> Tracks { get; set; } = new List<MusicFile>();
        public List<AlbumInfo> Albums { get; set; } = new List<AlbumInfo>();
    }
}

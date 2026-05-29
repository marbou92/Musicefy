using System.Collections.Generic;

namespace Musicefy.Core.Models
{
    public class AlbumInfo
    {
        public string Name { get; set; }
        public string Artist { get; set; }
        public int Year { get; set; }
        public string CoverPath { get; set; }
        public string SourceType { get; set; }
        public List<MusicFile> Tracks { get; set; } = new List<MusicFile>();
    }
}

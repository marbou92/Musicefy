using System;

namespace Musicefy.Core.Models
{
    /// <summary>
    /// Represents a music file with metadata
    /// </summary>
    public class MusicFile
    {
        public string FilePath { get; set; }
        public string Title { get; set; }
        public string Artist { get; set; }
        public string Album { get; set; }
        public string Genre { get; set; }
        public TimeSpan Duration { get; set; }
        public int Year { get; set; }
        public int TrackNumber { get; set; }
        public byte[] AlbumArt { get; set; }

        public override string ToString()
        {
            return $"{Title} - {Artist}";
        }
    }
}

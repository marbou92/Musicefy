namespace Musicefy.Core.Models
{
    public class MusicFile
    {
        public string FilePath { get; set; }      // source:songId format
        public string Title { get; set; }
        public string Artist { get; set; }
        public string Album { get; set; }
        public int Year { get; set; }
        public string Genre { get; set; }
        public System.TimeSpan Duration { get; set; }
        public int TrackNumber { get; set; }
        public string SourceUri { get; set; }

        public MusicFile() { }

        public MusicFile(
            string title,
            string artist,
            string album = "",
            int year = 0,
            string sourceUri = null,
            string filePath = null,
            string genre = null,
            System.TimeSpan duration = default,
            int trackNumber = 0)
        {
            Title = title;
            Artist = artist;
            Album = album;
            Year = year;
            SourceUri = sourceUri;
            FilePath = filePath;
            Genre = genre;
            Duration = duration;
            TrackNumber = trackNumber;
        }

        public override string ToString()
        {
            return string.IsNullOrEmpty(Artist) ? Title : $"{Title} - {Artist}";
        }
    }
}

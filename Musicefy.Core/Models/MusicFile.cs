namespace Musicefy.Core.Models
{
    public class MusicFile
    {
        public string Title { get; set; }
        public string Artist { get; set; }
        public string Album { get; set; }
        public int Year { get; set; }
        public string SourceUri { get; set; }

        public MusicFile() { }

        public MusicFile(string title, string artist, string album = "", int year = 0, string sourceUri = null)
        {
            Title = title;
            Artist = artist;
            Album = album;
            Year = year;
            SourceUri = sourceUri;
        }

        public override string ToString()
        {
            return string.IsNullOrEmpty(Artist) ? Title : $"{Title} - {Artist}";
        }
    }
}

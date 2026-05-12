using System;

namespace Musicefy.Core.Models
{
    public class MusicFile
    {
        public string Id { get; set; }
        public string FilePath { get; set; }

        // Alias for compatibility with existing code
        public string Path
        {
            get => FilePath;
            set => FilePath = value;
        }

        public string Title { get; set; }
        public string Artist { get; set; }
        public string Album { get; set; }
        public int Year { get; set; }
        public string Genre { get; set; }
        public TimeSpan Duration { get; set; }
        public int TrackNumber { get; set; }

        public string SourceUri { get; set; }
        public string SourceType { get; set; }

        public MusicFile()
        {
            Id = Guid.NewGuid().ToString();
        }

        public MusicFile(
            string title,
            string artist,
            string album = "",
            int year = 0,
            string sourceUri = null,
            string filePath = null,
            string genre = null,
            TimeSpan duration = default,
            int trackNumber = 0,
            string sourceType = "Local")
        {
            Id = Guid.NewGuid().ToString();
            Title = title;
            Artist = artist;
            Album = album;
            Year = year;
            SourceUri = sourceUri;
            FilePath = filePath ?? sourceUri;
            Genre = genre;
            Duration = duration;
            TrackNumber = trackNumber;
            SourceType = sourceType;
        }

        public override string ToString()
        {
            return string.IsNullOrEmpty(Artist) ? Title : $"{Title} - {Artist}";
        }
    }
}

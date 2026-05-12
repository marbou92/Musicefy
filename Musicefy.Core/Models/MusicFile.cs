using System;

namespace Musicefy.Core.Models
{
    public class MusicFile
    {
        // Unique identifier for each track
        public string Id { get; set; }

        // Local file path (if applicable)
        public string FilePath { get; set; }

        // Display metadata
        public string Title { get; set; }
        public string Artist { get; set; }
        public string Album { get; set; }
        public int Year { get; set; }
        public string Genre { get; set; }
        public TimeSpan Duration { get; set; }
        public int TrackNumber { get; set; }

        // Playback source
        public string SourceUri { get; set; }     // URI for playback (local or streaming)
        public string SourceType { get; set; }    // "Local", "Subsonic", etc.

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

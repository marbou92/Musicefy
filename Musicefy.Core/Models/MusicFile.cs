using System;

namespace Musicefy.Core.Models
{
    public class MusicFile
    {
        // Unique identifier
        public string Id { get; set; }

        // File path on disk (local) or remote URI
        public string FilePath { get; set; }

        // Core metadata
        public string Title { get; set; }
        public string Artist { get; set; }
        public string Album { get; set; }
        public int Year { get; set; }
        public string Genre { get; set; }
        public TimeSpan Duration { get; set; }
        public int TrackNumber { get; set; }

        // Extended metadata
        public int Bitrate { get; set; }            // kbps
        public long FileSize { get; set; }          // bytes
        public string Lyrics { get; set; }          // raw lyrics text
        public string CoverPath { get; set; }       // album art image path

        // Source info
        public string SourceUri { get; set; }       // streaming/local source
        public string SourceType { get; set; }      // e.g. Local, Subsonic, Spotify

        // User interaction
        public int PlayCount { get; set; }
        public DateTime LastPlayed { get; set; }
        public bool IsFavourite { get; set; }
        public bool IsDownloaded { get; set; }

        // Constructors
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
            string sourceType = "Local",
            int bitrate = 0,
            long fileSize = 0,
            string lyrics = null,
            string coverPath = null)
        {
            Id = Guid.NewGuid().ToString();
            Title = title;
            Artist = artist;
            Album = album;
            Year = year;
            SourceUri = sourceUri;
            FilePath = filePath ?? sourceUri ?? string.Empty;
            Genre = genre;
            Duration = duration;
            TrackNumber = trackNumber;
            SourceType = sourceType;
            Bitrate = bitrate;
            FileSize = fileSize;
            Lyrics = lyrics;
            CoverPath = coverPath;
            PlayCount = 0;
            LastPlayed = DateTime.MinValue;
            IsFavourite = false;
            IsDownloaded = false;
        }

        // Methods
        public void MarkPlayed()
        {
            PlayCount++;
            LastPlayed = DateTime.Now;
        }

        public void ToggleFavourite() => IsFavourite = !IsFavourite;

        public override bool Equals(object obj)
        {
            return obj is MusicFile other &&
                   string.Equals(FilePath, other.FilePath, StringComparison.OrdinalIgnoreCase);
        }

        public override int GetHashCode()
        {
            return FilePath != null ? StringComparer.OrdinalIgnoreCase.GetHashCode(FilePath) : 0;
        }

        public override string ToString()
        {
            return string.IsNullOrEmpty(Artist) ? Title : $"{Title} - {Artist}";
        }
    }
}

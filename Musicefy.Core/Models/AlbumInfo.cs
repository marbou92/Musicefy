using System;
using System.Collections.Generic;

namespace Musicefy.Core.Models
{
    public class AlbumInfo
    {
        /// <summary>Stable unique identifier (for local albums: a generated ID; for YouTube: the album browse ID).</summary>
        public string Id { get; set; }

        public string Name { get; set; }

        /// <summary>Artist name (display string). For navigation, use ArtistId when available.</summary>
        public string Artist { get; set; }

        /// <summary>
        /// Stable artist ID (YouTube channel ID or local artist ID).
        /// Enables reliable album → artist navigation without name-based disambiguation.
        /// </summary>
        public string ArtistId { get; set; }

        public int Year { get; set; }
        public string CoverPath { get; set; }
        public string SourceType { get; set; }

        /// <summary>YouTube album browse ID (MPRE...) — enables browsing the album page on YouTube Music.</summary>
        public string YouTubeAlbumId { get; set; }

        /// <summary>Album description (from YouTube Music or local metadata).</summary>
        public string Description { get; set; }

        /// <summary>Genre of the album.</summary>
        public string Genre { get; set; }

        /// <summary>Whether the user has saved/favourited this album.</summary>
        public bool IsSaved { get; set; }

        /// <summary>When this album was last browsed on YouTube Music (for cache invalidation).</summary>
        public DateTime? LastBrowsedAt { get; set; }

        /// <summary>Total number of tracks in the album (available without loading all tracks).</summary>
        public int TrackCount { get; set; }

        public List<MusicFile> Tracks { get; set; } = new List<MusicFile>();
    }
}

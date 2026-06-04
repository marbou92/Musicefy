using System;
using System.Collections.Generic;

namespace Musicefy.Core.Models
{
    /// <summary>
    /// First-class playlist entity with stable ID, track membership, and metadata.
    /// Phase 5: Playlists & Collection Management.
    /// Inspired by Echo Music's playlist model which treats playlists as
    /// first-class citizens with full CRUD, reorder, and YouTube import support.
    /// </summary>
    public class PlaylistInfo
    {
        /// <summary>Stable unique identifier (GUID).</summary>
        public string Id { get; set; }

        /// <summary>User-visible playlist name.</summary>
        public string Name { get; set; }

        /// <summary>When this playlist was created (ISO 8601).</summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>When this playlist was last modified (track added/removed/reordered).</summary>
        public DateTime? LastModifiedAt { get; set; }

        /// <summary>Optional description for the playlist.</summary>
        public string Description { get; set; }

        /// <summary>
        /// Cover art path for the playlist.
        /// If not explicitly set, the first track's cover is used as a collage fallback.
        /// </summary>
        public string CoverPath { get; set; }

        /// <summary>
        /// YouTube playlist ID (VL...). If set, this playlist was imported
        /// from or is linked to a YouTube Music playlist.
        /// </summary>
        public string YouTubePlaylistId { get; set; }

        /// <summary>Source type of this playlist (YouTube, Local, etc.).</summary>
        public string SourceType { get; set; }

        /// <summary>Number of tracks in the playlist (available without loading all tracks).</summary>
        public int TrackCount { get; set; }

        /// <summary>Total duration of all tracks in the playlist.</summary>
        public TimeSpan TotalDuration { get; set; }

        /// <summary>Ordered list of tracks in this playlist.</summary>
        public List<MusicFile> Tracks { get; set; } = new List<MusicFile>();
    }
}

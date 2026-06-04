using System;
using System.Collections.Generic;

namespace Musicefy.Core.Models
{
    public class ArtistInfo
    {
        /// <summary>Stable unique identifier (for local artists: a generated ID; for YouTube: the channel browse ID).</summary>
        public string Id { get; set; }

        public string Name { get; set; }
        public string CoverPath { get; set; }
        public string SourceType { get; set; }

        /// <summary>YouTube channel browse ID (UC...) — enables browsing the artist page on YouTube Music.</summary>
        public string YouTubeChannelId { get; set; }

        /// <summary>Artist biography / description (from YouTube Music or local metadata).</summary>
        public string Description { get; set; }

        /// <summary>Number of subscribers (YouTube Music artists). Zero or null if unavailable.</summary>
        public long? SubscriberCount { get; set; }

        /// <summary>Whether the user has followed/subscribed to this artist.</summary>
        public bool IsFollowed { get; set; }

        /// <summary>When this artist was last browsed on YouTube Music (for cache invalidation).</summary>
        public DateTime? LastBrowsedAt { get; set; }

        /// <summary>
        /// Top tracks returned by YouTube browse — a curated subset separate from all tracks.
        /// Inspired by Echo Music's artist page which distinguishes top songs from all releases.
        /// </summary>
        public List<MusicFile> TopTracks { get; set; } = new List<MusicFile>();

        /// <summary>All known tracks by this artist (from local library + YouTube browse).</summary>
        public List<MusicFile> Tracks { get; set; } = new List<MusicFile>();

        /// <summary>All known albums by this artist.</summary>
        public List<AlbumInfo> Albums { get; set; } = new List<AlbumInfo>();
    }
}

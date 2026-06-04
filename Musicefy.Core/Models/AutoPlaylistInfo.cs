using System.Collections.Generic;

namespace Musicefy.Core.Models
{
    /// <summary>
    /// Phase 6: Virtual auto-playlist that is dynamically resolved from track properties.
    /// Inspired by Echo Music's AutoPlaylist system (Liked, Downloaded, Top, Recently Played, etc.).
    /// Unlike regular playlists, auto-playlists are not stored in the database — they are
    /// computed on-the-fly from track metadata and play events.
    /// </summary>
    public enum AutoPlaylistType
    {
        Liked,           // IsFavourite = 1
        MostPlayed,      // PlayCount DESC within time window
        RecentlyPlayed,  // LastPlayed DESC
        RecentlyAdded,   // DateAdded DESC
        Forgotten,       // IsFavourite = 1 but not played recently
        Downloaded       // IsDownloaded = 1 or file exists in downloads folder
    }

    public class AutoPlaylistInfo
    {
        public AutoPlaylistType Type { get; set; }
        public string Name { get; set; }
        public string IconData { get; set; }
        public string Subtitle { get; set; }
        public int TrackCount { get; set; }
    }
}

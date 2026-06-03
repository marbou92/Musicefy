using System;
using Musicefy.Core.Models;

namespace Musicefy.Services
{
    /// <summary>
    /// Manages navigation between pages in the main window.
    /// Stub implementation for Phase 1 — full implementation in Phase 2.
    /// </summary>
    public class NavigationService
    {
        public event Action<string> NavigationRequested;

        public void NavigateToHome() => NavigationRequested?.Invoke("Home");
        public void NavigateToSearch() => NavigationRequested?.Invoke("Search");
        public void NavigateToLibrary() => NavigationRequested?.Invoke("Library");

        public void NavigateToArtist(ArtistInfo artist)
        {
            NavigationRequested?.Invoke($"Artist:{artist?.Name}");
        }

        public void NavigateToAlbum(AlbumInfo album)
        {
            NavigationRequested?.Invoke($"Album:{album?.Name}");
        }
    }
}

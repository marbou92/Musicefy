using System;
using System.Windows.Controls;
using Musicefy.Core.Models;
using Musicefy.Views;

namespace Musicefy.Services
{
    /// <summary>
    /// Manages navigation between pages in the main window.
    /// Uses compile-time typeof() instead of Type.GetType(string) because
    /// the latter can silently return null in .NET Framework 4.7.2 due to
    /// assembly loading context issues — which caused Settings to show empty.
    /// </summary>
    public class NavigationService
    {
        private readonly IServiceProvider _serviceProvider;

        public event Action<string> NavigationRequested;

        /// <summary>
        /// Raised when navigation to an artist page is requested.
        /// Carries the full <see cref="ArtistInfo"/> object so that
        /// the target ViewModel can use YouTube browse IDs.
        /// </summary>
        public event Action<ArtistInfo> ArtistNavigationRequested;

        /// <summary>
        /// Raised when navigation to an album page is requested.
        /// Carries the full <see cref="AlbumInfo"/> object so that
        /// the target ViewModel can use YouTube browse IDs.
        /// </summary>
        public event Action<AlbumInfo> AlbumNavigationRequested;

        public NavigationService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }

        /// <summary>
        /// Resolves a page by zero-based index to a UserControl instance via DI.
        /// 0=Home, 1=Search, 2=Library, 3=Settings
        /// </summary>
        public UserControl GetPage(int pageIndex)
        {
            Type pageType = pageIndex switch
            {
                0 => typeof(HomeControl),
                1 => typeof(SearchControl),
                2 => typeof(LibraryControl),
                3 => typeof(SettingsPage),
                _ => null
            };

            if (pageType == null)
                return null;

            try
            {
                return _serviceProvider.GetService(pageType) as UserControl;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[NavigationService] Failed to create {pageType.Name}: {ex}");
                return null;
            }
        }

        /// <summary>
        /// Resolves a named page to a UserControl instance via DI.
        /// </summary>
        public UserControl GetPage(string pageName)
        {
            if (string.IsNullOrEmpty(pageName))
                throw new ArgumentNullException(nameof(pageName));

            Type pageType = pageName switch
            {
                "Home"     => typeof(HomeControl),
                "Search"   => typeof(SearchControl),
                "Library"  => typeof(LibraryControl),
                "Settings" => typeof(SettingsPage),
                _ => null
            };

            if (pageType == null)
                return null;

            try
            {
                return _serviceProvider.GetService(pageType) as UserControl;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[NavigationService] Failed to create {pageType.Name}: {ex}");
                return null;
            }
        }

        public void NavigateToHome() => NavigationRequested?.Invoke("Home");
        public void NavigateToSearch() => NavigationRequested?.Invoke("Search");
        public void NavigateToLibrary() => NavigationRequested?.Invoke("Library");

        public void NavigateToArtist(ArtistInfo artist)
        {
            // Raise the typed event first — MainWindow uses this to pass the
            // full ArtistInfo (with YouTubeChannelId) to ArtistViewModel.
            ArtistNavigationRequested?.Invoke(artist);

            // Legacy string-based event kept for backward compatibility.
            NavigationRequested?.Invoke($"Artist:{artist?.Name}");
        }

        public void NavigateToAlbum(AlbumInfo album)
        {
            // Raise the typed event first — MainWindow uses this to pass the
            // full AlbumInfo (with YouTubeAlbumId) to AlbumViewModel.
            AlbumNavigationRequested?.Invoke(album);

            // Legacy string-based event kept for backward compatibility.
            NavigationRequested?.Invoke($"Album:{album?.Name}");
        }
    }
}

using System;
using System.Windows.Controls;
using Musicefy.Core.Models;
using Musicefy.Views;

namespace Musicefy.Services
{
    /// <summary>
    /// Manages navigation between pages in the main window.
    /// Provides both page resolution and navigation event routing.
    /// </summary>
    public class NavigationService
    {
        private readonly IServiceProvider _serviceProvider;

        public event Action<string> NavigationRequested;

        public NavigationService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }

        /// <summary>
        /// Resolves a page by zero-based index (0=Home, 1=Search, 2=Library, 3=Settings)
        /// to a UserControl instance via DI. Used by MainWindowViewModel with
        /// SelectedIndex-style navigation.
        /// Returns null if the index is out of range or the service is not registered.
        /// </summary>
        public UserControl GetPage(int pageIndex)
        {
            if (pageIndex < 0 || pageIndex > 3)
                return null;

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

            var service = _serviceProvider.GetService(pageType);
            return service as UserControl;
        }

        /// <summary>
        /// Resolves a named page to a UserControl instance via DI.
        /// Supported names: "Home", "Search", "Library", "Settings".
        /// Returns null if the page name is not recognized or the service is not registered.
        /// </summary>
        public UserControl GetPage(string pageName)
        {
            if (string.IsNullOrEmpty(pageName))
                throw new ArgumentNullException(nameof(pageName));

            // Use compile-time typeof() instead of fragile Type.GetType(string)
            // — Type.GetType can fail at runtime due to assembly loading context issues
            // in .NET Framework 4.7.2, which caused Settings navigation to return null.
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

            var service = _serviceProvider.GetService(pageType);
            return service as UserControl;
        }

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

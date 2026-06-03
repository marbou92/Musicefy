using System;
using System.Windows.Controls;
using Musicefy.Core.Models;

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

        // Index-to-page mapping matching the nav bar order in MainWindow
        private static readonly string[] PageNames = { "Home", "Search", "Library" };

        /// <summary>
        /// Resolves a page by zero-based index (0=Home, 1=Search, 2=Library)
        /// to a UserControl instance via DI. Used by MainWindowViewModel with
        /// SelectedIndex-style navigation.
        /// Returns null if the index is out of range or the service is not registered.
        /// </summary>
        public UserControl GetPage(int pageIndex)
        {
            if (pageIndex < 0 || pageIndex >= PageNames.Length)
                return null;

            return GetPage(PageNames[pageIndex]);
        }

        /// <summary>
        /// Resolves a named page to a UserControl instance via DI.
        /// Supported names: "Home", "Search", "Library", and any other registered key.
        /// Returns null if the page name is not recognized or the service is not registered.
        /// </summary>
        public UserControl GetPage(string pageName)
        {
            if (string.IsNullOrEmpty(pageName))
                throw new ArgumentNullException(nameof(pageName));

            // Resolve page by mapping name to the fully-qualified type, then
            // asking the DI container for it.  This avoids compile-time coupling
            // to View types that may live in the Musicefy assembly only.
            var pageType = System.Type.GetType($"Musicefy.Views.{pageName}Control, Musicefy");
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

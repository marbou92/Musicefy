using System;
using System.Windows.Controls;
using Musicefy.Views;

namespace Musicefy.Services
{
    public class NavigationService
    {
        private readonly IServiceProvider _serviceProvider;

        private UserControl _cachedHomePage;
        private UserControl _cachedSearchPage;
        private UserControl _cachedLibraryPage;
        private SettingsPage _cachedSettingsPage;

        public NavigationService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }

        public UserControl GetPage(int pageIndex)
        {
            switch (pageIndex)
            {
                case 0: return _cachedHomePage ??= (UserControl)_serviceProvider.GetService(typeof(HomeControl));
                case 1: return _cachedSearchPage ??= (UserControl)_serviceProvider.GetService(typeof(SearchControl));
                case 2: return _cachedLibraryPage ??= (UserControl)_serviceProvider.GetService(typeof(LibraryControl));
                case 3: return _cachedSettingsPage ??= new SettingsPage();
                default: return _cachedHomePage ??= (UserControl)_serviceProvider.GetService(typeof(HomeControl));
            }
        }
    }
}

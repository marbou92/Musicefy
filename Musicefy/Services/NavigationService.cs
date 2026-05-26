using System;
using System.Windows.Controls;
using Musicefy.ViewModels;
using Musicefy.Views;

namespace Musicefy.Services
{
public class NavigationService
{
    private readonly PlaybackService _playback;
    private readonly MainViewModel _homeViewModel;
    private readonly SearchViewModel _searchViewModel;
    private readonly LibraryViewModel _libraryViewModel;

    private UserControl _cachedSearchPage;
    private UserControl _cachedLibraryPage;

    public NavigationService(
        PlaybackService playback,
        MainViewModel homeViewModel,
        SearchViewModel searchViewModel,
        LibraryViewModel libraryViewModel)
    {
        _playback = playback ?? throw new ArgumentNullException(nameof(playback));
        _homeViewModel = homeViewModel ?? throw new ArgumentNullException(nameof(homeViewModel));
        _searchViewModel = searchViewModel ?? throw new ArgumentNullException(nameof(searchViewModel));
        _libraryViewModel = libraryViewModel ?? throw new ArgumentNullException(nameof(libraryViewModel));
    }

    public UserControl GetPage(int pageIndex)
    {
        switch (pageIndex)
        {
            case 0: return new HomeControl(_playback, _homeViewModel);
            case 1: return _cachedSearchPage ??= new SearchControl { DataContext = _searchViewModel };
            case 2: return _cachedLibraryPage ??= new LibraryControl { DataContext = _libraryViewModel };
            default: return new HomeControl(_playback, _homeViewModel);
        }
    }
}
}

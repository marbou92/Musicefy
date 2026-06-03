using System.Windows.Controls;
using Musicefy.Core.Models;
using Musicefy.ViewModels;

namespace Musicefy.Views
{
    /// <summary>
    /// Interaction logic for SearchControl.xaml
    /// Implements Echo Music's search screen with state-driven UI
    /// (Idle, Suggestions, Searching, Results).
    /// </summary>
    public partial class SearchControl : UserControl
    {
        public SearchControl()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Handles filter tab clicks since WPF RadioButton doesn't natively
        /// bind to enum values. Converts the Tag string to SearchResultFilter
        /// and sets it on the ViewModel.
        /// </summary>
        private void FilterTab_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.RadioButton radio &&
                radio.Tag is string tag &&
                DataContext is SearchViewModel vm)
            {
                SearchResultFilter filter;
                switch (tag)
                {
                    case "Songs":
                        filter = SearchResultFilter.Songs;
                        break;
                    case "Albums":
                        filter = SearchResultFilter.Albums;
                        break;
                    case "Artists":
                        filter = SearchResultFilter.Artists;
                        break;
                    case "Playlists":
                        filter = SearchResultFilter.Playlists;
                        break;
                    default:
                        filter = SearchResultFilter.All;
                        break;
                }
                vm.SelectedFilter = filter;
            }
        }

        /// <summary>
        /// Handles click on a search result item. Determines the type
        /// and invokes the appropriate ViewModel command.
        /// </summary>
        private void ResultItem_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is System.Windows.FrameworkElement fe && fe.DataContext != null && DataContext is SearchViewModel vm)
            {
                var item = fe.DataContext;

                if (item is MusicFile track)
                {
                    vm.PlayTrackCommand.Execute(track);
                }
                else if (item is ArtistInfo artist)
                {
                    vm.NavigateToArtistCommand.Execute(artist);
                }
                else if (item is AlbumInfo album)
                {
                    vm.NavigateToAlbumCommand.Execute(album);
                }
            }
        }
    }
}

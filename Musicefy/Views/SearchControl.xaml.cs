using System.Windows.Controls;
using Musicefy.Core.Models;
using Musicefy.ViewModels;

namespace Musicefy.Views
{
    /// <summary>
    /// Interaction logic for SearchControl.xaml
    /// Implements Echo Music's search screen with state-driven UI
    /// (Idle, Suggestions, Searching, Results).
    /// Filter tab selection is now handled via SelectedFilterCommand (MVVM).
    /// Session 4: Added Top Result card and Online/Library toggle pill.
    /// </summary>
    public partial class SearchControl : UserControl
    {
        public SearchControl()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Constructor with DI-injected ViewModel. Used when resolved from ServiceCollection.
        /// This ensures the SearchControl has a proper DataContext so all bindings work.
        /// Follows the same pattern as HomeControl(HomeViewModel).
        /// </summary>
        public SearchControl(SearchViewModel viewModel) : this()
        {
            DataContext = viewModel;
        }

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

        /// <summary>
        /// Handles click on the Top Result featured card.
        /// Routes to the appropriate action based on the TopResult item type.
        /// </summary>
        private void TopResultCard_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (DataContext is SearchViewModel vm && vm.TopResult != null)
            {
                var item = vm.TopResult;

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

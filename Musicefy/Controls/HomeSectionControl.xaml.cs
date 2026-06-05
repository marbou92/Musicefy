using System.Windows;
using System.Windows.Controls;
using Musicefy.Core.Models;

namespace Musicefy.Controls
{
    /// <summary>
    /// Reusable control that renders a single Home section with a title
    /// and horizontally scrolling content cards.
    /// Supports differentiated card layouts for tracks, albums, and artists
    /// via HomeSectionItemTemplateSelector.
    /// </summary>
    public partial class HomeSectionControl : UserControl
    {
        // ── Routed Events ──────────────────────────────────────────────────

        /// <summary>
        /// Raised when the user clicks "See All" for a section.
        /// The DataContext of this control (a HomeSection) is carried in the event args.
        /// </summary>
        public static readonly RoutedEvent SeeAllRequestedEvent =
            EventManager.RegisterRoutedEvent(
                nameof(SeeAllRequested),
                RoutingStrategy.Bubble,
                typeof(RoutedEventHandler),
                typeof(HomeSectionControl));

        public event RoutedEventHandler SeeAllRequested
        {
            add { AddHandler(SeeAllRequestedEvent, value); }
            remove { RemoveHandler(SeeAllRequestedEvent, value); }
        }

        public HomeSectionControl()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Handles click on a track card. Bubbles up to HomeViewModel's PlayTrackCommand.
        /// </summary>
        private void OnCardClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is Border border && border.DataContext is MusicFile track)
            {
                if (DataContext is HomeSection section)
                {
                    var parent = System.Windows.Media.VisualTreeHelper.GetParent(this);
                    while (parent != null)
                    {
                        if (parent is FrameworkElement fe && fe.DataContext is ViewModels.HomeViewModel vm)
                        {
                            vm.PlayTrackCommand.Execute(track);
                            break;
                        }
                        parent = System.Windows.Media.VisualTreeHelper.GetParent(parent);
                    }
                }
            }
        }

        /// <summary>
        /// Handles click on an album card. Navigates to the album detail view.
        /// </summary>
        private void OnAlbumCardClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is Border border && border.DataContext is AlbumInfo album)
            {
                if (DataContext is HomeSection section)
                {
                    var parent = System.Windows.Media.VisualTreeHelper.GetParent(this);
                    while (parent != null)
                    {
                        if (parent is FrameworkElement fe && fe.DataContext is ViewModels.HomeViewModel vm)
                        {
                            vm.NavigateToAlbumCommand.Execute(album);
                            break;
                        }
                        parent = System.Windows.Media.VisualTreeHelper.GetParent(parent);
                    }
                }
            }
        }

        /// <summary>
        /// Handles click on an artist card. Navigates to the artist detail view.
        /// </summary>
        private void OnArtistCardClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is Border border && border.DataContext is ArtistInfo artist)
            {
                if (DataContext is HomeSection section)
                {
                    var parent = System.Windows.Media.VisualTreeHelper.GetParent(this);
                    while (parent != null)
                    {
                        if (parent is FrameworkElement fe && fe.DataContext is ViewModels.HomeViewModel vm)
                        {
                            vm.NavigateToArtistCommand.Execute(artist);
                            break;
                        }
                        parent = System.Windows.Media.VisualTreeHelper.GetParent(parent);
                    }
                }
            }
        }

        /// <summary>
        /// Handles the "See All" button click. Raises the SeeAllRequested routed event
        /// so parent controls (HomeControl) can handle navigation.
        /// </summary>
        private void SeeAllButton_Click(object sender, RoutedEventArgs e)
        {
            // Raise the bubble event with this control's DataContext (the HomeSection) as the data
            var args = new RoutedEventArgs(SeeAllRequestedEvent, this);
            RaiseEvent(args);
            e.Handled = true;
        }
    }
}

using System.Windows;
using System.Windows.Controls;
using Musicefy.Core.Models;

namespace Musicefy.Controls
{
    /// <summary>
    /// Reusable control that renders a single Home section with a title
    /// and horizontally scrolling content cards.
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

        private void OnCardClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is Border border && border.DataContext is MusicFile track)
            {
                // Bubble up to HomeViewModel's PlayTrackCommand
                if (DataContext is HomeSection section)
                {
                    // Find the HomeViewModel through the visual tree
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

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
    }
}

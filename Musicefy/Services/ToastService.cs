using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace Musicefy.Services
{
    public static class ToastService
    {
        public static void ShowToast(string message, Brush background, int durationMs = 3000)
        {
            var toast = new Border
            {
                Background = background,
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12),
                Margin = new Thickness(10),
                Child = new TextBlock
                {
                    Text = message,
                    Foreground = Brushes.White,
                    FontSize = 14,
                    TextWrapping = TextWrapping.Wrap
                },
                Opacity = 0
            };

            var window = Application.Current.MainWindow;
            if (window == null) return;

            var grid = window.Content as Grid;
            if (grid == null) return;

            // Overlay container
            if (!(grid.FindName("ToastContainer") is StackPanel container))
            {
                container = new StackPanel
                {
                    Name = "ToastContainer",
                    VerticalAlignment = VerticalAlignment.Bottom,
                    HorizontalAlignment = HorizontalAlignment.Right
                };
                grid.Children.Add(container);
                grid.RegisterName("ToastContainer", container);
            }

            container.Children.Add(toast);

            // Fade in
            var fadeIn = new System.Windows.Media.Animation.DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(300)));
            toast.BeginAnimation(UIElement.OpacityProperty, fadeIn);

            // Auto remove after duration
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(durationMs) };
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                var fadeOut = new System.Windows.Media.Animation.DoubleAnimation(1, 0, new Duration(TimeSpan.FromMilliseconds(300)));
                fadeOut.Completed += (s2, e2) => container.Children.Remove(toast);
                toast.BeginAnimation(UIElement.OpacityProperty, fadeOut);
            };
            timer.Start();
        }
    }
}

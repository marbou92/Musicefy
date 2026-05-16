using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace Musicefy.Services
{
    public static class ToastService
    {
        public static void ShowToast(string message, Brush background, int durationMs = 3000)
        {
            // FIXED: Wrapped the window/grid resolution steps inside a Dispatcher invocation pass
            // This safely allows downloads background workers threads to throw toasts without crashing
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                var window = Application.Current.MainWindow;
                if (window == null) return;

                var grid = window.Content as Grid;
                if (grid == null) return;

                var toast = new Border
                {
                    Background = background,
                    CornerRadius = new CornerRadius(12), // Upgraded to match Echo soft capsule shape
                    Padding = new Thickness(16, 12, 16, 12),
                    Margin = new Thickness(0, 0, 0, 10),
                    MaxWidth = 350,
                    Child = new TextBlock
                    {
                        Text = message,
                        StringFormat = "   {0}",
                        Foreground = Brushes.White,
                        FontSize = 13,
                        FontWeight = FontWeights.Bold,
                        TextWrapping = TextWrapping.Wrap,
                        VerticalAlignment = VerticalAlignment.Center
                    },
                    Opacity = 0
                };

                // Add drop shadow to keep toast visible against all background modes
                toast.Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    BlurRadius = 15,
                    ShadowDepth = 1,
                    Opacity = 0.4,
                    Color = Colors.Black
                };

                if (!(grid.FindName("ToastContainer") is StackPanel container))
                {
                    container = new StackPanel
                    {
                        Name = "ToastContainer",
                        VerticalAlignment = VerticalAlignment.Bottom,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Margin = new Thickness(0, 0, 24, 24),
                        MinWidth = 260
                    };
                    grid.Children.Add(container);
                    grid.RegisterName("ToastContainer", container);
                }

                container.Children.Add(toast);

                // Fluid slide-fade input animation vector
                var fadeIn = new DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(250)));
                toast.BeginAnimation(UIElement.OpacityProperty, fadeIn);

                var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(durationMs) };
                timer.Tick += (s, e) =>
                {
                    timer.Stop();
                    var fadeOut = new DoubleAnimation(1, 0, new Duration(TimeSpan.FromMilliseconds(250)));
                    fadeOut.Completed += (s2, e2) =>
                    {
                        // Clean closure lookup ensures elements clear visually out of memory safely
                        if (container.Children.Contains(toast))
                        {
                            container.Children.Remove(toast);
                        }
                    };
                    toast.BeginAnimation(UIElement.OpacityProperty, fadeOut);
                };
                timer.Start();

            }), DispatcherPriority.Normal);
        }
    }
}
